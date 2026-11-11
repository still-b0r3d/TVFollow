using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow.Tests
{
    // The developer tools (1.3): developer mode, pushing apps and the rest, against a pretend TV
    // and its pretend installer. No real TV, and no real password.
    static partial class TestMain
    {
        static void RunDevTests()
        {
            Run("dev: the developer settings save and load, the password sealed", DevSettingsRoundTrip);
            Run("dev: device info says whether developer mode is on", DevDeviceInfo);
            Run("dev: a folder is zipped with the manifest at the top, junk left out", DevZipFolder);
            Run("dev: an app one folder down is found, in a folder or a zip", DevZipOneDown);
            Run("dev: the example app is built in, whole", DevExampleBuiltIn);
            Run("dev: the installer's messages are read, old and new", DevInstallerMessages);
            Run("dev: pushing an app, with the password", DevPushInstalls);
            Run("dev: pushing says what went wrong, and only this TV gets the password", DevPushProblems);
            Run("dev: screenshot, open and remove", DevScreenshotAndRemove);
            Run("dev: a vanished app goes back on before the TV turns off", DevPutBack);
            Run("dev: a saved file pushes the folder again, once", DevPushOnSave);
            Run("dev: the app log shows the TV's console", DevAppLog);
            Run("dev: the pushed app asks TVFollow to switch the TV's input", DevAppSwitchesInputs);
            Run("dev: nobody reads the app's console for asks unless switched on, and only on this TV", DevInputsOnlyWhenOn);
            Run("dev: the app opens when the TV goes to Roku's home screen, and Home twice stays home", DevHomeOpensApp);
            Run("dev: the app log is saved in a file while it's read", DevAppLogSaved);
            Run("dev: push and screenshot from the command line, with the saved password", DevCommandLine);
            Run("dev: the developer window shows the TV's state", DevWindow);
            Run("dev: Settings shows the developer tools only when switched on", DevSettingsSwitch);
            Run("dev: the tray menu has Developer tools… only when switched on, and it opens them", DevTrayItem);
            Run("dev: removing TVFollow's settings takes the pushed apps' copies too", DevUninstallRemovesCopies);
            DevClient.InstallerPort = 80;   // as it was, for whatever runs after
        }

        // a pretend TV in developer mode with its installer, and settings that know the password.
        // No copies kept from earlier tests (the pretend TVs share a serial).
        static Settings DevSettings(FakeTv tv, FakeInstaller installer)
        {
            string kept = Path.Combine(Settings.Dir, "dev");
            if (Directory.Exists(kept)) Directory.Delete(kept, true);
            Settings s = SettingsFor(tv);
            s.DeveloperTools = true;
            Tv(s).DevPassword = installer.Password;
            DevClient.InstallerPort = installer.Port;
            return s;
        }

        static DevTools DevToolsFor(Settings s)
        {
            return new DevTools(s, new List<Controller> { Fast(FirstTv(s)) });
        }

        static string TempFolder(string name)
        {
            string dir = Path.Combine(Path.Combine(Path.GetTempPath(), "TVFollowDev-" + Guid.NewGuid().ToString("N")), name);
            Directory.CreateDirectory(dir);
            return dir;
        }

        static void WriteFile(string root, string relative, string text)
        {
            string path = Path.Combine(root, relative.Replace('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        static List<string> Entries(byte[] zip)
        {
            var names = new List<string>();
            using (var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
                foreach (ZipArchiveEntry e in archive.Entries) names.Add(e.FullName);
            names.Sort(string.CompareOrdinal);
            return names;
        }

        static void AddEntry(ZipArchive archive, string name, string text)
        {
            using (var w = new StreamWriter(archive.CreateEntry(name).Open())) w.Write(text);
        }

        // The example app's name and version as its manifest gives them, read here the plain way,
        // apart from DevPackage. The example is b0r3d TV, so they change with each new version of it.
        internal static string ExampleTitle { get { return ExampleManifest("title"); } }

        internal static string ExampleVersion
        {
            get
            {
                string minor = ExampleManifest("minor_version"), build = ExampleManifest("build_version");
                return ExampleManifest("major_version") + "." + (minor.Length > 0 ? minor : "0") + (build.Length > 0 ? "." + build : "");
            }
        }

        // as TVFollow shows it: the name, then the version
        static string ExampleShown { get { return ExampleTitle + " " + ExampleVersion; } }

        static string ExampleManifest(string key)
        {
            using (Stream s = typeof(DevPackage).Assembly.GetManifestResourceStream(DevPackage.ExamplePrefix + "manifest"))
            {
                if (s == null) return "";
                using (var reader = new StreamReader(s))
                {
                    Match m = Regex.Match(reader.ReadToEnd(), "^" + key + "=(.*)$", RegexOptions.Multiline);
                    return m.Success ? m.Groups[1].Value.Trim() : "";
                }
            }
        }

        static void CheckThrows(Action action, string words)
        {
            try { action(); }
            catch (DevPackageException e)
            {
                Check(e.Message.IndexOf(words, StringComparison.OrdinalIgnoreCase) >= 0, "the message doesn't say '" + words + "': " + e.Message);
                return;
            }
            throw new Exception("no error for: " + words);
        }

        static void WaitFor(Func<bool> done, int seconds, string what)
        {
            DateTime until = DateTime.UtcNow.AddSeconds(seconds);
            while (!done())
            {
                if (DateTime.UtcNow > until) throw new Exception(what);
                Thread.Sleep(50);
            }
        }

        static Button FindButton(Control root, string text)
        {
            return FindControl<Button>(root, b => b.Text == text);
        }

        // a port nothing listens on
        static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        // ---- settings ----

        static void DevSettingsRoundTrip()
        {
            const string password = "test-pass-123";
            var s = new Settings();
            Tv(s).Ip = "192.168.1.50";
            Tv(s).Serial = "X000000001";
            Tv(s).DevPassword = password;
            Tv(s).DevFolder = "C:\\Apps\\Den #2 app";
            Tv(s).DevOnSave = true;
            Tv(s).DevInputs = true;
            s.Tvs.Add(new TvSettings { Ip = "192.168.1.51", Serial = "X000000002", DevPushBack = false, DevHome = true });
            s.DeveloperTools = true;
            s.Save();
            string file = File.ReadAllText(Settings.FilePath);
            Check(!file.Contains(password), "the password is in settings.ini as typed");
            Check(file.Contains("dev_password=dpapi:"), "the password isn't sealed: " + file);
            Check(file.Contains("\ndev_inputs=yes") && !file.Contains("tv2.dev_inputs"), "the inputs option's lines: " + file);
            Check(file.Contains("\ntv2.dev_home=yes") && !file.Contains("\ndev_home"), "the home screen option's lines: " + file);
            Settings t = Settings.Load();
            Check(t.DeveloperTools, "the switch was lost");
            Check(Tv(t).DevPassword == password, "the password didn't come back");
            Check(Tv(t).DevFolder == "C:\\Apps\\Den #2 app", "the folder lost its #: " + Tv(t).DevFolder);
            Check(Tv(t).DevOnSave && Tv(t).DevPushBack && Tv(t).DevInputs && !Tv(t).DevHome, "the first TV's options were lost");
            Check(!t.Tvs[1].DevPushBack && !t.Tvs[1].DevOnSave && !t.Tvs[1].DevInputs && t.Tvs[1].DevHome && t.Tvs[1].DevPassword == "", "the second TV's options were lost");

            // a TV that never used the tools gets no lines for them
            var plain = new Settings();
            Tv(plain).Ip = "192.168.1.50";
            Tv(plain).Serial = "X1";
            plain.Save();
            Check(!File.ReadAllText(Settings.FilePath).Contains("dev"), "unused developer settings were written");

            // a password sealed on another PC or account reads as none, quietly
            File.WriteAllText(Settings.FilePath, "ip=192.168.1.50\r\nserial=X1\r\ndev_password=dpapi:AAAA\r\n");
            Check(Tv(Settings.Load()).DevPassword == "", "a password sealed elsewhere wasn't read as none");
            File.Delete(Settings.FilePath);
        }

        static void DevDeviceInfo()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                var c = new RokuClient(tv.Ip, tv.Port);
                Check(!c.GetDeviceInfo(2000).DeveloperEnabled, "developer mode reads as on");
                tv.DeveloperEnabled = true;
                Check(c.GetDeviceInfo(2000).DeveloperEnabled, "developer mode reads as off");
                bool answered;
                Check(c.GetDevApp(2000, out answered) == null && answered, "an app was listed before any push");
                tv.DevAppName = "My app";
                tv.DevAppVersion = "2.1.7";
                DevApp app = c.GetDevApp(2000, out answered);
                Check(app != null && app.Name == "My app" && app.Version == "2.1.7" && app.ToString() == "My app 2.1.7", "the pushed app wasn't read");
                tv.ControlMode = "limited";
                Check(c.GetDevApp(2000, out answered) == null && !answered, "a hidden list wasn't told apart from no app");
            }
        }

        // ---- making the zip ----

        static void DevZipFolder()
        {
            string app = TempFolder("My app");
            try
            {
                WriteFile(app, "manifest", "# a test app\ntitle=Test App\nmajor_version=2\nminor_version=1\nbuild_version=7\n");
                WriteFile(app, "source/main.brs", "sub Main()\nend sub\n");
                WriteFile(app, "components/Scene.xml", "<component name=\"Scene\" extends=\"Scene\" />");
                WriteFile(app, ".git/config", "[core]");
                WriteFile(app, ".vscode/settings.json", "{}");
                WriteFile(app, "Thumbs.db", "x");
                WriteFile(app, "source/main.brs~", "old");
                WriteFile(app, "source/notes.tmp", "temp");
                DevPackage p = DevPackage.FromFolder(app);
                string names = string.Join(", ", Entries(p.Zip));
                Check(names == "components/Scene.xml, manifest, source/main.brs", "the zip has: " + names);
                Check(p.Title == "Test App" && p.Version == "2.1.7" && p.ToString() == "Test App 2.1.7", "the manifest read as " + p);
                Check(p.Root == Path.GetFullPath(app).TrimEnd('\\') && p.Note == "", "root " + p.Root + ", note " + p.Note);
                Check(Convert.ToBase64String(DevPackage.FromFolder(app).Zip) == Convert.ToBase64String(p.Zip), "the same files made a different zip");
                Check(DevPackage.Skipped("source\\x.swp") && DevPackage.Skipped("__MACOSX/a") && !DevPackage.Skipped("images/a.png"), "what's left out");

                // a manifest saved with a byte-order mark (Notepad does that) still has its title read
                File.WriteAllText(Path.Combine(app, "manifest"), "title=Marked\nmajor_version=1\nminor_version=2\n", new System.Text.UTF8Encoding(true));
                p = DevPackage.FromFolder(app);
                Check(p.Title == "Marked" && p.Version == "1.2", "with a byte-order mark the manifest read as " + p);
            }
            finally { Directory.Delete(Path.GetDirectoryName(app), true); }
        }

        static void DevZipOneDown()
        {
            string parent = TempFolder("Projects");
            try
            {
                WriteFile(parent, "myapp/manifest", "title=Inner\nmajor_version=1\nminor_version=0\n");
                WriteFile(parent, "myapp/source/main.brs", "sub Main()\nend sub\n");
                WriteFile(parent, "readme.txt", "notes");
                DevPackage p = DevPackage.FromFolder(parent);
                Check(p.Title == "Inner" && p.Version == "1.0" && p.Note.Contains("myapp") && p.Root.EndsWith("myapp"), "one down: " + p + " / " + p.Note + " / " + p.Root);
                Check(string.Join(",", Entries(p.Zip)) == "manifest,source/main.brs", "one down, the zip has: " + string.Join(",", Entries(p.Zip)));
                CheckThrows(() => DevPackage.FromFolder(Path.Combine(Path.Combine(parent, "myapp"), "source")), "no manifest");
                WriteFile(parent, "other/manifest", "title=Other\n");
                CheckThrows(() => DevPackage.FromFolder(parent), "more than one app");

                // a zip with the app in a folder inside it, Windows-style backslashes and a sneaky path
                string zipPath = Path.Combine(parent, "app.zip");
                using (FileStream fs = File.Create(zipPath))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    AddEntry(archive, "myapp\\manifest", "title=Zipped\nmajor_version=3\nminor_version=2\nbuild_version=1\n");
                    AddEntry(archive, "myapp\\source\\main.brs", "sub Main()\nend sub\n");
                    AddEntry(archive, "myapp/../../evil.txt", "nope");
                    AddEntry(archive, "__MACOSX/myapp/._manifest", "junk");
                }
                DevPackage z = DevPackage.FromZip(zipPath);
                Check(z.Title == "Zipped" && z.Version == "3.2.1" && z.Note.Contains("myapp"), "zip one down: " + z + " / " + z.Note);
                Check(string.Join(",", Entries(z.Zip)) == "manifest,source/main.brs", "zip, the new zip has: " + string.Join(",", Entries(z.Zip)));
                File.WriteAllText(zipPath, "not a zip");
                CheckThrows(() => DevPackage.FromZip(zipPath), "isn't a zip");

                // what entries really unpack to counts toward the limit, not the sizes they claim
                long maxBefore = DevPackage.MaxBytes;
                DevPackage.MaxBytes = 2500;
                try
                {
                    File.Delete(zipPath);
                    using (FileStream fs = File.Create(zipPath))
                    using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
                    {
                        AddEntry(archive, "manifest", "title=Big\n" + new string('#', 1000));
                        AddEntry(archive, "source/a.brs", new string('a', 1000));
                        AddEntry(archive, "source/b.brs", new string('b', 1000));
                    }
                    CheckThrows(() => DevPackage.FromZip(zipPath), "far too big");
                    Directory.Delete(Path.Combine(parent, "other"), true);
                    WriteFile(parent, "myapp/source/c.brs", new string('c', 3000));
                    CheckThrows(() => DevPackage.FromFolder(parent), "far too big");
                }
                finally { DevPackage.MaxBytes = maxBefore; }
            }
            finally { Directory.Delete(Path.GetDirectoryName(parent), true); }
        }

        static void DevExampleBuiltIn()
        {
            DevPackage p = DevPackage.Example();
            // the name and version its manifest gives (and a version that's numbers)
            Check(ExampleTitle.Length > 0 && Regex.IsMatch(ExampleVersion, "^[0-9]+[.][0-9]+([.][0-9]+)?$") &&
                p.Title == ExampleTitle && p.Version == ExampleVersion, "the example is " + p + ", its manifest says " + ExampleShown);
            List<string> names = Entries(p.Zip);
            // the files a Roku app starts from, and b0r3d TV's licenses and build stamp beside them
            foreach (string need in new[] { "manifest", "source/main.brs", "components/AsideScene.xml", "components/AsideScene.brs",
                "images/icon_focus_fhd.png", "images/icon_focus_hd.png", "images/splash_fhd.png", "images/splash_hd.png",
                "LICENSE", "LICENSE-ASSETS", "build.txt" })
                Check(names.Contains(need), "the example has no " + need);
            using (var archive = new ZipArchive(new MemoryStream(p.Zip), ZipArchiveMode.Read))
            {
                // every file the manifest and the code point at is in it: each pkg:/ path in the
                // manifest and in every .brs and .xml file, and for a path the code finishes itself
                // ("pkg:/sounds/" + name + ".wav"), a file it starts
                foreach (string from in names)
                {
                    if (from != "manifest" && !from.EndsWith(".brs", StringComparison.Ordinal) && !from.EndsWith(".xml", StringComparison.Ordinal)) continue;
                    string text;
                    using (var reader = new StreamReader(archive.GetEntry(from).Open())) text = reader.ReadToEnd();
                    foreach (Match m in Regex.Matches(text, "pkg:/([\\w./-]+)(\"\\s*\\+)?"))
                    {
                        string path = m.Groups[1].Value;
                        bool started = m.Groups[2].Success;
                        Check(started ? names.Exists(n => n.StartsWith(path, StringComparison.Ordinal)) : archive.GetEntry(path) != null,
                            from + " points at a missing " + path + (started ? "..." : ""));
                    }
                }
                // the pictures are the sizes Roku asks for
                CheckPicture(archive, "images/icon_focus_fhd.png", 540, 405);
                CheckPicture(archive, "images/icon_focus_hd.png", 290, 218);
                CheckPicture(archive, "images/splash_fhd.png", 1920, 1080);
                CheckPicture(archive, "images/splash_hd.png", 1280, 720);
            }
            // build.cmd builds in every file of the example's folder (when the tests run in the repo)
            string folder = Path.Combine("examples", "tvfollow-example");
            if (Directory.Exists(folder))
            {
                string built = string.Join(",", names), there = string.Join(",", Entries(DevPackage.FromFolder(folder).Zip));
                Check(built == there, "build.cmd builds in " + built + ", the folder has " + there);
            }
            Check(Convert.ToBase64String(DevPackage.Example().Zip) == Convert.ToBase64String(p.Zip), "the example's zip changes each time");
        }

        static void CheckPicture(ZipArchive archive, string name, int width, int height)
        {
            using (Stream s = archive.GetEntry(name).Open())
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                ms.Position = 0;
                using (Image picture = Image.FromStream(ms))
                    Check(picture.Width == width && picture.Height == height, name + " is " + picture.Width + " x " + picture.Height);
            }
        }

        static void DevInstallerMessages()
        {
            DevResult r = DevClient.Judge("<script>Shell.create('Roku.Message').trigger('Set message type', 'success').trigger('Set message content', 'Install Success.').trigger('Render', node);</script>");
            Check(r.Outcome == DevOutcome.Done && r.Message == "Install Success.", "older success: " + r.Outcome + " " + r.Message);
            r = DevClient.Judge("Shell.create('Roku.Message').trigger('Set message type', 'error').trigger('Set message content', 'Install Failure: Compilation Failed.')");
            Check(r.Outcome == DevOutcome.Refused && r.Message == "Install Failure: Compilation Failed." && r.Problem.Contains("App log"), "older error: " + r.Message + " / " + r.Problem);
            r = DevClient.Judge("var params = JSON.parse('{\"messages\":[{\"text\":\"Install Success.\",\"text_type\":\"text\",\"type\":\"success\"}],\"metadata\":{}}');");
            Check(r.Outcome == DevOutcome.Done && r.Message == "Install Success.", "newer success: " + r.Outcome + " " + r.Message);
            r = DevClient.Judge("JSON.parse('{\"messages\":[{\"text\":\"Can\\'t do \\\\\"that\\\\\"\",\"text_type\":\"text\",\"type\":\"error\"}]}');");
            Check(r.Outcome == DevOutcome.Refused && r.Message == "Can't do \"that\"", "newer error with quotes: " + r.Message);
            r = DevClient.Judge("Shell.create('Roku.Message').trigger('Set message type', 'info').trigger('Set message content', 'Identical to previous version -- not replacing.')");
            Check(r.Outcome == DevOutcome.Same, "identical: " + r.Outcome);
            r = DevClient.Judge("<font color=\"red\">Install Failure: No manifest. Invalid package.</font>");
            Check(r.Outcome == DevOutcome.Refused && r.Message.StartsWith("Install Failure: No manifest"), "plain older failure: " + r.Message);
            Check(DevClient.Judge("<html>something else</html>").Outcome == DevOutcome.Done, "a page with nothing to say");
            r = DevClient.Judge("Shell.create('Roku.Message').trigger('Set message type', 'error').trigger('Set message content', 'Failed to check for software update')");
            Check(r.Outcome == DevOutcome.UpdateCheck && r.Problem.Contains("System update"), "update check: " + r.Outcome);
            // the TV's words go in the log and the window: one line, not too long
            r = DevClient.Judge("Shell.create('Roku.Message').trigger('Set message type', 'error').trigger('Set message content', 'Bad\\nfake log line " + new string('x', 1000) + "')");
            Check(r.Outcome == DevOutcome.Refused && r.Message.IndexOf('\n') < 0 && r.Message.StartsWith("Bad fake log line") && r.Message.Length <= DevResult.MaxWords + 1,
                "a long message with a line break: " + r.Message.Length + " characters");
        }

        // ---- talking to the TV ----

        static void DevPushInstalls()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            using (var installer = new FakeInstaller(tv, 0))
            {
                tv.DeveloperEnabled = true;
                Settings s = DevSettings(tv, installer);
                DevTools tools = DevToolsFor(s);
                DevTv dev = tools.Tvs[0];
                DevReport r = dev.Push(DevPackage.Example(), "pushed");
                Check(r.Ok && r.Text.StartsWith("Pushed " + ExampleShown), "push: " + r.Text);
                Check(tv.DevAppName == ExampleTitle && tv.DevAppVersion == ExampleVersion && tv.App == "dev", "the TV didn't get the app");
                Check(installer.Installs == 1 && !installer.SawBasic, "installs " + installer.Installs + ", the password offered as it is: " + installer.SawBasic);
                Check(dev.HasKept, "no copy kept to put back");
                DevStatus st = dev.Status(2000);
                Check(st.Answering && st.DevMode && st.AppKnown && st.App != null && st.App.ToString() == ExampleShown, "the state after the push");

                // the same files again: the TV keeps them, and TVFollow opens the app
                tv.App = "tvinput.hdmi1";
                r = dev.Push(DevPackage.Example(), "pushed");
                Check(r.Ok && r.Text.Contains("already had these very files") && installer.Installs == 1 && tv.Count("POST /launch/dev") == 1 && tv.App == "dev",
                    "the same files again: " + r.Text);
                string log = LogText();
                Check(log.Contains("developer tools: pushed " + ExampleShown), "the push isn't in the log: " + log);
                Check(!log.Contains(installer.Password), "the password got into the log");
                tools.Dispose();
            }
        }

        static void DevPushProblems()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            using (var installer = new FakeInstaller(tv, 0))
            {
                tv.DeveloperEnabled = true;
                Settings s = DevSettings(tv, installer);
                DevTools tools = DevToolsFor(s);
                DevTv dev = tools.Tvs[0];
                DevPackage app = DevPackage.Example();

                Tv(s).DevPassword = "wrong-pass";
                DevReport r = dev.Push(app, "pushed");
                Check(!r.Ok && r.Text.Contains("password is wrong") && installer.Installs == 0, "wrong password: " + r.Text);
                Check(dev.CheckPassword().Text.Contains("password is wrong"), "the check took a wrong password");
                Tv(s).DevPassword = "";
                Check(dev.Push(app, "pushed").Text.StartsWith("Type the password"), "no password");
                Tv(s).DevPassword = installer.Password;
                Check(dev.CheckPassword().Ok, "the check refused the right password");

                // the app's own code: no second try, and the app log is the place to look
                installer.Refuse = "Install Failure: Compilation Failed.";
                installer.JsonMessages = true;
                int submits = installer.Submits;
                r = dev.Push(app, "pushed");
                Check(!r.Ok && r.Text.Contains("Compilation Failed") && r.Text.Contains("App log") && !dev.HasKept && installer.Submits == submits + 1,
                    "refused: " + r.Text + " (" + (installer.Submits - submits) + " tries)");
                installer.Refuse = null;

                // Replace turned away for another reason: once more as Install, like Roku's tools
                installer.RefuseReplace = true;
                r = dev.Push(app, "pushed");
                Check(r.Ok && tv.Requests().Contains("INSTALLER Install") && tv.DevAppName == ExampleTitle, "Install after a refused Replace: " + r.Text);
                installer.RefuseReplace = false;

                // a TV that wants an update check first, said either way
                installer.UpdateCheckPage = true;
                r = dev.Push(app, "pushed");
                Check(!r.Ok && r.Text.Contains("System update"), "update check page: " + r.Text);
                installer.UpdateCheckPage = false;
                installer.ForceStatus = 577;
                r = dev.Push(app, "pushed");
                Check(!r.Ok && r.Text.Contains("System update"), "577: " + r.Text);
                // only a plain 200 is the installer's page: a redirect isn't "pushed"
                installer.ForceStatus = 302;
                r = dev.Push(app, "pushed");
                Check(!r.Ok && r.Text.Contains("doesn't know"), "a redirect counted: " + r.Text);
                Check(!dev.CheckPassword().Ok, "a redirect passed the password check");
                installer.ForceStatus = 0;

                // offered Basic beside digest, the password still never goes as it is
                installer.OfferBasic = true;
                Check(dev.Push(app, "pushed").Ok && dev.CheckPassword().Ok && !installer.SawBasic, "with Basic offered too, Basic seen: " + installer.SawBasic);
                installer.OfferBasic = false;

                tv.DeveloperEnabled = false;
                int hits = installer.Hits;
                r = dev.Push(app, "pushed");
                Check(!r.Ok && r.Text.Contains("Developer mode is off") && installer.Hits == hits, "developer mode off: " + r.Text);
                tv.DeveloperEnabled = true;

                // something else at the TV's address never even gets asked for the password
                tv.Serial = "SOMEONE-ELSE";
                hits = installer.Hits;
                r = dev.Push(app, "pushed");
                Check(!r.Ok && r.Text.Contains("Another Roku") && installer.Hits == hits, "another Roku: " + r.Text);
                tv.Serial = "X000000001";

                // developer mode on, but nothing answers on the installer's port
                DevClient.InstallerPort = FreePort();
                r = dev.Push(app, "pushed");
                Check(!r.Ok && r.Text.Contains("app installer didn't answer"), "no installer: " + r.Text);
                Check(!LogText().Contains(installer.Password), "the password got into the log");
                tools.Dispose();
            }
        }

        static void DevScreenshotAndRemove()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            using (var installer = new FakeInstaller(tv, 0))
            {
                tv.DeveloperEnabled = true;
                Settings s = DevSettings(tv, installer);
                DevTools tools = DevToolsFor(s);
                DevTv dev = tools.Tvs[0];
                byte[] picture;
                string extension;
                DevReport r = dev.Screenshot(out picture, out extension);
                Check(!r.Ok && picture == null && r.Text.Length > 0, "a picture with no app open");
                Check(dev.Push(DevPackage.Example(), "pushed").Ok, "push");
                r = dev.Screenshot(out picture, out extension);
                Check(r.Ok && extension == "jpg" && picture != null && picture.Length > 100 && picture[0] == 0xFF && picture[1] == 0xD8,
                    "screenshot: " + r.Text + " / " + extension);
                tv.App = "tvinput.hdmi1";
                Check(dev.Open().Ok && tv.App == "dev", "open");
                r = dev.Remove();
                Check(r.Ok && tv.DevAppName == null && !dev.HasKept, "remove: " + r.Text);
                tools.Dispose();
            }
        }

        static void DevPutBack()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            using (var installer = new FakeInstaller(tv, 0))
            {
                tv.DeveloperEnabled = true;
                Settings s = DevSettings(tv, installer);
                Controller c = Fast(FirstTv(s));
                var tools = new DevTools(s, new List<Controller> { c });
                var notes = new List<string>();
                tools.Notify = (title, text) => { lock (notes) notes.Add(title); };
                DevTv dev = tools.Tvs[0];
                Check(dev.Push(DevPackage.Example(), "pushed").Ok, "push");

                // a Roku update took it; the monitors go off with the TV showing the PC
                tv.DevAppName = null;
                tv.App = "tvinput.hdmi1";
                c.TurnOff(false, false);
                Check(tv.DevAppName == ExampleTitle && installer.Installs == 2, "not put back");
                List<string> order = tv.Requests();
                int put = order.LastIndexOf("INSTALLER Replace"), off = order.IndexOf("POST /keypress/PowerOff");
                Check(put >= 0 && off > put && !tv.IsOn, "put back at " + put + ", power off at " + off);
                Check(notes.Contains("App put back"), "no message that it's back: " + string.Join(" | ", notes));

                // still there: nothing pushed
                tv.PowerMode = "PowerOn";
                tv.App = "tvinput.hdmi1";
                c.TurnOff(false, false);
                Check(installer.Submits == 2, "pushed though it was there");

                // a button's off isn't the monitors going off
                tv.DevAppName = null;
                tv.PowerMode = "PowerOn";
                tv.App = "tvinput.hdmi1";
                c.TurnOff(true, false);
                Check(installer.Submits == 2, "a button's off pushed");

                // the tools switched off, or the option off: nothing
                s.DeveloperTools = false;
                tv.PowerMode = "PowerOn";
                tv.App = "tvinput.hdmi1";
                c.TurnOff(false, false);
                Check(installer.Submits == 2, "pushed with the tools off");
                s.DeveloperTools = true;
                Tv(s).DevPushBack = false;
                tv.PowerMode = "PowerOn";
                tv.App = "tvinput.hdmi1";
                c.TurnOff(false, false);
                Check(installer.Submits == 2, "pushed with the option off");
                Tv(s).DevPushBack = true;

                // sleep and shutdown give only a moment: never a push then
                tv.PowerMode = "PowerOn";
                tv.App = "tvinput.hdmi1";
                c.TurnOff(false, true);
                Check(installer.Submits == 2 && !tv.IsOn, "sleep pushed, or didn't turn the TV off");

                // Network access on Limited hides the TV's list of apps: nothing is pushed blind
                tv.ControlMode = "limited";
                tv.PowerMode = "PowerOn";
                tv.App = "tvinput.hdmi1";
                c.TurnOff(false, false);
                Check(installer.Submits == 2, "pushed with the app list hidden");
                tv.ControlMode = "enabled";

                // developer mode went off: said once, nothing pushed, and the TV still turns off
                tv.DeveloperEnabled = false;
                for (int i = 0; i < 2; i++)
                {
                    tv.PowerMode = "PowerOn";
                    tv.App = "tvinput.hdmi1";
                    c.TurnOff(false, false);
                }
                Check(installer.Submits == 2 && notes.FindAll(n => n.StartsWith("Developer mode is off")).Count == 1, "developer mode off: " + string.Join(" | ", notes));
                Check(!tv.IsOn, "the TV stayed on");
                tv.DeveloperEnabled = true;

                // on again with a new password: the put-back fails, says so once, and the TV still turns off
                Tv(s).DevPassword = "an-old-pass";
                for (int i = 0; i < 2; i++)
                {
                    tv.PowerMode = "PowerOn";
                    tv.App = "tvinput.hdmi1";
                    c.TurnOff(false, false);
                    Check(!tv.IsOn, "the TV stayed on after a failed put-back");
                }
                Check(notes.FindAll(n => n.StartsWith("Couldn't put your app back")).Count == 1 && notes.FindAll(n => n == "App put back").Count == 1,
                    "a failed put-back: " + string.Join(" | ", notes));
                Tv(s).DevPassword = installer.Password;

                // back at the PC while the app went back on: the TV stays on, and on the PC
                c.OffStillWanted = () => false;
                tv.PowerMode = "PowerOn";
                tv.App = "tvinput.hdmi1";
                c.TurnOff(false, false);
                Check(tv.DevAppName == ExampleTitle && tv.IsOn && tv.App == "tvinput.hdmi1" && tv.Count("POST /launch/tvinput.hdmi1") == 1,
                    "back during the put-back: on " + tv.IsOn + ", showing " + tv.App);
                tools.Dispose();
            }
        }

        static void DevPushOnSave()
        {
            string app = TempFolder("Saved app");
            int settleBefore = DevTv.SettleMs, retryBefore = DevTv.RetryMs;
            DevTv.SettleMs = 500;
            DevTv.RetryMs = 300;
            int quiet = DevTv.SettleMs * 3;   // long enough for a push that shouldn't happen to have happened
            DevTools tools = null;
            try
            {
                WriteFile(app, "manifest", "title=Saved\nmajor_version=1\nminor_version=0\nbuild_version=1\n");
                WriteFile(app, "source/main.brs", "sub Main()\nend sub\n");
                using (var tv = new FakeTv("127.0.0.1", 0))
                using (var installer = new FakeInstaller(tv, 0))
                {
                    tv.DeveloperEnabled = true;
                    Settings s = DevSettings(tv, installer);
                    Tv(s).DevFolder = app;
                    Tv(s).DevOnSave = true;
                    tools = DevToolsFor(s);
                    DevTv dev = tools.Tvs[0];
                    var reports = new List<DevReport>();
                    dev.Background += r => { lock (reports) reports.Add(r); };
                    Check(dev.Watching, "not watching the folder");

                    WriteFile(app, "source/main.brs", "sub Main()\n    print \"changed\"\nend sub\n");
                    WaitFor(() => installer.Installs == 1, 15, "the save wasn't pushed");
                    Check(tv.DevAppName == "Saved", "the TV got " + tv.DevAppName);
                    // an editor's backup file isn't a save of the app
                    WriteFile(app, "source/main.brs~", "backup");
                    Thread.Sleep(quiet);
                    Check(installer.Submits == 1, "a backup file was pushed");
                    // a burst of saves goes in one push, with every file in it
                    WriteFile(app, "source/main.brs", "sub Main()\n    print \"one\"\nend sub\n");
                    WriteFile(app, "components/a.xml", "<component name=\"a\" />");
                    WaitFor(() => installer.Installs == 2, 15, "the second save wasn't pushed");
                    Thread.Sleep(quiet);
                    Check(installer.Submits == 2 && Entries(installer.Last).Contains("components/a.xml"),
                        "one burst of saves pushed " + installer.Submits + " times");
                    lock (reports) Check(reports.Count == 2 && reports.TrueForAll(r => r.Ok), reports.Count + " reports");

                    // a build tool clears the folder and makes it again: it's watched again, and pushed
                    Directory.Delete(app, true);
                    Thread.Sleep(DevTv.RetryMs * 2);
                    WriteFile(app, "manifest", "title=Saved again\nmajor_version=1\nminor_version=0\nbuild_version=2\n");
                    WriteFile(app, "source/main.brs", "sub Main()\nend sub\n");
                    WaitFor(() => tv.DevAppName == "Saved again", 20, "the folder made again wasn't pushed");
                    Check(dev.Watching, "not watching the folder made again");

                    s.DeveloperTools = false;
                    tools.Refresh();
                    Check(!dev.Watching, "still watching with the tools off");
                }
            }
            finally
            {
                if (tools != null) tools.Dispose();
                DevTv.SettleMs = settleBefore;
                DevTv.RetryMs = retryBefore;
                Directory.Delete(Path.GetDirectoryName(app), true);
            }
        }

        // ---- the windows ----

        static void DevAppLog()
        {
            using (var console = new FakeConsole("127.0.0.1", 0))
            {
                int before = DevClient.ConsolePort;
                DevClient.ConsolePort = console.Port;
                try
                {
                    var shared = new DevConsole(() => "127.0.0.1");
                    using (var form = new DevLogForm(shared, "Test TV"))
                    {
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-20000, -20000);
                        form.ShowInTaskbar = false;
                        form.Show();
                        Pump(() => form.LogText.Contains("Welcome to the pretend app console.\r\n"), 10);
                        // bytes the text can't hold would show as the replacement character
                        Check(form.LogText.IndexOf((char)0xFFFD) < 0 && form.LogText.IndexOf((char)1) < 0 && form.LogText.IndexOf((char)24) < 0,
                            "the telnet commands showed up as text");
                        Check(form.LogText.StartsWith("Welcome"), "something came before the console's first words: " + form.LogText.Length);
                        Pump(() => console.Connections == 1, 5);
                        console.Say("TVFollow example: you pressed OK");
                        Pump(() => form.LogText.Contains("TVFollow example: you pressed OK\r\n"), 10);
                        Check(form.Text == "App log · Test TV", "the title is " + form.Text);

                        // another reader (the app's asks for inputs) shares the one connection, and
                        // starts with the recent text
                        var other = new ConsoleListener();
                        bool told = false;
                        string got = "";
                        other.Connected = () => told = true;
                        other.Text = chunk => got += chunk;
                        shared.Listen(other);
                        Check(told && got.Contains("Welcome to the pretend app console.") && got.Contains("you pressed OK"), "a second reader got: " + got);
                        console.Say("for both");
                        Pump(() => got.Contains("for both") && form.LogText.Contains("for both"), 10);
                        Check(console.Accepted == 1, "a second connection to the console: " + console.Accepted);
                        shared.Unlisten(other);
                        Check(shared.Connected, "one reader left, and the console closed");
                        form.Close();
                    }
                    // nobody reads it any more: the connection closes
                    Pump(() => !shared.Connected, 10);
                }
                finally { DevClient.ConsolePort = before; }
            }
        }

        static void DevWindow()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            using (var installer = new FakeInstaller(tv, 0))
            {
                tv.DeveloperEnabled = true;
                tv.DevAppName = ExampleTitle;
                tv.DevAppVersion = ExampleVersion;
                Settings s = DevSettings(tv, installer);
                DevTools tools = DevToolsFor(s);
                var follower = new Follower(s, tools.Tvs.ConvertAll(t => t.Controller)); // not started
                int pollBefore = DevPages.PollMs, settleBefore = DevTv.SettleMs;
                DevPages.PollMs = 200;
                DevTv.SettleMs = 300;
                string parent = TempFolder("Window apps");
                try
                {
                    using (var tray = new Tray(follower))
                    {
                        tray.Dev = tools;
                        using (var window = new SettingsForm(s, follower, tray))
                        {
                            OffScreen(window);
                            window.Show();
                            // switched on: the two app pages are listed
                            Check(window.ListedPages.Contains(SettingsForm.AppPage) && window.ListedPages.Contains(SettingsForm.AppOptionsPage),
                                "the app pages aren't listed: " + string.Join(", ", window.ListedPages.ToArray()));
                            window.ShowPage(SettingsForm.DeveloperPage);
                            Pump(() => AllText(window).Contains("Developer mode is on."), 10);
                            TextBox box = FindControl<TextBox>(window, b => b.UseSystemPasswordChar);
                            Check(box != null && box.Text == installer.Password, "the password box doesn't have the saved password");
                            box.Text = "another-pass";
                            Check(Tv(s).DevPassword == "another-pass" && Tv(Settings.Load()).DevPassword == "another-pass", "a typed password wasn't saved");
                            box.Text = installer.Password;

                            window.ShowPage(SettingsForm.AppPage);
                            Pump(() => AllText(window).Contains("On the TV now: " + ExampleShown), 10);
                            Check(FindButton(window, "Push the example app").Enabled && FindButton(window, "Remove").Enabled, "the buttons are off with developer mode on");

                            // letting the app switch inputs: ticked, saved, looked for; unticked, stopped
                            window.ShowPage(SettingsForm.AppOptionsPage);
                            CheckBox inputsBox = FindBox(window, "Let the app switch the TV's inputs");
                            Check(inputsBox != null && !inputsBox.Checked && !tools.Tvs[0].LookingForAsks, "no unticked inputs option");
                            inputsBox.Checked = true;
                            Check(Tv(s).DevInputs && Tv(Settings.Load()).DevInputs && tools.Tvs[0].LookingForAsks, "ticking the inputs option");
                            inputsBox.Checked = false;
                            Check(!Tv(s).DevInputs && !Tv(Settings.Load()).DevInputs && !tools.Tvs[0].LookingForAsks, "unticking the inputs option");
                            // opening the app when the TV goes home: the same
                            CheckBox homeBox = FindBox(window, "Open the app whenever the TV goes to Roku's home screen");
                            Check(homeBox != null && !homeBox.Checked, "no unticked home screen option");
                            homeBox.Checked = true;
                            Check(Tv(s).DevHome && Tv(Settings.Load()).DevHome && tools.Tvs[0].LookingForAsks, "ticking the home screen option");
                            homeBox.Checked = false;
                            Check(!Tv(s).DevHome && !Tv(Settings.Load()).DevHome && !tools.Tvs[0].LookingForAsks, "unticking the home screen option");

                            // a folder around the app: the app's own folder is pushed, and remembered
                            window.ShowPage(SettingsForm.AppPage);
                            WriteFile(parent, "inner/manifest", "title=Inner app\nmajor_version=1\nminor_version=0\nbuild_version=1\n");
                            WriteFile(parent, "inner/source/main.brs", "sub Main()\nend sub\n");
                            string inner = Path.Combine(parent, "inner");
                            window.Dev.PushApp(() => DevPackage.FromFolder(parent), null);
                            Pump(() => AllText(window).Contains("Pushed Inner app 1.0.1"), 10);
                            Check(Tv(s).DevFolder == inner && tv.DevAppName == "Inner app", "the folder pushed: " + Tv(s).DevFolder);
                            // pushing on save, ticked on App options; a save's push shows on App on the TV
                            FindBox(window, "Push the app's folder again").Checked = true;
                            Check(Tv(s).DevOnSave && tools.Tvs[0].Watching, "not watching after ticking the option");
                            WriteFile(parent, "inner/manifest", "title=Inner app\nmajor_version=1\nminor_version=0\nbuild_version=2\n");
                            Pump(() => AllText(window).Contains("Pushed Inner app 1.0.2"), 15);
                            // the example: the folder is forgotten, and so is watching it
                            window.Dev.PushApp(() => DevPackage.Example(), "");
                            Pump(() => AllText(window).Contains("Pushed " + ExampleShown), 10);
                            Check(Tv(s).DevFolder == "" && !tools.Tvs[0].Watching, "the example's push kept the folder: " + Tv(s).DevFolder);
                            // a folder push that fails (no password yet) still remembers the folder
                            Tv(s).DevOnSave = false;
                            box.Text = "";
                            window.Dev.PushApp(() => DevPackage.FromFolder(inner), null);
                            Pump(() => AllText(window).Contains("Type the password"), 10);
                            Check(Tv(s).DevFolder == inner, "a failed push forgot the folder");
                            box.Text = installer.Password;

                            tv.DeveloperEnabled = false;
                            window.ShowPage(SettingsForm.DeveloperPage);
                            Pump(() => AllText(window).Contains("Developer mode is off.") && AllText(window).Contains("Press Home 3 times"), 10);
                            Check(!FindButton(window, "Push the example app").Enabled, "pushing stays on with developer mode off");
                            window.Close();
                        }
                    }
                }
                finally
                {
                    DevPages.PollMs = pollBefore;
                    DevTv.SettleMs = settleBefore;
                    tools.Dispose();
                    File.Delete(Settings.FilePath);
                    Directory.Delete(Path.GetDirectoryName(parent), true);
                }
            }
        }

        static void DevUninstallRemovesCopies()
        {
            string dir = TempFolder("TVFollow data");
            try
            {
                File.WriteAllText(Path.Combine(dir, "settings.ini"), "ip=192.168.1.50\r\n");
                WriteFile(dir, "dev/X000000001.zip", "a kept app");
                WriteFile(dir, "dev/X000000002.zip.tmp", "half a kept app");
                Installer.RemoveSettings(dir);
                // a portable copy's folder left behind would make that copy start portable again
                Check(!Directory.Exists(dir), "the settings folder stayed: " +
                    (Directory.Exists(dir) ? string.Join(", ", Directory.GetFileSystemEntries(dir, "*", SearchOption.AllDirectories)) : ""));
            }
            finally { Directory.Delete(Path.GetDirectoryName(dir), true); }
        }

        static void DevSettingsSwitch()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings s = SettingsFor(tv);
                s.Save();
                var follower = new Follower(s, Controller.For(s)); // not started
                using (var tray = new Tray(follower))
                using (var dev = new DevTools(s, follower.Controllers))
                {
                    tray.Dev = dev;
                    using (var window = new SettingsForm(s, follower, tray))
                    {
                        window.StartPosition = FormStartPosition.Manual;
                        window.Location = new Point(-20000, -20000);
                        window.Show();
                        // as it opens for real: centred. From here on the window stays where it is.
                        window.StartPosition = FormStartPosition.CenterScreen;
                        Point where = window.Location;
                        Check(window.CurrentPage == SettingsForm.TvsPage, "it opened on " + window.CurrentPage);
                        Check(!window.ListedPages.Contains(SettingsForm.AppPage) && !window.ListedPages.Contains(SettingsForm.AppOptionsPage),
                            "the app pages are listed while the tools are off");
                        // an app page asked for while they're off: the page with their switch
                        window.ShowPage(SettingsForm.AppPage);
                        Check(window.CurrentPage == SettingsForm.DeveloperPage, "asking for an app page opened " + window.CurrentPage);
                        Check(!FindButton(window, "Check").Visible, "developer mode's part shows while the tools are off");
                        CheckBox box = FindBox(window, "Turn on the developer tools");
                        Check(box != null && !box.Checked, "no switch for the developer tools");
                        box.Checked = true;
                        Check(s.DeveloperTools && Settings.Load().DeveloperTools, "switching them on wasn't saved");
                        Check(window.ListedPages.Contains(SettingsForm.AppPage) && window.ListedPages.Contains(SettingsForm.AppOptionsPage),
                            "the app pages didn't show up: " + string.Join(", ", window.ListedPages.ToArray()));
                        Check(FindButton(window, "Check").Visible, "developer mode's part didn't show");
                        Check(window.CurrentPage == SettingsForm.DeveloperPage, "the switch moved the window to " + window.CurrentPage);
                        Check(window.Location == where, "the window moved to " + window.Location + " when the switch changed");
                        // switched off while an app page is open: back to the switch's page
                        window.ShowPage(SettingsForm.AppOptionsPage);
                        Check(window.CurrentPage == SettingsForm.AppOptionsPage, "App options didn't open");
                        box.Checked = false;
                        Check(!s.DeveloperTools && !window.ListedPages.Contains(SettingsForm.AppPage), "switching them off");
                        Check(window.CurrentPage == SettingsForm.DeveloperPage, "switching them off left " + window.CurrentPage + " open");
                        Check(!FindButton(window, "Check").Visible, "developer mode's part stayed");
                        window.Close();
                    }
                }
                File.Delete(Settings.FilePath);
            }
        }

        static void DevTrayItem()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings s = SettingsFor(tv);
                s.Save();
                var follower = new Follower(s, Controller.For(s)); // not started
                Action<Form> place = Tray.PlaceWindow;
                Tray.PlaceWindow = OffScreen;
                try
                {
                    using (var tray = new Tray(follower))
                    using (var dev = new DevTools(s, follower.Controllers))
                    {
                        tray.Dev = dev;
                        ToolStripItem item = TrayItem(tray, "Developer tools…");
                        Check(item != null, "no Developer tools… in the tray menu at all");
                        Check(ShownItems(tray).Contains("| Settings… | Open log |"), "the tray menu while they're off: " + ShownItems(tray));
                        tray.ShowSettings();
                        Check(OpenWindows<SettingsForm>().Count == 1, "Settings… didn't open the Settings window");
                        SettingsForm window = OpenWindows<SettingsForm>()[0];
                        Check(window.Location.X < -10000, "the Settings window opened on the screen, at " + window.Location);
                        CheckBox box = FindBox(window, "Turn on the developer tools");
                        box.Checked = true;
                        Check(ShownItems(tray).Contains("| Settings… | Developer tools… | Open log |"), "ticking the switch: " + ShownItems(tray));

                        // the item opens the same window, on App on the TV
                        window.ShowPage(SettingsForm.TvsPage);
                        item.PerformClick();
                        Check(OpenWindows<SettingsForm>().Count == 1 && OpenWindows<SettingsForm>()[0] == window, "the tray item opened a second window");
                        Check(window.CurrentPage == SettingsForm.AppPage, "the tray item opened " + window.CurrentPage);
                        // once more from minimized: the same window comes back
                        MinimizeOffScreen(window);
                        Check(window.WindowState == FormWindowState.Minimized && IsIconic(window.Handle), "the test couldn't minimize the window");
                        item.PerformClick();
                        Check(OpenWindows<SettingsForm>().Count == 1 && OpenWindows<SettingsForm>()[0] == window, "a second click opened a second window");
                        Check(window.WindowState == FormWindowState.Normal && !IsIconic(window.Handle), "a minimized Settings window stayed minimized");

                        box.Checked = false;
                        Check(window.CurrentPage == SettingsForm.DeveloperPage, "switching them off left " + window.CurrentPage + " open");
                        Check(ShownItems(tray).Contains("| Settings… | Open log |"), "switching them off left the tray item: " + ShownItems(tray));
                        window.Close();
                    }
                    // switched on before TVFollow starts: the item is there from the start
                    s.DeveloperTools = true;
                    using (var tray = new Tray(follower))
                        Check(ShownItems(tray).Contains("| Settings… | Developer tools… | Open log |"), "after a restart: " + ShownItems(tray));
                }
                finally
                {
                    Tray.PlaceWindow = place;
                    foreach (SettingsForm left in OpenWindows<SettingsForm>()) left.Close();
                }
                File.Delete(Settings.FilePath);
            }
        }

        static void DevHomeOpensApp()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            using (var console = new FakeConsole("127.0.0.1", 0))
            using (new InputTimings(console, 100))
            {
                int settle = DevTv.HomeSettleMs, stay = DevTv.StayHomeMs;
                DevTv.HomeSettleMs = 100;
                DevTv.StayHomeMs = 1500;
                try
                {
                    tv.ModernHome = true;
                    tv.DeveloperEnabled = true;
                    tv.DevAppName = "Home app";
                    tv.App = "";
                    // Roku OS 15 shows its home screen as an app of type "home": it's still the home screen
                    Check(new RokuClient(tv.Ip, tv.Port).GetActiveApp(2000) == "", "the home screen read as an app");
                    Settings s = SettingsFor(tv);
                    s.DeveloperTools = true;
                    Tv(s).DevHome = true;
                    using (DevTools tools = DevToolsFor(s))
                    {
                        // on the home screen: the app opens
                        Until(tv, () => tv.App == "dev", "the app opening from the home screen");
                        Until(tv, () => LogText().Contains("the TV went to Roku's home screen, so Home app opened"), "the log line");
                        // Home pressed right after: the TV stays home
                        Check(new RokuClient(tv.Ip, tv.Port).Keypress("Home", 2000) == 200 && tv.App == "", "pressing Home");
                        Until(tv, () => LogText().Contains("right after the app opened, so the TV stays there"), "staying home");
                        Thread.Sleep(600);
                        Check(tv.App == "" && tv.Count("POST /launch/dev") == 1, "it opened again after Home twice: " + tv.Count("POST /launch/dev"));
                        // another app, then backing out of it to the home screen: the app opens again
                        tv.App = "12";
                        Thread.Sleep(400);
                        tv.App = "";
                        Until(tv, () => tv.App == "dev" && tv.Count("POST /launch/dev") == 2, "the app after backing out of another");
                        // a long while in the app, then Home: it opens again; Home once more: it stays
                        Thread.Sleep(DevTv.StayHomeMs + 200);
                        tv.App = "";
                        Until(tv, () => tv.App == "dev" && tv.Count("POST /launch/dev") == 3, "the app after a while in it");
                        tv.App = "";
                        Thread.Sleep(800);
                        Check(tv.App == "" && tv.Count("POST /launch/dev") == 3, "Home twice didn't stay home");
                        // turned off on the home screen: nothing opens; turned on, landing home: it opens
                        tv.PowerMode = "DisplayOff";
                        Thread.Sleep(600);
                        Check(tv.Count("POST /launch/dev") == 3, "the app opened on a TV that's off");
                        tv.PowerMode = "PowerOn";
                        Until(tv, () => tv.App == "dev" && tv.Count("POST /launch/dev") == 4, "the app after turning the TV on");
                        // paused: the TV is left alone
                        s.Paused = true;
                        tv.App = "12";
                        Thread.Sleep(300);
                        tv.App = "";
                        Thread.Sleep(800);
                        Check(tv.App == "" && tv.Count("POST /launch/dev") == 4, "the app opened while paused");
                        s.Paused = false;
                        // no pushed app: nothing to open, and it says so
                        tv.DevAppName = null;
                        tv.App = "12";
                        Thread.Sleep(300);
                        tv.App = "";
                        Until(tv, () => LogText().Contains("but there's no pushed app to open"), "saying there's no app");
                        Check(tv.Count("POST /launch/dev") == 4, "opened an app that isn't there");
                        // unticked: the looks stop
                        Tv(s).DevHome = false;
                        tools.Refresh();
                        Check(!tools.Tvs[0].LookingForAsks, "still looking with the option off");
                    }
                }
                finally
                {
                    DevTv.HomeSettleMs = settle;
                    DevTv.StayHomeMs = stay;
                }
            }
        }

        static void DevAppLogSaved()
        {
            using (var console = new FakeConsole("127.0.0.1", 0))
            {
                int before = DevClient.ConsolePort;
                long max = DevConsole.SaveMaxBytes;
                DevClient.ConsolePort = console.Port;
                string dir = TempFolder("App log");
                string path = Path.Combine(dir, "X000000001 app log.txt");
                try
                {
                    var shared = new DevConsole(() => "127.0.0.1") { SavePath = () => path };
                    var reader = new ConsoleListener();
                    shared.Listen(reader);
                    Pump(() => console.Connections == 1, 5);
                    console.Say("TVFollow example: a saved line");
                    Pump(() => File.Exists(path) && ReadShared(path).Contains("TVFollow example: a saved line"), 10);
                    string text = ReadShared(path);
                    Check(text.Contains("---- TVFollow connected to the app log, ") && text.Contains("Welcome to the pretend app console."), "the saved log: " + text);
                    // past the most it keeps, the file keeps its newer half
                    DevConsole.SaveMaxBytes = 4000;
                    for (int i = 0; i < 60; i++) console.Say("line " + i + " " + new string('x', 80));
                    Pump(() => ReadShared(path).Contains("line 59 "), 10);
                    console.Say("after the trim");
                    Pump(() => ReadShared(path).Contains("after the trim"), 10);
                    long size = new FileInfo(path).Length;
                    Check(!ReadShared(path).Contains("a saved line") && size < 4000 + 4096, "the file wasn't trimmed: " + size + " bytes");
                    shared.Unlisten(reader);
                }
                finally
                {
                    DevClient.ConsolePort = before;
                    DevConsole.SaveMaxBytes = max;
                    Directory.Delete(Path.GetDirectoryName(dir), true);
                }
            }
        }

        static string ReadShared(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(file)) return reader.ReadToEnd();
        }

        static void DevCommandLine()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            using (var installer = new FakeInstaller(tv, 0))
            {
                tv.DeveloperEnabled = true;
                Settings s = DevSettings(tv, installer);
                var tvs = new List<Controller> { Fast(FirstTv(s)) };
                string parent = TempFolder("Command line");
                try
                {
                    WriteFile(parent, "app/manifest", "title=Command app\nmajor_version=2\nminor_version=0\nbuild_version=1\n");
                    WriteFile(parent, "app/source/main.brs", "sub Main()\nend sub\n' " + new string('x', 700) + "\n");
                    string app = Path.Combine(parent, "app");
                    // the developer tools switched off: nothing happens
                    s.DeveloperTools = false;
                    Check(Program.DevCommand(s, tvs, "push", new[] { "push", app }) == 2 && tv.DevAppName == null, "pushed with the tools off");
                    s.DeveloperTools = true;
                    Check(Program.DevCommand(s, tvs, "push", new[] { "push" }) == 2, "a push of nothing");
                    Check(Program.DevCommand(s, tvs, "push", new[] { "push", Path.Combine(parent, "missing") }) == 1, "a push of a folder that isn't there");
                    // a folder, then a picture saved into a folder
                    Check(Program.DevCommand(s, tvs, "push", new[] { "push", app }) == 0 && tv.DevAppName == "Command app", "the push: " + tv.DevAppName);
                    Check(LogText().Contains("developer tools: pushed (command line) Command app"), "the push's log line");
                    Check(Tv(s).DevFolder == "", "the command line changed the folder pushed on save: " + Tv(s).DevFolder);
                    Check(Program.DevCommand(s, tvs, "screenshot", new[] { "screenshot", parent }) == 0, "the picture");
                    string[] pictures = Directory.GetFiles(parent, "*.jpg");
                    Check(pictures.Length == 1 && File.ReadAllBytes(pictures[0]).Length > 100, "the picture's file: " + pictures.Length);
                }
                finally { Directory.Delete(Path.GetDirectoryName(parent), true); }
            }
        }

        // the timings the input tests make short, put back afterwards
        class InputTimings : IDisposable
        {
            readonly int idle = DevTv.InputsIdleMs, after = DevTv.HelloAfterMs,
                retry = DevConsole.RetryMs, port = DevClient.ConsolePort;

            public InputTimings(FakeConsole console, int helloAfterMs)
            {
                DevTv.InputsIdleMs = 150;
                DevTv.HelloAfterMs = helloAfterMs;
                DevConsole.RetryMs = 200;
                DevClient.ConsolePort = console.Port;
            }

            public void Dispose()
            {
                DevTv.InputsIdleMs = idle;
                DevTv.HelloAfterMs = after;
                DevConsole.RetryMs = retry;
                DevClient.ConsolePort = port;
            }
        }

        static void DevAppSwitchesInputs()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            using (var console = new FakeConsole("127.0.0.1", 0))
            using (new InputTimings(console, 100))
            {
                tv.DeveloperEnabled = true;
                tv.DevAppName = "Input app";
                tv.InputNames = new[] { "PC", "Game console", "HDMI 3" };
                tv.App = "dev";
                // the TV repeats its recent lines to a new reader: an ask from an earlier run
                console.Backlog = "TVFollow ask 0ld70ken tvinput.hdmi3\nTVFollow hello\n";
                Settings s = SettingsFor(tv);
                s.DeveloperTools = true;
                Tv(s).DevInputs = true;
                using (DevTools tools = DevToolsFor(s))
                {
                    // the app on screen is told TVFollow is there, which inputs the TV has, and a token
                    Until(tv, () => Hellos(tv).Count >= 1, "the first hello");
                    Dictionary<string, string> hello = Hellos(tv)[0];
                    Check(hello["protocol"] == "1" && hello["pc"] == "tvinput.hdmi1" && hello["inputs"] == "4" && hello["token"].Length == 8,
                        "the hello: " + Describe(hello));
                    Check(hello["input1"] == "tvinput.hdmi1" && hello["name1"] == "PC" && hello["input2"] == "tvinput.hdmi2" &&
                        hello["name2"] == "Game console" && hello["input4"] == "tvinput.cvbs" && hello["name4"] == "AV", "the inputs: " + Describe(hello));
                    Thread.Sleep(400);   // a few looks
                    Check(tv.App == "dev", "an ask the TV repeated from before was carried out: " + tv.App);
                    string token = Last(Hellos(tv))["token"];

                    // the app asks: the TV switches, and the app's console is left alone
                    console.Say("TVFollow ask " + token + " tvinput.hdmi2");
                    Until(tv, () => tv.App == "tvinput.hdmi2", "the switch to HDMI 2");
                    Until(tv, () => LogText().Contains("the app asked for Game console (tvinput.hdmi2): switched"), "the log line for the switch");
                    Until(tv, () => !tools.Tvs[0].ListeningForAsks && !tools.Tvs[0].Console.Connected, "letting go of the console");

                    // back in the app: told again with a new token; the ask the TV repeats isn't carried out again
                    console.Backlog = "TVFollow ask " + token + " tvinput.hdmi2\n";
                    int hellos = Hellos(tv).Count;
                    tv.App = "dev";
                    Until(tv, () => Hellos(tv).Count > hellos, "the hello when the app came back");
                    string token2 = Last(Hellos(tv))["token"];
                    Check(token2.Length == 8 && token2 != token, "the token stayed the same: " + token2);
                    Thread.Sleep(400);
                    Check(tv.App == "dev", "the repeated ask was carried out again: " + tv.App);
                    console.Say("TVFollow ask " + token + " tvinput.hdmi2");
                    Thread.Sleep(400);
                    Check(tv.App == "dev", "an ask with an old token was carried out");

                    // the app's own hello line gets a hello
                    hellos = Hellos(tv).Count;
                    console.Say("TVFollow hello");
                    Until(tv, () => Hellos(tv).Count > hellos, "the hello for the app's hello line");
                    Check(Last(Hellos(tv))["token"] == token2, "a hello on the same connection changed the token");

                    // only the TV's own inputs, and the app's words stay one line in the log
                    console.Say("TVFollow ask " + token2 + " tvinput.hdmi9");
                    Until(tv, () => LogText().Contains("\"tvinput.hdmi9\", an input this TV doesn't have"), "the log line for a missing input");
                    console.Say("TVFollow ask " + token2 + " launch/12");
                    Until(tv, () => LogText().Contains("\"launch/12\", which isn't an input"), "the log line for something that isn't an input");
                    console.Say("TVFollow ask " + token2 + " tvinput.hdmi1" + (char)27 + "[2J");
                    Until(tv, () => LogText().Contains("\"tvinput.hdmi1 [2J\""), "the log line for an ask with a control character");
                    Check(LogText().IndexOf((char)27) < 0, "a control character got into the log");
                    Check(tv.App == "dev" && tv.Count("POST /launch/12") == 0, "something that isn't an input was opened: " + tv.App);

                    // a line can come in pieces
                    console.Send("TVFollow ask " + token2 + " tvin");
                    Thread.Sleep(200);
                    Check(tv.App == "dev", "half a line was carried out");
                    console.Say("put.hdmi3");
                    Until(tv, () => tv.App == "tvinput.hdmi3", "the switch from a line in two pieces");

                    // a moment without an answer from the TV isn't the app leaving: same connection, same token
                    hellos = Hellos(tv).Count;
                    tv.App = "dev";
                    Until(tv, () => Hellos(tv).Count > hellos, "the hello after HDMI 3");
                    string token3 = Last(Hellos(tv))["token"];
                    int accepted = console.Accepted;
                    tv.Down = true;
                    Thread.Sleep(200);
                    tv.Down = false;
                    console.Say("TVFollow ask " + token3 + " tvinput.hdmi2");
                    Until(tv, () => tv.App == "tvinput.hdmi2", "the switch after the TV missed a look");
                    Check(console.Accepted == accepted, "the console was read anew after one missed look");

                    // the app log window open too: the asks share its connection, the lines the
                    // window already has don't count, and a switch leaves the window's connection be
                    using (var window = new DevLogForm(tools.Tvs[0].Console, "Test TV"))
                    {
                        console.Backlog = "TVFollow ask " + token3 + " tvinput.hdmi1\n";
                        OffScreen(window);
                        window.Show();
                        Pump(() => window.LogText.Contains("TVFollow ask " + token3), 10);
                        accepted = console.Accepted;
                        hellos = Hellos(tv).Count;
                        tv.App = "dev";
                        Until(tv, () => Hellos(tv).Count > hellos, "the hello with the app log open");
                        string token4 = Last(Hellos(tv))["token"];
                        Thread.Sleep(400);
                        Check(tv.App == "dev" && token4 != token3, "an ask the window already had was carried out: " + tv.App);
                        console.Say("TVFollow ask " + token4 + " tvinput.hdmi3");
                        Until(tv, () => tv.App == "tvinput.hdmi3", "the switch with the app log open");
                        Until(tv, () => !tools.Tvs[0].ListeningForAsks, "letting go after the switch");
                        Check(tools.Tvs[0].Console.Connected && console.Accepted == accepted, "the window's connection went, or a second one was made");
                        Pump(() => window.LogText.Contains("TVFollow ask " + token4), 10);
                        window.Close();
                    }
                }
                Check(Hellos(tv).Count > 0, "no hellos at all");
            }
        }

        static void DevInputsOnlyWhenOn()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            using (var console = new FakeConsole("127.0.0.1", 0))
            using (new InputTimings(console, 300))
            {
                tv.DeveloperEnabled = true;
                tv.DevAppName = "Input app";
                tv.App = "dev";
                Settings s = SettingsFor(tv);
                using (DevTools tools = DevToolsFor(s))
                {
                    // the developer tools off, then on without the option: nobody looks
                    Thread.Sleep(300);
                    s.DeveloperTools = true;
                    tools.Refresh();
                    Thread.Sleep(300);
                    Check(!tools.Tvs[0].LookingForAsks && console.Accepted == 0 && Hellos(tv).Count == 0, "looked without the option");
                    // ticked: the console is read and the app told; the developer tools off again: all stops
                    Tv(s).DevInputs = true;
                    tools.Refresh();
                    Until(tv, () => Hellos(tv).Count >= 1, "the hello once ticked");
                    s.DeveloperTools = false;
                    tools.Refresh();
                    Check(!tools.Tvs[0].LookingForAsks && !tools.Tvs[0].ListeningForAsks, "still at it with the developer tools off");
                    Until(tv, () => !tools.Tvs[0].Console.Connected, "letting go of the console");
                    Thread.Sleep(300);   // a look under way finishes
                    int asked = tv.Requests().Count, connections = console.Accepted;
                    Thread.Sleep(500);
                    Check(tv.Requests().Count == asked && console.Accepted == connections, "kept asking the TV after the developer tools went off");
                }

                // another Roku at the TV's address: its console isn't read, and it isn't told anything
                s.DeveloperTools = true;
                tv.Serial = "SOMEONE-ELSE";
                int hellos = Hellos(tv).Count, accepted = console.Accepted;
                using (DevTools tools = DevToolsFor(s))
                {
                    Thread.Sleep(600);
                    Check(Hellos(tv).Count == hellos && console.Accepted == accepted, "another Roku was read or told");
                }
                tv.Serial = Tv(s).Serial;

                // this PC on another network (a laptop away from home): nothing goes to the TV's address
                Controller away = Fast(FirstTv(s));
                away.OnTvNetwork = () => false;
                int looks = tv.Count("GET /query/active-app");
                using (var tools = new DevTools(s, new List<Controller> { away }))
                {
                    Thread.Sleep(600);
                    Check(tv.Count("GET /query/active-app") == looks && console.Accepted == accepted, "the TV was asked from another network");
                }

                // unticked while a look is talking to the TV: the look doesn't start reading the console
                tv.DelayMs = 500;
                looks = tv.Count("GET /query/active-app");
                using (DevTools tools = DevToolsFor(s))
                {
                    Until(tv, () => tv.Count("GET /query/active-app") > looks, "the first look");
                    Tv(s).DevInputs = false;
                    tools.Refresh();
                    Thread.Sleep(1500);
                    Check(!tools.Tvs[0].ListeningForAsks && console.Accepted == accepted && Hellos(tv).Count == hellos,
                        "a look that was under way when the option was unticked started reading the console");
                }
                tv.DelayMs = 0;
                Tv(s).DevInputs = true;

                // another program reads the console, and the TV is slow to hang up: said once, and
                // no hello while it does
                console.Busy = true;
                console.BusyHangUpMs = 800;
                using (DevTools tools = DevToolsFor(s))
                {
                    Until(tv, () => LogText().Contains("another program is reading the app's console"), "the log line for a busy console");
                    Until(tv, () => console.Accepted >= accepted + 3, "trying the console again");
                    string log = LogText();
                    Check(log.IndexOf("another program is reading") == log.LastIndexOf("another program is reading"), "said more than once: " + log);
                    Check(Hellos(tv).Count == hellos, "the app was told while its console was busy");
                    // free again: the hello comes
                    console.Busy = false;
                    Until(tv, () => Hellos(tv).Count > hellos, "the hello once the console was free");
                }

                // Limited hides the TV's inputs: nothing to tell the app
                tv.ControlMode = "limited";
                hellos = Hellos(tv).Count;
                using (DevTools tools = DevToolsFor(s))
                {
                    Until(tv, () => LogText().Contains("the TV won't list its inputs"), "the log line for inputs hidden by Limited");
                    Check(Hellos(tv).Count == hellos, "the app was told about inputs the TV didn't list");
                }
            }
        }

        static Dictionary<string, string> Last(List<Dictionary<string, string>> hellos)
        {
            return hellos[hellos.Count - 1];
        }

        // the hellos TVFollow sent to the app on the pretend TV, each as its name=value pairs
        static List<Dictionary<string, string>> Hellos(FakeTv tv)
        {
            var hellos = new List<Dictionary<string, string>>();
            foreach (string request in tv.Requests())
            {
                if (!request.StartsWith("POST /input?")) continue;
                var pairs = new Dictionary<string, string>();
                foreach (string pair in request.Substring("POST /input?".Length).Split('&'))
                {
                    int eq = pair.IndexOf('=');
                    if (eq > 0) pairs[Uri.UnescapeDataString(pair.Substring(0, eq))] = Uri.UnescapeDataString(pair.Substring(eq + 1));
                }
                string kind;
                if (pairs.TryGetValue("tvfollow", out kind) && kind == "hello") hellos.Add(pairs);
            }
            return hellos;
        }

        static string Describe(Dictionary<string, string> pairs)
        {
            var parts = new List<string>();
            foreach (KeyValuePair<string, string> p in pairs) parts.Add(p.Key + "=" + p.Value);
            return string.Join(", ", parts);
        }

        // waits up to 10 s for done; if it doesn't come, says what didn't happen, with the log and
        // the pretend TV's last requests
        static void Until(FakeTv tv, Func<bool> done, string what)
        {
            try { Pump(done, 10); }
            catch (TimeoutException)
            {
                List<string> asked = tv.Requests();
                throw new Exception(what + " didn't come. The log: " + LogText().Replace("\r\n", " / ") +
                    " The TV's last requests: " + string.Join(" | ", asked.GetRange(Math.Max(0, asked.Count - 8), Math.Min(8, asked.Count)).ToArray()));
            }
        }

        static void OffScreen(Form window)
        {
            window.StartPosition = FormStartPosition.Manual;
            window.Location = new Point(-20000, -20000);
            window.ShowInTaskbar = false;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WindowPlacement
        {
            public int Length, Flags, ShowCmd, MinX, MinY, MaxX, MaxY, Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr window, ref WindowPlacement placement);
        [DllImport("user32.dll")] static extern bool SetWindowPlacement(IntPtr window, ref WindowPlacement placement);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);

        // Minimized, and off the screen while minimized too: without a taskbar button, Windows would
        // park it as a little title bar at the bottom of the screen
        static void MinimizeOffScreen(Form window)
        {
            var placement = new WindowPlacement { Length = Marshal.SizeOf(typeof(WindowPlacement)) };
            GetWindowPlacement(window.Handle, ref placement);
            placement.Flags = 1;                        // WPF_SETMINPOSITION
            placement.MinX = placement.MinY = -20000;
            placement.ShowCmd = 7;                      // SW_SHOWMINNOACTIVE: no other window comes forward
            SetWindowPlacement(window.Handle, ref placement);
        }

        static ToolStripItem TrayItem(Tray tray, string text)
        {
            foreach (ToolStripItem item in tray.Menu.Items) if (item.Text == text) return item;
            return null;
        }

        // the tray menu as it would open, a line between items blank: "… | Settings… | Open log |  | Quit TVFollow |"
        // (Available, not Visible: Visible is false for every item while the menu is closed)
        static string ShownItems(Tray tray)
        {
            string text = "|";
            foreach (ToolStripItem item in tray.Menu.Items) if (item.Available) text += " " + item.Text + " |";
            return text;
        }

        // the open windows of one kind (DevForm, SettingsForm)
        static List<T> OpenWindows<T>() where T : Form
        {
            var found = new List<T>();
            foreach (Form f in Application.OpenForms)
            {
                var w = f as T;
                if (w != null && !w.IsDisposed) found.Add(w);
            }
            return found;
        }
    }
}
