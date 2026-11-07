using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
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
            Run("dev: the developer window shows the TV's state", DevWindow);
            Run("dev: Settings shows the developer tools only when switched on", DevSettingsSwitch);
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
            s.Tvs.Add(new TvSettings { Ip = "192.168.1.51", Serial = "X000000002", DevPushBack = false });
            s.DeveloperTools = true;
            s.Save();
            string file = File.ReadAllText(Settings.FilePath);
            Check(!file.Contains(password), "the password is in settings.ini as typed");
            Check(file.Contains("dev_password=dpapi:"), "the password isn't sealed: " + file);
            Settings t = Settings.Load();
            Check(t.DeveloperTools, "the switch was lost");
            Check(Tv(t).DevPassword == password, "the password didn't come back");
            Check(Tv(t).DevFolder == "C:\\Apps\\Den #2 app", "the folder lost its #: " + Tv(t).DevFolder);
            Check(Tv(t).DevOnSave && Tv(t).DevPushBack, "the first TV's options were lost");
            Check(!t.Tvs[1].DevPushBack && !t.Tvs[1].DevOnSave && t.Tvs[1].DevPassword == "", "the second TV's options were lost");

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
            Check(p.Title == "TVFollow example" && p.Version == "1.0.1", "the example is " + p);
            List<string> names = Entries(p.Zip);
            foreach (string need in new[] { "manifest", "source/main.brs", "components/MainScene.xml", "components/MainScene.brs",
                "images/icon_focus_fhd.png", "images/icon_focus_hd.png", "images/splash_fhd.png", "images/splash_hd.png" })
                Check(names.Contains(need), "the example has no " + need);
            using (var archive = new ZipArchive(new MemoryStream(p.Zip), ZipArchiveMode.Read))
            {
                // every file the manifest and the code point at is in it
                foreach (string from in new[] { "manifest", "components/MainScene.xml", "source/main.brs", "components/MainScene.brs" })
                {
                    string text;
                    using (var reader = new StreamReader(archive.GetEntry(from).Open())) text = reader.ReadToEnd();
                    foreach (Match m in Regex.Matches(text, "pkg:/([\\w./-]+)"))
                        Check(archive.GetEntry(m.Groups[1].Value) != null, from + " points at a missing " + m.Groups[1].Value);
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
                Check(r.Ok && r.Text.StartsWith("Pushed TVFollow example 1.0.1"), "push: " + r.Text);
                Check(tv.DevAppName == "TVFollow example" && tv.DevAppVersion == "1.0.1" && tv.App == "dev", "the TV didn't get the app");
                Check(installer.Installs == 1 && !installer.SawBasic, "installs " + installer.Installs + ", the password offered as it is: " + installer.SawBasic);
                Check(dev.HasKept, "no copy kept to put back");
                DevStatus st = dev.Status(2000);
                Check(st.Answering && st.DevMode && st.AppKnown && st.App != null && st.App.ToString() == "TVFollow example 1.0.1", "the state after the push");

                // the same files again: the TV keeps them, and TVFollow opens the app
                tv.App = "tvinput.hdmi1";
                r = dev.Push(DevPackage.Example(), "pushed");
                Check(r.Ok && r.Text.Contains("already had these very files") && installer.Installs == 1 && tv.Count("POST /launch/dev") == 1 && tv.App == "dev",
                    "the same files again: " + r.Text);
                string log = LogText();
                Check(log.Contains("developer tools: pushed TVFollow example 1.0.1"), "the push isn't in the log: " + log);
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
                Check(r.Ok && tv.Requests().Contains("INSTALLER Install") && tv.DevAppName == "TVFollow example", "Install after a refused Replace: " + r.Text);
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
                Check(tv.DevAppName == "TVFollow example" && installer.Installs == 2, "not put back");
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
                Check(tv.DevAppName == "TVFollow example" && tv.IsOn && tv.App == "tvinput.hdmi1" && tv.Count("POST /launch/tvinput.hdmi1") == 1,
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
                    using (var form = new DevLogForm("127.0.0.1", "Test TV"))
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
                        form.Close();
                    }
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
                tv.DevAppName = "TVFollow example";
                tv.DevAppVersion = "1.0.1";
                Settings s = DevSettings(tv, installer);
                DevTools tools = DevToolsFor(s);
                int pollBefore = DevForm.PollMs;
                DevForm.PollMs = 200;
                try
                {
                    using (var window = new DevForm(tools, tools.Tvs[0]))
                    {
                        window.StartPosition = FormStartPosition.Manual;
                        window.Location = new Point(-20000, -20000);
                        window.ShowInTaskbar = false;
                        window.Show();
                        Pump(() => AllText(window).Contains("Developer mode is on.") && AllText(window).Contains("On the TV now: TVFollow example 1.0.1"), 10);
                        Check(FindButton(window, "Push the example app").Enabled && FindButton(window, "Remove").Enabled, "the buttons are off with developer mode on");
                        TextBox box = FindControl<TextBox>(window, b => b.UseSystemPasswordChar);
                        Check(box != null && box.Text == installer.Password, "the password box doesn't have the saved password");
                        box.Text = "another-pass";
                        Check(Tv(s).DevPassword == "another-pass" && Tv(Settings.Load()).DevPassword == "another-pass", "a typed password wasn't saved");

                        tv.DeveloperEnabled = false;
                        Pump(() => AllText(window).Contains("Developer mode is off.") && AllText(window).Contains("Press Home 3 times"), 10);
                        Check(!FindButton(window, "Push the example app").Enabled, "pushing stays on with developer mode off");
                        window.Close();
                    }
                }
                finally
                {
                    DevForm.PollMs = pollBefore;
                    tools.Dispose();
                    File.Delete(Settings.FilePath);
                }
            }
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
                        Check(!AllText(window).Contains("Developer tools…"), "the developer tools' button shows while they're off");
                        CheckBox box = FindBox(window, "Show the developer tools");
                        Check(box != null && !box.Checked, "no switch for the developer tools");
                        box.Checked = true;
                        Check(s.DeveloperTools && Settings.Load().DeveloperTools, "switching them on wasn't saved");
                        Check(AllText(window).Contains("Developer tools…"), "the button didn't show");
                        box.Checked = false;
                        Check(!s.DeveloperTools && !AllText(window).Contains("Developer tools…"), "switching them off");
                        window.Close();
                    }
                }
                File.Delete(Settings.FilePath);
            }
        }
    }
}
