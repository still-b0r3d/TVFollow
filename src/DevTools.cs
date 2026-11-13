using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace TVFollow
{
    // The developer password, kept encrypted for this Windows account by Windows itself, so
    // settings.ini never holds it as typed. A copy of the file on another PC or account can't be
    // read back; there TVFollow just asks for the password again.
    static class DevSecret
    {
        const string Mark = "dpapi:";
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TVFollow developer password");

        public static string Seal(string password)
        {
            if (string.IsNullOrEmpty(password)) return "";
            byte[] locked = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser);
            return Mark + Convert.ToBase64String(locked);
        }

        public static string Open(string saved)
        {
            if (string.IsNullOrEmpty(saved) || !saved.StartsWith(Mark)) return "";
            try
            {
                byte[] locked = Convert.FromBase64String(saved.Substring(Mark.Length));
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(locked, Entropy, DataProtectionScope.CurrentUser));
            }
            catch { return ""; }
        }
    }

    // a TV as the developer tools see it
    class DevStatus
    {
        public bool Answering;   // this TV answered
        public bool OtherRoku;   // another Roku answered at its address
        public bool DevMode;     // developer mode is on
        public bool Limited;     // Network access is on Limited, which hides the TV's list of apps
        public bool AppKnown;    // the TV said which apps it has
        public DevApp App;       // the pushed app; null for none
    }

    // how something went, in a sentence for the window
    class DevReport
    {
        public readonly bool Ok;
        public readonly string Text;
        // It didn't work, and not because of the app: the TV is off or away, developer mode is
        // off, the password is missing or wrong. Other files wouldn't have fared better.
        public readonly bool TvTrouble;

        public DevReport(bool ok, string text) : this(ok, text, false) { }

        public DevReport(bool ok, string text, bool tvTrouble)
        {
            Ok = ok;
            Text = text ?? "";
            TvTrouble = tvTrouble;
        }
    }

    // The developer tools of every TV. Four jobs run in the background while TVFollow follows the
    // monitors: putting a pushed app back when it vanished from its TV, pushing an app's folder
    // again when a file in it is saved, switching the TV's input when the pushed app asks, and
    // opening the app when the TV goes to Roku's home screen. None of them runs while the tools
    // are switched off in Settings.
    class DevTools : IDisposable
    {
        readonly Settings settings;
        public readonly List<DevTv> Tvs;

        // a message in the taskbar corner (the tray fills this in): title, text
        public Action<string, string> Notify = delegate { };

        public DevTools(Settings settings, List<Controller> controllers) : this(settings, controllers, true) { }

        DevTools(Settings settings, List<Controller> controllers, bool background)
        {
            this.settings = settings;
            Tvs = controllers.ConvertAll(c => new DevTv(this, settings, c));
            if (!background) return;
            foreach (DevTv tv in Tvs) tv.Controller.BeforeFollowOff = tv.BeforeOff;
            Refresh();
        }

        // For TVFollow.exe push and screenshot: the tools with none of the background jobs, which
        // stay with the TVFollow in the taskbar corner.
        public static DevTools ForCommandLine(Settings settings, List<Controller> controllers)
        {
            return new DevTools(settings, controllers, false);
        }

        public Settings Settings { get { return settings; } }

        // a TV's tools; the first TV's for null
        public DevTv For(Controller controller)
        {
            return Tvs.Find(t => t.Controller == controller) ?? Tvs[0];
        }

        // the switch in Settings or a TV's options changed: folders are watched or not to match
        public void Refresh()
        {
            foreach (DevTv tv in Tvs) tv.Refresh();
        }

        public void Dispose()
        {
            foreach (DevTv tv in Tvs) tv.Close();
        }
    }

    // One TV's developer tools: pushing, removing and pictures, and the background jobs.
    class DevTv
    {
        public static int AskMs = 3000;
        public static int PushMs = 60000;      // an upload over Wi-Fi, and the TV unpacking it
        public static int PutBackMs = 30000;   // putting an app back holds up turning the TV off
        public static int SettleMs = 1500;     // quiet time after the last change in a watched folder
        public static int RetryMs = 3000;      // looking again for a watched folder that went away
        public static int InputsIdleMs = 3000; // how often TVFollow looks whether the pushed app is on screen
        public static int HelloAfterMs = 500;  // the console has to stay connected this long before the hello
        public static int HomeSettleMs = 4000; // the home screen has to stay this long before the app opens
        public static int HomeCheckMs = 250;   // how often the TV is asked meanwhile
        public static int SwitchingMs = 10000; // after TVFollow switched to an input, the home screen doesn't count this long, or until something else shows
        public static int SwitchLookMs = 500;  // how often TVFollow looks meanwhile, to see the input as soon as it's there
        public static int SwitchStartMs = 1000; // a look this soon after TVFollow switched may still see what was on before
        public static int StayHomeMs = 60000;  // back home this soon after the app opened: the TV stays home

        // what the pushed app prints on its console: "TVFollow hello" for a hello, and
        // "TVFollow ask <token> tvinput.hdmi2" to switch, with the token from the last hello
        public const string HelloLine = "TVFollow hello", AskLine = "TVFollow ask ";

        readonly DevTools tools;
        readonly Settings settings;
        public readonly Controller Controller;
        readonly object turn = new object();         // one push, removal or picture at a time
        readonly object watchLock = new object();    // the watching below
        readonly object refreshing = new object();   // Refresh from the window and from the retry timer, one at a time
        FileSystemWatcher watcher;
        Timer settle, retry;
        string watching;
        bool saving, savedAgain;   // a push on save is running; another save came meanwhile
        bool closed;
        bool toldDevModeOff, toldPutBackFailed;
        string toldSaveTrouble;   // the TV's trouble a push on save last told of, so it's told once
        string lastPushed;   // a fingerprint of the last zip the TV took, so a save that changed nothing isn't pushed

        // the app's console (port 8085), shared by the app log window and the app's asks
        public readonly DevConsole Console;

        // Letting the app switch inputs. Locks, always taken in this order: inputsLock (the looks
        // and the work the app asks for, one at a time; held across calls to the TV), switching
        // (starting or stopping the console reading, each whole), then watchLock, the console's
        // own lock, and listenLock (the state the console's callbacks touch, held only briefly).
        readonly object inputsLock = new object();
        readonly object switching = new object();
        readonly object listenLock = new object();
        readonly ConsoleListener asks = new ConsoleListener();
        Timer inputsTimer;            // under watchLock
        int inputsRun, lookedRun;     // each start of the looking gets a number; the state below is for lookedRun
        // under inputsLock
        bool appShown;                // the pushed app was on screen at the last look
        int missed;                   // the looks in a row the TV didn't answer
        bool toldNoInputs;
        string lastRefused;           // the last ask that wasn't carried out, said once in a row
        DateTime openedAt = DateTime.MinValue;  // when the home screen last made TVFollow open the app
        DateTime switchesOver = DateTime.MinValue;  // TVFollow's switches to an input before this are over: the TV has shown something besides the app since
        bool stayHome;                // the TV went home again right after: it stays there until it shows something else
        bool toldNoApp;               // said that there's no pushed app to open
        // under watchLock: what the log last said about the two options that need the looks
        bool saidInputs, saidHome;
        // under listenLock
        bool listening;               // reading the console for the app's asks
        int connection;               // goes up with each connection to the console, each loss of one, and each start and stop
        bool settled;                 // the console has stayed connected a moment: the hello can go
        bool firstLine;               // the next line with something in it is the connection's first
        bool helloDue;                // this connection's app hasn't had a hello yet
        bool helloWanted;             // a hello is waiting to be sent
        string askWanted;             // the latest ask waiting to be carried out, from connection askFrom
        int askFrom;
        bool draining;                // a worker is carrying out what's waiting
        bool toldBusy;
        int toldHello;                // the connection whose hello the log mentioned
        Timer helloTimer;             // the hello, once the console has stayed connected a moment
        string token = "";            // the code in this connection's hello: an ask has to carry it
        // under the console's own lock (its callbacks)
        string partial = "";          // console text after its last line break

        // something happened in the background (a push on save, an app put back): the window shows it
        public event Action<DevReport> Background = delegate { };

        public DevTv(DevTools tools, Settings settings, Controller controller)
        {
            this.tools = tools;
            this.settings = settings;
            Controller = controller;
            Console = new DevConsole(() => Tv.Ip);
            Console.SavePath = () => AppLogPath;
            asks.Connected = ConsoleConnected;
            asks.Text = ConsoleText;
            asks.Trouble = delegate { ConsoleLost(); };
        }

        public TvSettings Tv { get { return Controller.Tv; } }

        public string Label { get { return Controller.Label; } }

        // the copy of the app pushed last, kept to put back
        public string KeptPath { get { return Path.Combine(Path.Combine(Settings.Dir, "dev"), FileName(Tv.Serial) + ".zip"); } }

        public bool HasKept { get { return File.Exists(KeptPath); } }

        // where the app log is saved while TVFollow reads it (DevConsole writes it)
        public string AppLogPath { get { return Path.Combine(Path.Combine(Settings.Dir, "dev"), FileName(Tv.Serial) + " app log.txt"); } }

        // the name a picture of the TV's screen is saved under: the app's name and the time
        public static string PictureName(string appName, string extension)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) appName = appName.Replace(c, '_');
            if (appName.Trim().Length == 0) appName = "App";
            return (appName.Trim() + " " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + "." + extension).Trim();
        }

        static string FileName(string serial)
        {
            var name = new StringBuilder();
            foreach (char c in serial) name.Append(char.IsLetterOrDigit(c) || c == '-' ? c : '_');
            return name.Length > 0 ? name.ToString() : "tv";
        }

        RokuClient Ecp() { return new RokuClient(Tv.Ip, settings.Port); }

        DevClient Installer() { return new DevClient(Tv.Ip, Tv.DevPassword); }

        public DevStatus Status(int timeoutMs)
        {
            var s = new DevStatus();
            RokuClient ecp = Ecp();
            DeviceInfo info = ecp.GetDeviceInfo(timeoutMs);
            if (info == null) return s;
            if (Tv.Serial.Length > 0 && info.Serial != Tv.Serial) { s.OtherRoku = true; return s; }
            s.Answering = true;
            s.DevMode = info.DeveloperEnabled;
            s.Limited = !info.AllowsControl;
            bool answered;
            s.App = ecp.GetDevApp(timeoutMs, out answered);
            s.AppKnown = answered;
            return s;
        }

        // Before the password goes anywhere: the device at the TV's address has its serial number
        // and is in developer mode, so another Roku that got the address isn't asked. Null when
        // all's well.
        DevResult Ready()
        {
            DeviceInfo info = Ecp().GetDeviceInfo(AskMs);
            if (info == null) return new DevResult(DevOutcome.NoAnswer, "");
            if (Tv.Serial.Length > 0 && info.Serial != Tv.Serial) return new DevResult(DevOutcome.OtherRoku, "");
            if (!info.DeveloperEnabled) return new DevResult(DevOutcome.DevModeOff, "");
            if (Tv.DevPassword.Length == 0) return new DevResult(DevOutcome.NoPassword, "");
            return null;
        }

        // the installer's answer, once the TV itself has answered: silence is the installer's
        static DevResult FromInstaller(DevResult r)
        {
            return r.Outcome == DevOutcome.NoAnswer ? new DevResult(DevOutcome.InstallerSilent, r.Message) : r;
        }

        static string LogWords(DevResult r)
        {
            string words;
            switch (r.Outcome)
            {
                case DevOutcome.Done: words = "done"; break;
                case DevOutcome.Same: words = "the TV had the same files, opened it"; break;
                case DevOutcome.NoPassword: words = "no password yet"; break;
                case DevOutcome.WrongPassword: words = "wrong password"; break;
                case DevOutcome.DevModeOff: words = "developer mode is off"; break;
                case DevOutcome.OtherRoku: words = "another Roku answers at the TV's address"; break;
                case DevOutcome.NoAnswer: words = "no answer"; break;
                case DevOutcome.InstallerSilent: words = "the installer didn't answer"; break;
                case DevOutcome.TooSlow: words = "too slow"; break;
                case DevOutcome.UpdateCheck: words = "the TV wants an update check first"; break;
                default: words = "refused"; break;
            }
            return r.Message.Length > 0 ? words + " (" + r.Message + ")" : words;
        }

        // Pushes the app, keeps a copy to put back, and says how it went. Off the window's thread.
        public DevReport Push(DevPackage app, string why)
        {
            lock (turn) return PushNow(app, why, PushMs);
        }

        DevReport PushNow(DevPackage app, string why, int timeoutMs)
        {
            DevResult r = Ready() ?? FromInstaller(Installer().Push(app.Zip, timeoutMs));
            // the TV leaves the same files alone, without opening them
            if (r.Outcome == DevOutcome.Same) Ecp().Launch("dev", AskMs);
            string size = DevPackage.Size(app.Zip.Length);
            Controller.Say("developer tools: " + why + " " + app + " (" + size + "): " + LogWords(r));
            if (!r.Ok)
            {
                string problem = r.Problem;
                // Roku OS 15 turns away zips under 512 bytes as corrupt (Roku's own tools say so too)
                if (app.Zip.Length < 512 && (r.Message.IndexOf("corrupt", StringComparison.OrdinalIgnoreCase) >= 0 || r.Message.IndexOf("unzip", StringComparison.OrdinalIgnoreCase) >= 0))
                    problem += " Roku TVs turn away apps under 512 bytes, and this one is " + app.Zip.Length + ": add a file to it, a picture say.";
                // only the TV's own words about the app (its code doesn't compile, say) are about the app
                return new DevReport(false, problem, r.Outcome != DevOutcome.Refused);
            }
            Keep(app.Zip);
            lastPushed = Fingerprint(app.Zip);
            toldDevModeOff = toldPutBackFailed = false;
            toldSaveTrouble = null;
            string text = r.Outcome == DevOutcome.Same
                ? "The TV already had these very files, so TVFollow opened the app."
                : "Pushed " + app + " (" + size + "). It's open on the TV.";
            if (app.Note.Length > 0) text += " " + app.Note;
            return new DevReport(true, text);
        }

        static string Fingerprint(byte[] zip)
        {
            using (SHA256 sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(zip));
        }

        void Keep(byte[] zip)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(KeptPath));
                string temp = KeptPath + ".tmp";
                File.WriteAllBytes(temp, zip);
                if (File.Exists(KeptPath)) File.Replace(temp, KeptPath, null);
                else File.Move(temp, KeptPath);
            }
            catch (Exception e) { Controller.Say("developer tools: couldn't keep a copy of the app: " + e.Message); }
        }

        public DevReport Remove()
        {
            lock (turn)
            {
                DevResult r = Ready() ?? FromInstaller(Installer().Remove(AskMs * 5));
                Controller.Say("developer tools: remove the app: " + LogWords(r));
                if (!r.Ok) return new DevReport(false, r.Problem);
                // and it's not put back
                try { if (File.Exists(KeptPath)) File.Delete(KeptPath); }
                catch (Exception e) { Controller.Say("developer tools: couldn't delete the kept copy: " + e.Message); }
                lastPushed = null;
                return new DevReport(true, "The app is off the TV, and TVFollow forgot its copy.");
            }
        }

        // a picture of the pushed app's screen
        public DevReport Screenshot(out byte[] picture, out string extension)
        {
            picture = null;
            extension = "";
            lock (turn)
            {
                DevResult r = Ready();
                if (r == null)
                {
                    picture = Installer().Screenshot(AskMs * 5, out extension, out r);
                    r = FromInstaller(r);
                }
                Controller.Say("developer tools: screenshot: " + LogWords(r));
                if (picture == null) return new DevReport(false, r.Problem ?? "No picture came back.");
                return new DevReport(true, "");
            }
        }

        public DevReport CheckPassword()
        {
            lock (turn)
            {
                DevResult r = Ready() ?? FromInstaller(Installer().CheckPassword(AskMs * 2));
                return r.Ok ? new DevReport(true, "The password is right.") : new DevReport(false, r.Problem);
            }
        }

        // opens the pushed app on the TV (it needs no password)
        public DevReport Open()
        {
            int code = Ecp().Launch("dev", AskMs);
            if (code >= 200 && code < 300) return new DevReport(true, "Opened on the TV.");
            return new DevReport(false, code == 0 ? "The TV didn't answer. Is it on, and on this network?" : "The TV didn't open it. Is there an app on it?");
        }

        // Runs just before TVFollow turns the TV off with the monitors (the TV is on and showing the
        // PC). If the pushed app vanished from the TV (a Roku update, say), the kept copy goes back
        // on now, while nobody's looking. If developer mode itself went off, or the app can't go
        // back, a message says so, once until things change. True when it pushed, or tried to:
        // that took a while, and the TV now shows the app.
        public bool BeforeOff()
        {
            if (!settings.DeveloperTools || !Tv.DevPushBack || !HasKept) return false;
            DevStatus s = Status(AskMs);
            if (!s.Answering) return false;
            string where = settings.SeveralTvs ? Label : "the TV";
            if (!s.DevMode)
            {
                if (toldDevModeOff) return false;
                toldDevModeOff = true;
                Controller.Say("developer tools: developer mode is off, so the app can't be put back");
                tools.Notify("Developer mode is off on " + where,
                    "TVFollow can't put your app back until it's on again. Turn it on with the TV's own remote, then type its password in the developer tools.");
                return false;
            }
            toldDevModeOff = false;
            if (!s.AppKnown || s.App != null) return false;  // still there, or the TV won't say
            byte[] zip;
            try { zip = File.ReadAllBytes(KeptPath); }
            catch (Exception e) { Controller.Say("developer tools: couldn't read the kept copy: " + e.Message); return false; }
            DevPackage app = DevPackage.Describe(zip);
            DevReport r;
            lock (turn) r = PushNow(app, "put back", PutBackMs);
            if (r.Ok) tools.Notify("App put back", "TVFollow put " + app + " back on " + where + ".");
            else if (!toldPutBackFailed)
            {
                toldPutBackFailed = true;
                tools.Notify("Couldn't put your app back on " + where, r.Text);
            }
            Background(r);
            return true;
        }

        // ---- pushing on save ----

        // Is this folder on a drive letter (C:\apps\mine)? The only kind TVFollow looks at on its
        // own. Anything else may be a network path (\\server\share, also written //server/share
        // or \\?\UNC\server\share), and just asking Windows whether such a folder is there makes
        // it connect to that computer, even when the folder was only named in a settings file.
        // Told from the text alone: a drive letter, a colon, a slash.
        public static bool OnADrive(string folder)
        {
            return folder != null && folder.Length >= 3 &&
                ((folder[0] >= 'A' && folder[0] <= 'Z') || (folder[0] >= 'a' && folder[0] <= 'z')) &&
                folder[1] == ':' && (folder[2] == '\\' || folder[2] == '/');
        }

        // asking Windows whether the watched folder is there (the tests listen in on this)
        public static Func<string, bool> FolderThere = Directory.Exists;

        // Starts or stops watching the app's folder, as the settings now say. Only folders on
        // a drive letter (see OnADrive).
        public void Refresh()
        {
            lock (refreshing) RefreshNow();
            RefreshInputs();
        }

        void RefreshNow()
        {
            string folder = Tv.DevFolder;
            bool wanted = settings.DeveloperTools && Tv.DevOnSave && OnADrive(folder);
            bool there = wanted && FolderThere(folder);
            lock (watchLock)
            {
                if (closed) return;
                if (there ? string.Equals(folder, watching, StringComparison.OrdinalIgnoreCase) : watching == null)
                {
                    if (wanted && !there) TryAgainLater();
                    return;
                }
            }
            StopWatching();
            if (!there)
            {
                // a folder a build tool cleared and makes again: watched again once it's back
                if (wanted) TryAgainLater();
                return;
            }
            try
            {
                var w = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                w.Changed += (sender, e) => Saved(e.FullPath);
                w.Created += (sender, e) => Saved(e.FullPath);
                w.Deleted += (sender, e) => Saved(e.FullPath);
                w.Renamed += (sender, e) => Saved(e.FullPath);
                // too many changes at once, or the folder itself went away: Windows stops telling,
                // so the folder is watched afresh (and pushed, if anything changed meanwhile)
                w.Error += (sender, e) =>
                {
                    Controller.Say("developer tools: lost track of " + folder + ", watching it again");
                    StopWatching();
                    TryAgainLater();
                };
                lock (watchLock)
                {
                    if (closed) { w.Dispose(); return; }
                    watcher = w;
                    watching = folder;
                }
                w.EnableRaisingEvents = true;
                Controller.Say("developer tools: pushing " + folder + " again whenever a file in it is saved");
                // anything saved while it wasn't watched goes too (an unchanged folder isn't sent)
                if (lastPushed != null) Saved(null);
            }
            catch (Exception e)
            {
                Controller.Say("developer tools: can't watch " + folder + ": " + e.Message);
                StopWatching();
            }
        }

        void TryAgainLater()
        {
            lock (watchLock)
            {
                if (closed) return;
                if (retry == null) retry = new Timer(delegate { Refresh(); });
                retry.Change(RetryMs, Timeout.Infinite);
            }
        }

        public bool Watching
        {
            get { lock (watchLock) return watching != null; }
        }

        public void StopWatching()
        {
            lock (watchLock)
            {
                if (watcher != null)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                    watcher = null;
                }
                if (settle != null)
                {
                    settle.Dispose();
                    settle = null;
                }
                watching = null;
            }
        }

        // for good: TVFollow is stopping
        public void Close()
        {
            lock (watchLock)
            {
                closed = true;
                if (retry != null)
                {
                    retry.Dispose();
                    retry = null;
                }
                if (inputsTimer != null)
                {
                    inputsTimer.Dispose();
                    inputsTimer = null;
                }
            }
            StopWatching();
            StopListening();
        }

        // a change in the folder: the push waits until the saving stops
        void Saved(string path)
        {
            lock (watchLock)
            {
                if (watching == null) return;
                // an editor's temporary file, or a change inside .git, isn't a save of the app
                if (path != null && path.Length > watching.Length && DevPackage.Skipped(path.Substring(watching.Length))) return;
                if (settle == null) settle = new Timer(delegate { PushSaved(); });
                settle.Change(SettleMs, Timeout.Infinite);
            }
        }

        // One push on save at a time. Saves that come while one is going push again once it's
        // done, with the folder as it is then, so the TV always ends up with the newest files.
        void PushSaved()
        {
            lock (watchLock)
            {
                if (saving) { savedAgain = true; return; }
                saving = true;
                savedAgain = false;
            }
            while (true)
            {
                try { PushFolder(); }
                catch (Exception e) { Controller.Say("developer tools: push on save: " + e.Message); }
                lock (watchLock)
                {
                    if (!savedAgain || watching == null) { saving = false; return; }
                    savedAgain = false;
                }
            }
        }

        void PushFolder()
        {
            string folder;
            lock (watchLock) folder = watching;
            // gone (a build tool clearing it, say): pushed once it's back, without a message meanwhile
            if (folder == null || !Directory.Exists(folder)) return;
            DevReport r = null;
            // like the following, nothing goes to the TV's address on another network (a laptop
            // away from home), where something else may have that address
            if (!Controller.OnTvNetwork())
            {
                r = new DevReport(false, "This PC isn't on the TV's network, so the app wasn't pushed.", true);
                if (r.Text != toldSaveTrouble) Controller.Say("developer tools: push on save: this PC isn't on the TV's network, so nothing is pushed");
            }
            // the folder is zipped in this TV's turn, so no push waiting behind it can send older files
            else lock (turn)
            {
                DevPackage app = null;
                try { app = DevPackage.FromFolder(folder); }
                catch (DevPackageException e) { r = new DevReport(false, e.Message); }
                catch (Exception e) { r = new DevReport(false, "Couldn't zip the folder: " + e.Message); }
                if (app != null)
                {
                    // Windows can tell of a save twice, the second time a moment later; the same
                    // files make the same zip, and the TV already has them
                    if (Fingerprint(app.Zip) == lastPushed) return;
                    r = PushNow(app, "pushed on save", PushMs);
                }
            }
            // The TV's own trouble (it's off or away, developer mode is off, the password is
            // wrong) is told once, not again at every save until it's over or turns into other
            // trouble: an editor that saves by itself would pile the notifications up. What's
            // wrong with the app itself is told each time, since each save is a new try at it.
            bool tell = !r.Ok;
            if (r.Ok || !r.TvTrouble) toldSaveTrouble = null;
            else if (r.Text == toldSaveTrouble) tell = false;
            else toldSaveTrouble = r.Text;
            if (tell) tools.Notify("Couldn't push your app", r.Text);
            Background(r);
        }

        // ---- letting the app switch inputs ----
        //
        // A Roku TV refuses an app that tries to switch its inputs, so the pushed app asks TVFollow.
        // While the app is on screen, TVFollow reads the app's console (the app log) and tells
        // the app it's there, and which inputs the TV has, with an input event the app reads with
        // roInput. The hello carries a new token with each connection; the app prints
        // "TVFollow ask <token> tvinput.hdmi2", and only this connection's token counts, because
        // the TV repeats its recent console lines to a new reader. TVFollow only ever talks to
        // the TV here: nothing on this PC listens for the app.

        // Starts or stops the looking, as the settings now say: it's for letting the app switch
        // inputs, and for opening the app when the TV goes home.
        void RefreshInputs()
        {
            bool inputsOn = settings.DeveloperTools && Tv.DevInputs;
            bool homeOn = settings.DeveloperTools && Tv.DevHome;
            bool wanted = inputsOn || homeOn;
            bool tellInputs, tellHome;
            lock (watchLock)
            {
                if (closed) return;
                tellInputs = inputsOn != saidInputs;
                tellHome = homeOn != saidHome;
                saidInputs = inputsOn;
                saidHome = homeOn;
                if (wanted != (inputsTimer != null))
                {
                    if (wanted)
                    {
                        inputsRun++;
                        inputsTimer = new Timer(delegate { LookForAsks(); });
                        inputsTimer.Change(0, Timeout.Infinite);
                    }
                    else
                    {
                        inputsTimer.Dispose();
                        inputsTimer = null;
                    }
                }
            }
            if (!inputsOn) StopListening();
            if (tellInputs)
                Controller.Say(inputsOn ? "developer tools: the app on the TV can ask for the TV's inputs now"
                    : "developer tools: the app on the TV can't ask for the TV's inputs any more");
            if (tellHome)
                Controller.Say(homeOn ? "developer tools: the app opens whenever the TV goes to Roku's home screen"
                    : "developer tools: the app doesn't open by itself any more when the TV goes home");
        }

        public bool LookingForAsks
        {
            get { lock (watchLock) return inputsTimer != null; }
        }

        public bool ListeningForAsks
        {
            get { lock (listenLock) return listening; }
        }

        // the looking that started as run is still wanted: not switched off or closed since
        bool StillWanted(int run)
        {
            lock (watchLock) return !closed && inputsTimer != null && inputsRun == run;
        }

        // One look every few seconds: is the pushed app on screen? While it is, its console is read.
        void LookForAsks()
        {
            int next = InputsIdleMs;
            lock (inputsLock)
            {
                int run;
                lock (watchLock)
                {
                    if (inputsTimer == null) return;
                    run = inputsRun;
                }
                if (lookedRun != run)
                {
                    // started again: nothing seen before counts
                    lookedRun = run;
                    appShown = toldNoInputs = stayHome = toldNoApp = false;
                    openedAt = switchesOver = DateTime.MinValue;
                    missed = 0;
                    lastRefused = null;
                }
                try { next = Look(run); }
                catch (Exception e) { Controller.Say("developer tools: looking for the app's asks: " + e.Message); }
            }
            lock (watchLock)
            {
                if (inputsTimer != null) inputsTimer.Change(next, Timeout.Infinite);
            }
        }

        // one look; says how long until the next
        int Look(int run)
        {
            // like the following, nothing goes to the TV's address on another network (a laptop away from home)
            if (!Controller.OnTvNetwork())
            {
                Gone();
                return InputsIdleMs;
            }
            RokuClient ecp = Ecp();
            DateTime asked = DateTime.UtcNow;
            string app = ecp.GetActiveApp(AskMs);
            if (app == null)
            {
                // no answer: a moment's hiccup, or the TV went off. A few in a row count as the app
                // gone, and the looks slow down until the TV answers again.
                if (++missed < 3) return InputsIdleMs;
                Gone();
                return InputsIdleMs * 5;
            }
            missed = 0;
            if (Tv.DevHome) HomeLook(ecp, app, asked, run);
            // on the way to an input TVFollow switched to, the looks come sooner, so the input is
            // seen as soon as it's there and a Home right after it is a trip home
            int next = Tv.DevHome && OnTheWayToInput() ? Math.Min(SwitchLookMs, InputsIdleMs) : InputsIdleMs;
            if (!Tv.DevInputs || app != "dev")
            {
                Gone();
                return next;
            }
            if (!appShown)
            {
                // only this TV: another Roku that got its address isn't told this TV's inputs
                DeviceInfo info = ecp.GetDeviceInfo(AskMs);
                if (info == null || (Tv.Serial.Length > 0 && info.Serial != Tv.Serial)) return next;
                appShown = true;
                StartListening(run);
            }
            else
            {
                // a hello that didn't get through (the TV didn't take it, say) goes again
                bool again;
                lock (listenLock) again = listening && settled && helloDue;
                if (again) Hello(ecp);
            }
            return next;
        }

        void Gone()
        {
            appShown = false;
            StopListening();
        }

        // ---- the app as the TV's home screen ----
        //
        // The TV went to Roku's home screen (backing out of another app, say, or turned on), so
        // the pushed app opens. A Roku TV also shows its home screen on the way from an app to
        // an input: about 2.5 seconds from the app to HDMI 1, measured on a Roku TV with Roku
        // OS 16. On the way to an input TVFollow switched to (for the app, from the remote or
        // Settings, or to the PC), the home screen doesn't count at all; any other home screen
        // has to stay a few seconds first. Going home again right after TVFollow opened the app,
        // with nothing in between, means someone wants the home screen: pressing Home twice in a
        // row. The TV then stays home until it shows something else or turns off. Not while
        // TVFollow is paused, not while it's turning the TV on or off, and only on a TV that's
        // on, since opening the app could turn the screen on. (Under inputsLock.)
        void HomeLook(RokuClient ecp, string app, DateTime asked, int run)
        {
            if (app != "")
            {
                stayHome = false;
                if (app != "dev")
                {
                    // something besides the app came on (the input it asked for, say): the next
                    // trip home opens the app again, and a switch TVFollow made before this look
                    // is over, unless it was so recent that the TV may not have left what was on
                    openedAt = DateTime.MinValue;
                    if (asked - Controller.InputSwitchedAt >= TimeSpan.FromMilliseconds(SwitchStartMs)) switchesOver = asked;
                }
                return;
            }
            DeviceInfo info = ecp.GetDeviceInfo(AskMs);
            if (!IsThisTv(info)) return;
            if (!info.IsOn)
            {
                // off: turned on again and landing home, it opens the app
                stayHome = false;
                openedAt = DateTime.MinValue;
                switchesOver = asked;
                return;
            }
            if (stayHome || !HomeWanted(run) || OnTheWayToInput()) return;
            // a TV without a pushed app isn't watched for nothing
            bool answered;
            DevApp pushed = ecp.GetDevApp(AskMs, out answered);
            if (answered && pushed == null)
            {
                if (!toldNoApp) Controller.Say("developer tools: the TV went to Roku's home screen, but there's no pushed app to open");
                toldNoApp = true;
                return;
            }
            toldNoApp = false;
            // a home screen that doesn't stay is one between two apps, or between an app and an
            // input someone else switched to (with the TV's own remote, say)
            if (!StaysHome(ecp, run)) return;
            // and a TV turned off meanwhile still says it shows the home screen
            info = ecp.GetDeviceInfo(AskMs);
            if (!IsThisTv(info) || !info.IsOn) return;
            if (DateTime.UtcNow - openedAt < TimeSpan.FromMilliseconds(StayHomeMs))
            {
                stayHome = true;
                Controller.Say("developer tools: back on Roku's home screen right after the app opened, so the TV stays there");
                return;
            }
            // still wanted after the wait (not unticked, closed, paused or busy meanwhile, and no
            // input switched to)
            if (!HomeWanted(run) || OnTheWayToInput()) return;
            int code = ecp.Launch("dev", AskMs);
            bool opened = code >= 200 && code < 300;
            openedAt = DateTime.UtcNow;
            string name = pushed != null ? Clean(pushed.Name) : "the app";
            Controller.Say(opened ? "developer tools: the TV went to Roku's home screen, so " + name + " opened"
                : "developer tools: the TV went to Roku's home screen, but " + name + " didn't open" + (code == 0 ? " (no answer)" : " (the TV said " + code + ")"));
            // a TV that won't open it isn't asked again until it shows something else
            if (!opened) stayHome = true;
        }

        // Watches the home screen for HomeSettleMs, asking every HomeCheckMs: false as soon as the
        // TV shows something else or doesn't answer, TVFollow switches to an input, or the app
        // isn't wanted any more (unticked, closed, paused or busy).
        bool StaysHome(RokuClient ecp, int run)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(HomeSettleMs);
            while (true)
            {
                int left = (int)(until - DateTime.UtcNow).TotalMilliseconds;
                if (left <= 0) return true;
                Thread.Sleep(Math.Min(HomeCheckMs, left));
                if (!HomeWanted(run) || OnTheWayToInput() || ecp.GetActiveApp(AskMs) != "") return false;
            }
        }

        // the home screen should still open the app: the looking not stopped, the option on, and
        // TVFollow neither paused nor turning this TV on or off
        bool HomeWanted(int run)
        {
            return StillWanted(run) && settings.DeveloperTools && Tv.DevHome && !settings.Paused && !Controller.Busy;
        }

        // TVFollow switched this TV to an input a moment ago (for the app, from the remote or
        // Settings, or to the PC), and the TV hasn't shown anything besides the app since: the
        // home screen now is the one on the way there
        bool OnTheWayToInput()
        {
            DateTime at = Controller.InputSwitchedAt;
            return at > switchesOver && DateTime.UtcNow - at < TimeSpan.FromMilliseconds(SwitchingMs);
        }

        // this TV, answering (not another Roku that got its address)
        bool IsThisTv(DeviceInfo info)
        {
            return info != null && (Tv.Serial.Length == 0 || info.Serial == Tv.Serial);
        }

        // Each whole, under switching: an untick or Close in the middle of a look always wins.
        void StartListening(int run)
        {
            lock (switching)
            {
                if (!StillWanted(run)) return;
                lock (listenLock)
                {
                    if (listening) return;
                    listening = true;
                    settled = false;
                    token = "";
                    connection++;
                }
                Console.Listen(asks);
            }
        }

        void StopListening()
        {
            lock (switching)
            {
                lock (listenLock)
                {
                    if (!listening) return;
                    listening = false;
                    Unsettle();
                    helloWanted = false;
                    askWanted = null;
                }
                Console.Unlisten(asks);
            }
        }

        // no hello and no asks on the connection there was (under listenLock)
        void Unsettle()
        {
            settled = false;
            token = "";
            connection++;
            if (helloTimer != null)
            {
                helloTimer.Dispose();
                helloTimer = null;
            }
        }

        // Connected (again): a new token, so asks from before don't count. The hello goes once
        // the connection has lasted a moment. (On the console's thread.)
        void ConsoleConnected()
        {
            lock (listenLock)
            {
                if (!listening) return;
                Unsettle();
                token = Guid.NewGuid().ToString("N").Substring(0, 8);
                helloDue = firstLine = true;
                partial = "";
                int mine = connection;
                helloTimer = new Timer(delegate { Settled(mine); }, null, HelloAfterMs, Timeout.Infinite);
            }
        }

        void ConsoleLost()
        {
            lock (listenLock) Unsettle();
        }

        void Settled(int mine)
        {
            lock (listenLock)
            {
                if (!listening || connection != mine) return;
                settled = true;
                helloWanted = true;
            }
            Kick();
        }

        // the console's text, line by line (under the console's lock)
        void ConsoleText(string chunk)
        {
            string text = partial + chunk;
            int start = 0, end;
            while ((end = text.IndexOf('\n', start)) >= 0)
            {
                ConsoleLine(text.Substring(start, end - start));
                start = end + 1;
            }
            // a line that never ends isn't an ask
            partial = text.Length - start > 4096 ? "" : text.Substring(start);
        }

        void ConsoleLine(string line)
        {
            if (line.Trim().Length == 0) return;
            bool first;
            lock (listenLock)
            {
                first = firstLine;
                firstLine = false;
            }
            // a TV that another program's console session holds says so first, then hangs up:
            // no hello on this connection
            if (first && line.IndexOf("connection is already in use", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bool tell;
                lock (listenLock)
                {
                    Unsettle();
                    tell = !toldBusy;
                    toldBusy = true;
                }
                if (tell) Controller.Say("developer tools: another program is reading the app's console, so the app can't ask for inputs until it stops");
                return;
            }
            // only lines that start with "TVFollow": the app's other lines and the TV's own pass by
            line = line.TrimStart();
            if (line.StartsWith(AskLine, StringComparison.Ordinal))
            {
                string[] words = line.Substring(AskLine.Length).Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                lock (listenLock)
                {
                    // an ask with another token is one the TV repeated to a new reader, or one from before
                    if (!listening || words.Length < 2 || token.Length == 0 || words[0] != token) return;
                    askWanted = words[1];
                    askFrom = connection;
                }
                Kick();
                return;
            }
            if (line.StartsWith(HelloLine, StringComparison.Ordinal))
            {
                lock (listenLock)
                {
                    if (!listening || !settled) return;   // one comes anyway once the console settles
                    helloWanted = true;
                }
                Kick();
            }
        }

        // What the app asks for is carried out off the console's thread by one worker at a time:
        // the latest ask and one hello at most, so an app printing them in a loop can't pile up work.
        void Kick()
        {
            lock (listenLock)
            {
                if (draining) return;
                draining = true;
            }
            ThreadPool.QueueUserWorkItem(delegate { Drain(); });
        }

        void Drain()
        {
            lock (inputsLock)
            {
                while (true)
                {
                    bool hello;
                    string ask;
                    int from;
                    lock (listenLock)
                    {
                        hello = helloWanted;
                        ask = askWanted;
                        from = askFrom;
                        helloWanted = false;
                        askWanted = null;
                        if (!hello && ask == null)
                        {
                            draining = false;
                            return;
                        }
                    }
                    try
                    {
                        if (ask != null) Switch(ask, from);
                        if (hello) Hello(Ecp());
                    }
                    catch (Exception e) { Controller.Say("developer tools: the app's asks: " + e.Message); }
                }
            }
        }

        // the app asked for an input (under inputsLock)
        void Switch(string what, int from)
        {
            RokuClient ecp = Ecp();
            List<KeyValuePair<string, string>> inputs = ecp.GetTvInputs(AskMs);
            if (inputs == null)
            {
                Refused(what, ", but the TV won't list its inputs");
                return;
            }
            KeyValuePair<string, string> input = inputs.Find(p => string.Equals(p.Key, what, StringComparison.OrdinalIgnoreCase));
            if (input.Key == null)
            {
                Refused(what, what.StartsWith("tvinput.", StringComparison.OrdinalIgnoreCase) ? ", an input this TV doesn't have" : ", which isn't an input");
                return;
            }
            // still this connection, and still wanted (not unticked meanwhile, say)
            lock (listenLock) if (!listening || connection != from) return;
            int code = ecp.Launch(input.Key, AskMs);
            bool switched = code >= 200 && code < 300;
            lastRefused = null;
            Controller.Say("developer tools: the app asked for " + Clean(input.Value) + " (" + input.Key + "): " +
                (switched ? "switched" : code == 0 ? "no answer" : "the TV said " + code));
            if (switched || code == 0)
            {
                // Roku's home screen on the way there (a TV that didn't answer may still be on its
                // way) doesn't open the app again, and the next trip home isn't Home twice. The
                // next look comes soon, to see the input as soon as it's there.
                Controller.NoteInputSwitch();
                openedAt = DateTime.MinValue;
                lock (watchLock) if (inputsTimer != null) inputsTimer.Change(Math.Min(SwitchLookMs, InputsIdleMs), Timeout.Infinite);
            }
            // the app is off the screen now: its console is left alone until it's back
            if (switched) Gone();
        }

        // an ask that isn't carried out goes in the log, the same one only once in a row
        void Refused(string what, string why)
        {
            if (what == lastRefused) return;
            lastRefused = what;
            Controller.Say("developer tools: the app asked for " + Shown(what) + why);
        }

        // Tells the app on screen TVFollow is there, and which inputs the TV has: tvfollow=hello,
        // protocol=1, token (for its asks), pc (the input this PC is on), inputs (how many), then
        // input1, name1, input2, name2... (like tvinput.hdmi2 and the TV's name for it).
        // (Under inputsLock.)
        void Hello(RokuClient ecp)
        {
            string forToken;
            int forConnection;
            lock (listenLock)
            {
                if (!listening || !settled) return;
                forToken = token;
                forConnection = connection;
            }
            List<KeyValuePair<string, string>> inputs = ecp.GetTvInputs(AskMs);
            if (inputs == null)
            {
                if (!toldNoInputs)
                {
                    toldNoInputs = true;
                    Controller.Say("developer tools: the TV won't list its inputs (is Control by mobile apps on Limited?), so the app wasn't told any");
                }
                return;
            }
            toldNoInputs = false;
            // an input event goes to whatever is on screen, and the last look may be a few seconds old
            if (ecp.GetActiveApp(AskMs) != "dev") return;
            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("tvfollow", "hello"),
                new KeyValuePair<string, string>("protocol", "1"),
                new KeyValuePair<string, string>("token", forToken),
                new KeyValuePair<string, string>("pc", Tv.PcApp),
                new KeyValuePair<string, string>("inputs", inputs.Count.ToString(CultureInfo.InvariantCulture)),
            };
            for (int i = 0; i < inputs.Count; i++)
            {
                string n = (i + 1).ToString(CultureInfo.InvariantCulture);
                pairs.Add(new KeyValuePair<string, string>("input" + n, inputs[i].Key));
                pairs.Add(new KeyValuePair<string, string>("name" + n, inputs[i].Value));
            }
            int code;
            lock (listenLock)
            {
                // the connection the token belongs to is still the one being read
                if (!listening || connection != forConnection) return;
            }
            code = ecp.Input(pairs, AskMs);
            bool sent = code >= 200 && code < 300;
            bool tell;
            lock (listenLock)
            {
                if (connection == forConnection && sent) helloDue = false;
                if (sent) toldBusy = false;
                // the log mentions a connection's first hello, and every one that didn't get through
                tell = !sent || toldHello != forConnection;
                if (sent) toldHello = forConnection;
            }
            if (tell)
                Controller.Say("developer tools: told the app on the TV about " + inputs.Count + " inputs" +
                    (sent ? "" : code == 0 ? " (no answer, trying again)" : " (the TV said " + code + ", trying again)"));
        }

        // something the app wrote, for the log: one line in quotes, kept short
        static string Shown(string text)
        {
            return "\"" + Clean(text) + "\"";
        }

        // Text from the app or the TV, for the log: control characters, line and paragraph breaks
        // and invisible format characters (like the ones that turn text right to left) become
        // spaces, and it's kept to 60 characters.
        static string Clean(string text)
        {
            var line = new StringBuilder();
            foreach (char c in text)
            {
                if (line.Length >= 60) { line.Append("…"); break; }
                UnicodeCategory kind = char.GetUnicodeCategory(c);
                bool hidden = kind == UnicodeCategory.Control || kind == UnicodeCategory.Format ||
                    kind == UnicodeCategory.LineSeparator || kind == UnicodeCategory.ParagraphSeparator;
                line.Append(hidden ? ' ' : c);
            }
            return line.ToString();
        }
    }
}
