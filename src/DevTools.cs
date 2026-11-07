using System;
using System.Collections.Generic;
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

        public DevReport(bool ok, string text)
        {
            Ok = ok;
            Text = text ?? "";
        }
    }

    // The developer tools of every TV. Two jobs run in the background while TVFollow follows the
    // monitors: putting a pushed app back when it vanished from its TV, and pushing an app's folder
    // again when a file in it is saved. Neither runs while the tools are switched off in Settings.
    class DevTools : IDisposable
    {
        readonly Settings settings;
        public readonly List<DevTv> Tvs;

        // a message in the taskbar corner (the tray fills this in): title, text
        public Action<string, string> Notify = delegate { };

        public DevTools(Settings settings, List<Controller> controllers)
        {
            this.settings = settings;
            Tvs = controllers.ConvertAll(c => new DevTv(this, settings, c));
            foreach (DevTv tv in Tvs) tv.Controller.BeforeFollowOff = tv.BeforeOff;
            Refresh();
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

    // One TV's developer tools: pushing, removing and pictures, and the two background jobs.
    class DevTv
    {
        public static int AskMs = 3000;
        public static int PushMs = 60000;      // an upload over Wi-Fi, and the TV unpacking it
        public static int PutBackMs = 30000;   // putting an app back holds up turning the TV off
        public static int SettleMs = 1500;     // quiet time after the last change in a watched folder
        public static int RetryMs = 3000;      // looking again for a watched folder that went away

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
        string lastPushed;   // a fingerprint of the last zip the TV took, so a save that changed nothing isn't pushed

        // something happened in the background (a push on save, an app put back): the window shows it
        public event Action<DevReport> Background = delegate { };

        public DevTv(DevTools tools, Settings settings, Controller controller)
        {
            this.tools = tools;
            this.settings = settings;
            Controller = controller;
        }

        public TvSettings Tv { get { return Controller.Tv; } }

        public string Label { get { return Controller.Label; } }

        // the copy of the app pushed last, kept to put back
        public string KeptPath { get { return Path.Combine(Path.Combine(Settings.Dir, "dev"), FileName(Tv.Serial) + ".zip"); } }

        public bool HasKept { get { return File.Exists(KeptPath); } }

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
                return new DevReport(false, problem);
            }
            Keep(app.Zip);
            lastPushed = Fingerprint(app.Zip);
            toldDevModeOff = toldPutBackFailed = false;
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

        // Starts or stops watching the app's folder, as the settings now say. Only folders on
        // this PC's own drives: looking at a network path (\\server\...) would have Windows
        // connect to it, even for a folder only named in a settings file.
        public void Refresh()
        {
            lock (refreshing) RefreshNow();
        }

        void RefreshNow()
        {
            string folder = Tv.DevFolder;
            bool wanted = settings.DeveloperTools && Tv.DevOnSave && folder.Length > 0 && !folder.StartsWith("\\\\");
            bool there = wanted && Directory.Exists(folder);
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
            }
            StopWatching();
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
            // the folder is zipped in this TV's turn, so no push waiting behind it can send older files
            lock (turn)
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
            if (!r.Ok) tools.Notify("Couldn't push your app", r.Text);
            Background(r);
        }
    }
}
