using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace TVFollow.Tests
{
    // Runs every test against pretend TVs on this PC's loopback addresses. No real TV needed.
    static partial class TestMain
    {
        static int passed, failed;
        static Exception windowError;  // what a window's message handler threw during the running test

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--shots") return Shots.Run(args[1], false);
            if (args.Length == 2 && args[0] == "--screenshots") return Shots.Run(args[1], true);
            if (args.Length == 1 && args[0] == "--probe") return Shots.Probe();

            // an exception inside a window's message handler never reaches Run on its own: WinForms
            // catches it and shows its error dialog (or, with nobody there, drops it). Caught here, it
            // fails the test that was running instead, and no dialog stops the run.
            System.Windows.Forms.Application.ThreadException += (s, e) => { windowError = e.Exception; };

            string home = Path.Combine(Path.GetTempPath(), "TVFollowTests-" + Guid.NewGuid().ToString("N"));
            Settings.Dir = home;

            Run("settings save and load", SettingsRoundTrip);
            Run("settings read the old 'mac' key", SettingsOldMacKey);
            Run("device info is read right", DeviceInfoParse);
            Run("wake address follows the TV's connection", WakeMacFollowsNetwork);
            Run("off: turns off a TV showing the PC", OffWhenShowingPc);
            Run("off: leaves a TV showing something else", OffLeavesOtherApp);
            Run("off button: turns it off whatever it shows", OffButtonForces);
            Run("on: turns on and switches to the PC", OnFromOff);
            Run("on: leaves a TV that's busy with something else", OnLeavesOtherApp);
            Run("TV to PC button: switches input", ToPcSwitches);
            Run("TV on button: only powers on", PowerOnlyDoesNotSwitch);
            Run("Limited setting: gives up cleanly and logs 403", LimitedIsLogged);
            Run("deep standby: sends a wake packet", WakesDeepStandby);
            Run("moved TV: found again by serial and saved", FindsMovedTv);
            Run("rules: monitors on with fresh input", RulesFreshInput);
            Run("rules: monitors on with nobody there waits for input", RulesWaitForInput);
            Run("rules: paused and switched-off rules do nothing", RulesPausedAndOptions);
            Run("TV inputs are read with their names", InputsAreRead);
            Run("scanner lists TVs before sticks and skips silence", ScannerOrder);
            Run("power test: passes on a TV that answers while off", PowerTestPasses);
            Run("power test: stops cleanly on the Limited setting", PowerTestLimited);
            Run("power test: wakes a TV from deep standby", PowerTestWakes);
            Run("power test: explains Fast TV start when the TV can't be reached", PowerTestUnreachable);
            Run("a separate settings folder gets its own instance", InstanceNames);
            Run("install, change options, uninstall", InstallAndUninstall);
            Run("a trial copy stays inside its own folder", TrialInstallPaths);
            Run("the icons are built in", IconsAreBuiltIn);
            Run("updates: comparing versions", UpdateVersions);
            Run("updates: reading GitHub's answer and the weekly timing", UpdateParsingAndTiming);
            Run("updates: remembering the last check", UpdateState);
            RunReviewFixes();
            Run("version matches the exe's version", VersionMatches);

            try { Directory.Delete(home, true); } catch { }
            Console.WriteLine();
            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }

        static void Run(string name, Action test)
        {
            try
            {
                if (File.Exists(Log.FilePath)) File.Delete(Log.FilePath);
                windowError = null;
                test();
                if (windowError != null) throw new Exception("a window threw: " + windowError.Message);
                passed++;
                Console.WriteLine("PASS  " + name);
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine("FAIL  " + name + ": " + e.Message);
            }
        }

        static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception(what);
        }

        static string LogText()
        {
            return File.Exists(Log.FilePath) ? File.ReadAllText(Log.FilePath) : "";
        }

        static Controller Fast(Controller c)
        {
            c.FirstCheckMs = 20;
            c.RetryMs = 30;
            c.PollMs = 20;
            c.ReachMs = 200;
            c.WakeReachMs = 400;
            c.SweepWaitMs = 100;
            c.SendWake = mac => { throw new Exception("unexpected wake packet"); };
            c.Candidates = () => new List<string>();
            c.OnTvNetwork = () => true;
            c.SameNumbers = () => false; // a test that takes the PC off the TV's network means all the way off
            c.GatewayMacFor = ip => "";
            c.SearchEvery = TimeSpan.Zero;
            c.WakeRepeatMs = 1000000;
            return c;
        }

        static Settings SettingsFor(FakeTv tv)
        {
            var s = new Settings();
            s.Ip = tv.Ip;
            s.Port = tv.Port;
            s.Serial = tv.Serial;
            s.Input = "hdmi1";
            s.WakeMac = "00:11:22:33:44:55";
            return s;
        }

        // ---- settings and parsing ----

        static void SettingsRoundTrip()
        {
            var s = new Settings();
            s.Ip = "192.168.1.50";
            s.Serial = "X000000001";
            s.Input = "hdmi2";
            s.WakeMac = "00:11:22:33:44:55";
            s.GatewayMac = "AA:BB:CC:DD:EE:01";
            s.Port = 18060;
            s.OnWithMonitors = false;
            s.StartWithWindows = false;
            s.DesktopShortcuts = true;
            s.CheckUpdates = false;
            s.Save();
            s.Save(); // second save replaces the file
            Settings t = Settings.Load();
            Check(t.Ip == s.Ip && t.Serial == s.Serial && t.Input == "hdmi2" && t.WakeMac == s.WakeMac && t.GatewayMac == s.GatewayMac, "fields lost");
            Check(t.Port == 18060, "port lost");
            Check(!t.OnWithMonitors && t.OffWithMonitors, "options lost");
            Check(!t.StartWithWindows && t.DesktopShortcuts && !t.CheckUpdates, "install options lost");
            Check(t.PcApp == "tvinput.hdmi2", "PC app wrong");
            File.Delete(Settings.FilePath);
        }

        static void SettingsOldMacKey()
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(Settings.FilePath, "# old style\r\nip=192.168.1.50\r\nserial=X1\r\ninput=HDMI1\r\nmac=00:11:22:33:44:55\r\nport=nonsense\r\n");
            Settings s = Settings.Load();
            Check(s.WakeMac == "00:11:22:33:44:55", "old mac key ignored");
            Check(s.Input == "hdmi1", "input not lower-cased");
            Check(s.Port == Settings.DefaultPort, "bad port not ignored");
            Check(s.IsComplete, "should count as set up");
            File.Delete(Settings.FilePath);
        }

        static void DeviceInfoParse()
        {
            DeviceInfo d = DeviceInfo.Parse(
                "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n<device-info>\n" +
                "<serial-number>X000000001</serial-number><vendor-name>Example</vendor-name><model-name>EX43</model-name>" +
                "<is-tv>true</is-tv><friendly-device-name>Example TV</friendly-device-name><user-device-name></user-device-name>" +
                "<user-device-location>Den</user-device-location><power-mode>DisplayOff</power-mode>" +
                "<ecp-setting-mode>limited</ecp-setting-mode></device-info>");
            Check(d.Serial == "X000000001" && d.Vendor == "Example" && d.Model == "EX43", "basic fields");
            Check(d.Name == "Example TV", "name should fall back to the friendly name, got " + d.Name);
            Check(d.Location == "Den" && d.IsTv && !d.IsOn, "location / is-tv / power");
            Check(!d.AllowsControl, "limited should block control");
            DeviceInfo old = DeviceInfo.Parse("<device-info><serial-number>X2</serial-number></device-info>");
            Check(old.AllowsControl, "a TV that doesn't report the setting allows control");
            Check(!old.IsTv, "missing is-tv means not a TV");
        }

        static void WakeMacFollowsNetwork()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                var client = new RokuClient(tv.Ip, tv.Port);
                Check(client.GetDeviceInfo(2000).WakeMac == "00:11:22:33:44:55", "Wi-Fi TV should wake on its Wi-Fi address");
                tv.NetworkType = "ethernet";
                Check(client.GetDeviceInfo(2000).WakeMac == "00:11:22:33:44:66", "wired TV should wake on its wired address");
            }
            Check(Wake.ParseMac("00-11-22-33-44-55") != null && Wake.ParseMac("nope") == null, "MAC parsing");
        }

        // ---- turning off ----

        static void OffWhenShowingPc()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Fast(new Controller(SettingsFor(tv))).TurnOff(false, false);
                Check(!tv.IsOn, "TV still on");
                Check(tv.Count("POST /keypress/PowerOff") == 1, "expected one PowerOff");
                Check(LogText().Contains("off: sending power off"), "power off not logged before sending");
            }
        }

        static void OffLeavesOtherApp()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.App = "";
                Fast(new Controller(SettingsFor(tv))).TurnOff(false, false);
                Check(tv.IsOn && tv.Presses == 0, "should not touch a TV on its home screen");
                tv.App = "12";
                Fast(new Controller(SettingsFor(tv))).TurnOff(false, true);
                Check(tv.IsOn && tv.Presses == 0, "should not touch a TV showing an app");
            }
        }

        static void OffButtonForces()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.App = "12";
                Fast(new Controller(SettingsFor(tv))).TurnOff(true, false);
                Check(!tv.IsOn, "button should turn it off");
            }
        }

        // ---- turning on ----

        static void OnFromOff()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.PowerMode = "DisplayOff";
                tv.App = ""; // comes on to the home screen
                Fast(new Controller(SettingsFor(tv))).TurnOn(TurnOnMode.Follow);
                Check(tv.IsOn, "TV still off");
                Check(tv.App == "tvinput.hdmi1", "not switched to the PC, showing " + tv.App);
                Check(tv.Count("POST /keypress/InputHDMI1") >= 1, "no input press");
            }
        }

        static void OnLeavesOtherApp()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.App = "12";
                Fast(new Controller(SettingsFor(tv))).TurnOn(TurnOnMode.Follow);
                Check(tv.App == "12" && tv.Presses == 0, "should leave a busy TV alone");
            }
        }

        static void ToPcSwitches()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.App = "12";
                Fast(new Controller(SettingsFor(tv))).TurnOn(TurnOnMode.ToPc);
                Check(tv.App == "tvinput.hdmi1", "should switch to the PC");
            }
        }

        static void PowerOnlyDoesNotSwitch()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.PowerMode = "DisplayOff";
                tv.App = "";
                Fast(new Controller(SettingsFor(tv))).TurnOn(TurnOnMode.PowerOnly);
                Check(tv.IsOn && tv.App == "", "should power on and nothing else");
                Check(tv.Count("POST /keypress/PowerOn") == 1 && tv.Presses == 1, "expected exactly one press");
            }
        }

        static void LimitedIsLogged()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.ControlMode = "limited";
                tv.PowerMode = "DisplayOff";
                Fast(new Controller(SettingsFor(tv))).TurnOn(TurnOnMode.Follow);
                Check(!tv.IsOn, "a Limited TV can't be turned on");
                Check(LogText().Contains("-> 403"), "the 403 should be in the log");
                Check(LogText().Contains("gave up"), "should give up, not hang");
            }
        }

        static void WakesDeepStandby()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.Down = true;
                tv.PowerMode = "DisplayOff";
                var woken = new List<string>();
                Controller c = Fast(new Controller(SettingsFor(tv)));
                c.SendWake = mac => { woken.Add(mac); tv.Down = false; tv.PowerMode = "PowerOn"; return 2; };
                c.TurnOn(TurnOnMode.Follow);
                Check(woken.Count == 1 && woken[0] == "00:11:22:33:44:55", "expected one wake packet to the TV's address");
                Check(tv.IsOn && tv.App == "tvinput.hdmi1", "TV should be on and showing the PC");
                Check(LogText().Contains("TV woke up"), "wake-up not logged");
            }
        }

        static void FindsMovedTv()
        {
            using (var right = new FakeTv("127.0.0.2", 0))
            using (var wrong = new FakeTv("127.0.0.1", right.Port))
            {
                wrong.Serial = "X999999999";
                Settings s = SettingsFor(right);
                s.Ip = "127.0.0.3"; // nothing there any more
                Controller c = Fast(new Controller(s));
                c.Candidates = () => new List<string> { "127.0.0.3", "127.0.0.1", "127.0.0.2" };
                c.TurnOff(true, false);
                Check(!right.IsOn, "the right TV should be off");
                Check(wrong.IsOn && wrong.Presses == 0, "the other Roku must not be touched");
                Check(s.Ip == "127.0.0.2", "new address not used");
                Check(Settings.Load().Ip == "127.0.0.2", "new address not saved");
                File.Delete(Settings.FilePath);
            }
        }

        // ---- following rules ----

        class RulesProbe
        {
            public double Idle;
            public uint Tick = 100;
            public readonly List<bool> Wants = new List<bool>();
            public readonly FollowRules Rules = new FollowRules();

            public RulesProbe()
            {
                Rules.SecondsSinceInput = () => Idle;
                Rules.LastInputTick = () => Tick;
                Rules.Want = on => Wants.Add(on);
            }
        }

        static void RulesFreshInput()
        {
            var p = new RulesProbe();
            p.Idle = 0.4;
            p.Rules.MonitorsOn();
            Check(p.Wants.Count == 1 && p.Wants[0], "should want the TV on");
            p.Rules.MonitorsOff();
            Check(p.Wants.Count == 2 && !p.Wants[1], "should want the TV off");
        }

        static void RulesWaitForInput()
        {
            var p = new RulesProbe();
            p.Idle = 63;
            p.Rules.MonitorsOn();
            Check(p.Wants.Count == 0, "nobody there: should wait");
            p.Rules.CheckForInput();
            Check(p.Wants.Count == 0, "no input yet: should keep waiting");
            p.Tick = 200; // the mouse moved
            p.Rules.CheckForInput();
            Check(p.Wants.Count == 1 && p.Wants[0], "input: should want the TV on");
            p.Rules.CheckForInput();
            Check(p.Wants.Count == 1, "should only fire once");

            p.Rules.MonitorsOn();
            p.Rules.MonitorsOff(); // monitors went off again before anyone came back
            p.Tick = 300;
            p.Rules.CheckForInput();
            Check(p.Wants.Count == 2 && !p.Wants[1], "off should cancel the wait");
        }

        static void RulesPausedAndOptions()
        {
            var p = new RulesProbe();
            p.Rules.Paused = true;
            p.Rules.MonitorsOn();
            p.Rules.MonitorsOff();
            Check(p.Wants.Count == 0, "paused should do nothing");

            p = new RulesProbe();
            p.Rules.OnWithMonitors = false;
            p.Rules.MonitorsOn();
            p.Rules.MonitorsOff();
            Check(p.Wants.Count == 1 && !p.Wants[0], "on switched off: only the off should happen");

            p = new RulesProbe();
            p.Rules.OffWithMonitors = false;
            p.Rules.MonitorsOff();
            p.Rules.MonitorsOn();
            Check(p.Wants.Count == 1 && p.Wants[0], "off switched off: only the on should happen");
        }

        // ---- setup pieces ----

        static void InputsAreRead()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.InputNames = new[] { "PC", "HDMI 2" };
                List<KeyValuePair<string, string>> inputs = new RokuClient(tv.Ip, tv.Port).GetHdmiInputs(2000);
                Check(inputs != null && inputs.Count == 2, "expected the two HDMI inputs only");
                Check(inputs[0].Key == "tvinput.hdmi1" && inputs[0].Value == "PC", "first input wrong");
                Check(inputs[1].Key == "tvinput.hdmi2" && inputs[1].Value == "HDMI 2", "second input wrong (non-breaking space?)");
                tv.ControlMode = "limited";
                Check(new RokuClient(tv.Ip, tv.Port).GetHdmiInputs(2000) == null, "Limited hides the list");
            }
        }

        static void ScannerOrder()
        {
            using (var tv = new FakeTv("127.0.0.2", 0))
            using (var stick = new FakeTv("127.0.0.1", tv.Port))
            {
                stick.IsTv = false;
                stick.Name = "A stick";
                stick.Serial = "X000000002";
                List<FoundTv> found = TvScanner.Probe(new[] { "127.0.0.1", "127.0.0.2", "127.0.0.3" }, tv.Port);
                Check(found.Count == 2, "expected 2, found " + found.Count);
                Check(found[0].Info.IsTv && found[0].Ip == "127.0.0.2", "the TV should come first");
                Check(found[0].Title == "Test TV" && found[0].Details.Contains("Test room") && found[0].Details.Contains("127.0.0.2"), "title/details");
            }
        }

        static PowerTest FastTest()
        {
            var t = new PowerTest();
            t.OffWaitMs = 500;
            t.StandbyCheckMs = 30;
            t.WakeWaitMs = 400;
            t.OnWaitMs = 600;
            t.PollMs = 20;
            t.SendWake = mac => { throw new Exception("unexpected wake packet"); };
            return t;
        }

        static bool RunPowerTest(FakeTv tv, PowerTest test, List<string> lines)
        {
            return test.Run(new RokuClient(tv.Ip, tv.Port), "hdmi1", "00:11:22:33:44:55",
                (ok, text) => lines.Add((ok == null ? "." : ok.Value ? "+" : "-") + text));
        }

        static void PowerTestPasses()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.App = ""; // comes back on to the home screen, so it has to switch
                var lines = new List<string>();
                Check(RunPowerTest(tv, FastTest(), lines), "test failed: " + string.Join(" | ", lines));
                Check(tv.IsOn && tv.App == "tvinput.hdmi1", "TV should end on and showing the PC");
                Check(lines.Exists(l => l.StartsWith("+It still answers")), "standby answer not reported");
                Check(lines[lines.Count - 1] == "+Back on and switched to HDMI 1. Check that this PC's screen is on the TV.", "last line: " + lines[lines.Count - 1]);
            }
        }

        static void PowerTestLimited()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.ControlMode = "limited";
                var lines = new List<string>();
                Check(!RunPowerTest(tv, FastTest(), lines), "should fail");
                Check(tv.IsOn, "TV should be untouched");
                Check(lines[lines.Count - 1].Contains("Limited"), "should name the setting: " + lines[lines.Count - 1]);
            }
        }

        static void PowerTestWakes()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.DownWhenOff = true;
                PowerTest test = FastTest();
                var woken = new List<string>();
                test.SendWake = mac => { woken.Add(mac); tv.Down = false; tv.PowerMode = "PowerOn"; return 2; };
                var lines = new List<string>();
                Check(RunPowerTest(tv, test, lines), "test failed: " + string.Join(" | ", lines));
                Check(woken.Count == 1, "expected one wake packet");
                Check(lines.Exists(l => l == "+The wake signal reached it."), "wake not reported");
            }
        }

        static void PowerTestUnreachable()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.DownWhenOff = true;
                PowerTest test = FastTest();
                test.SendWake = mac => 2;
                var lines = new List<string>();
                Check(!RunPowerTest(tv, test, lines), "should fail");
                Check(lines[lines.Count - 1].Contains("Fast TV start"), "should explain Fast TV start: " + lines[lines.Count - 1]);
            }
        }

        static void InstanceNames()
        {
            Check(!Settings.IsDefaultDir, "tests use their own folder");
            Check(Instance.MutexName.StartsWith(@"Local\TVFollow.") && Instance.QuitName.StartsWith(@"Local\TVFollow.Quit."),
                "names should carry a suffix: " + Instance.MutexName);
            Check(!Instance.IsRunning(), "nothing should be running for the test folder");
        }

        // ---- installing ----

        static void InstallAndUninstall()
        {
            string root = Path.Combine(Settings.Dir, "install-test");
            string registry = @"Software\TVFollowTests\" + Guid.NewGuid().ToString("N");
            var p = new InstallPaths
            {
                Dir = Path.Combine(root, "TVFollow"),
                StartMenuLink = Path.Combine(root, "Start menu", "TVFollow.lnk"),
                DesktopDir = Path.Combine(root, "Desktop"),
                RegistryRoot = registry
            };
            try
            {
                var s = new Settings();
                s.Ip = "192.168.1.50";
                s.Serial = "X000000001";
                s.DesktopShortcuts = true;
                s.Save();
                Installer.Install(p, s, System.Reflection.Assembly.GetExecutingAssembly().Location);

                Check(File.Exists(p.Exe), "exe not copied");
                Check(Directory.GetFiles(p.IconDir, "*.ico").Length == 5, "icons not written");
                Check(Shortcut.Read(p.StartMenuLink)[0] == p.Exe, "Start menu shortcut wrong");
                string[] on = Shortcut.Read(Path.Combine(p.DesktopDir, "TV on.lnk"));
                Check(on[0] == p.Exe && on[1] == "on", "TV on shortcut wrong: " + on[0] + " " + on[1]);
                Check(File.Exists(Path.Combine(p.DesktopDir, "TV to PC.lnk")), "TV to PC shortcut missing");
                using (var run = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(p.RunKey))
                    Check(run != null && (string)run.GetValue(Installer.RunValue) == "\"" + p.Exe + "\" --startup", "start-with-Windows not set");
                using (var app = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(p.UninstallKey))
                {
                    Check(app != null && (string)app.GetValue("DisplayName") == "TVFollow", "Apps entry missing");
                    Check((string)app.GetValue("UninstallString") == "\"" + p.Exe + "\" uninstall", "uninstall command wrong");
                    Check((string)app.GetValue("Publisher") == "b0r3d.org" && (string)app.GetValue("DisplayVersion") == Program.Version, "publisher/version");
                }

                // change the options: shortcuts and start-with-Windows go away
                s.DesktopShortcuts = false;
                s.StartWithWindows = false;
                Installer.Install(p, s, p.Exe);
                Check(!File.Exists(Path.Combine(p.DesktopDir, "TV on.lnk")), "desktop shortcut not removed");
                using (var run = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(p.RunKey))
                    Check(run == null || run.GetValue(Installer.RunValue) == null, "start-with-Windows not removed");

                Updates.Save(DateTime.UtcNow, "", "");
                Installer.Uninstall(p, Settings.Dir);
                Check(!File.Exists(Updates.FilePath), "update-check file left behind");
                Check(!Directory.Exists(p.Dir), "program folder left behind");
                Check(!File.Exists(p.StartMenuLink), "Start menu shortcut left behind");
                Check(!File.Exists(Settings.FilePath), "settings left behind");
                using (var app = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(p.UninstallKey))
                    Check(app == null, "Apps entry left behind");
            }
            finally
            {
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\TVFollowTests", false);
                try { Directory.Delete(root, true); } catch { }
            }
        }

        static void TrialInstallPaths()
        {
            InstallPaths p = InstallPaths.ForThisPC();
            Check(p.RegistryRoot == null, "a trial copy must not touch the registry");
            Check(p.Dir.StartsWith(Settings.Dir) && p.StartMenuLink.StartsWith(Settings.Dir) && p.DesktopDir.StartsWith(Settings.Dir),
                "a trial copy must keep inside its folder");
        }

        static void IconsAreBuiltIn()
        {
            foreach (string name in new[] { "tv.ico", "tv-paused.ico", "tv-on.ico", "tv-off.ico", "tv-pc.ico" })
            {
                using (System.Drawing.Icon icon = AppIcons.Load(name, new System.Drawing.Size(16, 16)))
                    Check(icon.Width == 16, name + " has no 16 px picture");
            }
        }

        // ---- update check ----

        static void UpdateVersions()
        {
            Check(Updates.IsNewer("1.1", "1.0") && Updates.IsNewer("v1.0.1", "1.0") && Updates.IsNewer("1.10", "1.9"), "newer not seen");
            Check(!Updates.IsNewer("v1.0", "1.0") && !Updates.IsNewer("1.0.0", "1.0") && !Updates.IsNewer("0.9", "1.0"), "same or older counted as newer");
            Check(!Updates.IsNewer("", "1.0") && !Updates.IsNewer("1.1-beta", "1.0") && !Updates.IsNewer(null, "1.0"), "nonsense counted as newer");
        }

        static void UpdateParsingAndTiming()
        {
            Release r = Release.Parse("{\"url\":\"https://api.github.com/x\",\"html_url\":\"https://github.com/still-b0r3d/TVFollow/releases/tag/v1.1\"," +
                "\"id\":1,\"author\":{\"login\":\"someone\",\"html_url\":\"https://github.com/someone\"},\"tag_name\":\"v1.1\",\"draft\":false," +
                "\"prerelease\":false,\"assets\":[{\"name\":\"TVFollow.exe\"}],\"body\":\"What's new\"}");
            Check(r.Tag == "v1.1" && r.Version == "1.1", "tag read wrong: " + r.Tag);
            Check(r.Url == "https://github.com/still-b0r3d/TVFollow/releases/tag/v1.1", "picked the wrong html_url: " + r.Url);
            Check(!r.Draft && !r.Prerelease, "flags read wrong");
            Check(Updates.IsOurPage(r.Url), "own release page refused");
            Check(!Updates.IsOurPage("https://example.com/TVFollow/releases/") && !Updates.IsOurPage("https://github.com/still-b0r3d/TVFollowX/releases/"),
                "a stranger's page would be opened");

            DateTime now = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
            Check(Updates.IsDue(DateTime.MinValue, now), "never checked: due");
            Check(!Updates.IsDue(now.AddDays(-6), now), "six days ago: not due yet");
            Check(Updates.IsDue(now.AddDays(-7), now), "a week ago: due");
            Check(Updates.IsDue(now.AddDays(3), now), "a clock set back: due");
        }

        static void UpdateState()
        {
            DateTime when = new DateTime(2026, 11, 1, 12, 30, 0, DateTimeKind.Utc);
            Updates.Save(when, "1.1", "https://github.com/still-b0r3d/TVFollow/releases/tag/v1.1");
            DateTime last;
            string latest, url;
            Updates.Load(out last, out latest, out url);
            Check(last == when && last.Kind == DateTimeKind.Utc, "time came back as " + last.ToString("o"));
            Check(latest == "1.1" && url.EndsWith("/v1.1"), "version or link lost");
            File.Delete(Updates.FilePath);
            Updates.Load(out last, out latest, out url);
            Check(last == DateTime.MinValue && latest == "", "no file should mean never checked");
        }

        static void VersionMatches()
        {
            var attr = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                typeof(Program).Assembly, typeof(AssemblyInformationalVersionAttribute));
            Check(attr != null && attr.InformationalVersion == Program.Version,
                "AssemblyInfo.cs says " + (attr == null ? "nothing" : attr.InformationalVersion) + ", Program.Version says " + Program.Version);
        }
    }
}
