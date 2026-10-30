using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow.Tests
{
    // Tests for the fixes from the 2026-10-23 review.
    static partial class TestMain
    {
        static void RunReviewFixes()
        {
            Run("a reply with a DOCTYPE is refused and reads no file", XmlRefusesDoctype);
            Run("a reply that trickles in is cut off at the deadline", TrickleIsCutOff);
            Run("an oversized reply is refused", HugeReplyRefused);
            Run("off the TV's network: no wake packet, no search", OffNetworkLeavesItAlone);
            Run("a moved TV is searched for at most every ten minutes", SearchIsRateLimited);
            Run("the wake packet is sent again while waiting", WakeIsRepeated);
            Run("no network: the log says no wake packet went out", WakeWithNoNetworkIsLogged);
            Run("the TV's wake address follows it to a cable", WakeMacIsRefreshed);
            Run("network maths: contains, broadcast, addresses to try", NetworkMaths);
            Run("settings: pause is kept and trailing comments are ignored", SettingsPauseAndComments);
            Run("rules: undimming isn't the monitors coming back", RulesIgnoreUndim);
            Run("opening TVFollow: when setup shows", SetupShowsWhen);
            Run("install: the downloaded-from-internet mark is dropped", InstallDropsDownloadMark);
            Run("install: a busy exe is retried", InstallRetriesBusyExe);
            Run("uninstall: the folder is removed after exit, odd names and all", DeleteAfterExitWorks);
            Run("updates: a locked updates.ini doesn't crash", UpdateFileLocked);
            Run("scanner: the saved address is tried and duplicates dropped", ScannerSavedAndDuplicates);
            Run("power test: a TV that's already off passes", PowerTestAlreadyOff);
            Run("power test: a TV that won't turn back on fails cleanly", PowerTestNoPowerOn);
            Run("power test: an input that won't switch fails cleanly", PowerTestNoInput);
            Run("portable: where the settings live", PortableSettingsPlace);
            Run("portable: one copy follows, installed or portable", PortableSharesTheLock);
            Run("portable: refused from the install folder, Temp, or a read-only folder", PortableBlockers);
            Run("portable: uninstalling keeps the user's exe and folder", PortableUninstallKeepsExe);

            // the setup window, driven off screen
            WindowsFormsSynchronizationContext.AutoInstall = false;
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            Run("setup: the footer buttons tab in screen order", SetupTabOrder);
            Run("setup: walking through and Finish saves the TV", SetupWalkThrough);
            Run("setup: a late answer about another TV is ignored", SetupIgnoresStaleCheck);
            Run("setup: choosing portable", SetupChoosesPortable);
            RunRemoteTests();
            RunSecondReview();
            RunMultiTv();
        }

        // ---- portable ----

        static void PortableSettingsPlace()
        {
            Check(Settings.PickDir("C:\\trial", "D:\\Tools\\TVFollow data", true) == "C:\\trial", "a trial folder wins");
            Check(Settings.PickDir(null, "D:\\Tools\\TVFollow data", true) == "D:\\Tools\\TVFollow data", "a data folder next to the exe means portable");
            Check(Settings.PickDir("", "D:\\Tools\\TVFollow data", false) == Settings.DefaultDir, "otherwise the installed place");
            Check(Settings.PortableDir == Path.Combine(Settings.ExeDir, "TVFollow data"), "portable folder name");
        }

        static void PortableSharesTheLock()
        {
            Check(Instance.SuffixFor(false, "D:\\Tools\\TVFollow data") == "", "portable must share the installed copy's lock");
            Check(Instance.SuffixFor(false, Settings.DefaultDir) == "", "installed uses the plain name");
            Check(Instance.SuffixFor(true, "C:\\trial").StartsWith("."), "a trial copy keeps to itself");
        }

        static void PortableBlockers()
        {
            string temp = Path.GetTempPath();
            string install = Path.Combine(Settings.Dir, "inst", "TVFollow");
            Directory.CreateDirectory(install);
            Check(Settings.PortableBlocker(install, install, temp) != null, "allowed in the install folder");
            Check(Settings.PortableBlocker(Path.Combine(temp, "Temp1_TVFollow.zip"), install, temp) != null, "allowed in Temp");
            string own = Path.Combine(Settings.Dir, "own folder");
            Directory.CreateDirectory(own);
            Check(Settings.PortableBlocker(own, install, Path.Combine(Settings.Dir, "elsewhere")) == null, "refused in a normal folder");
            Check(Directory.GetFiles(own).Length == 0, "the write check left a file behind");
            Check(Settings.PortableBlocker(Path.Combine(Settings.Dir, "missing"), install, Path.Combine(Settings.Dir, "elsewhere")) != null,
                "allowed where it can't write");
            Directory.Delete(Path.Combine(Settings.Dir, "inst"), true);
            Directory.Delete(own, true);
        }

        static void PortableUninstallKeepsExe()
        {
            string folder = Path.Combine(Settings.Dir, "TVFollow");   // even a folder named TVFollow is the user's
            string data = Path.Combine(folder, Settings.PortableFolderName);
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(folder, "TVFollow.exe"), "x");
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "the user's");
            File.WriteAllText(Path.Combine(data, "settings.ini"), "ip=1.2.3.4");
            File.WriteAllText(Path.Combine(data, "TVFollow.log"), "log");
            bool later = Installer.Uninstall(new InstallPaths { Dir = folder, Portable = true }, data);
            Check(!later, "nothing should be scheduled for deletion");
            Check(File.Exists(Path.Combine(folder, "TVFollow.exe")) && File.Exists(Path.Combine(folder, "notes.txt")), "the user's files were deleted");
            Check(!Directory.Exists(data), "the data folder is still there");
            Directory.Delete(folder, true);
        }

        static void SetupChoosesPortable()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                var s = new Settings();
                s.Port = tv.Port;
                SetupForm form = OffScreenSetup(s, () => TvScanner.Probe(new[] { "127.0.0.1" }, tv.Port));
                string trialDir = Settings.Dir;
                try
                {
                    Pump(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext, 15);
                    form.GoNext();
                    Pump(() => form.CurrentPage.CanGoNext, 15);
                    form.GoNext();
                    Pump(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext, 15);
                    form.GoNext();
                    form.GoNext(); // past the test: skipping isn't allowed, so this stays on the test page
                    Check(form.CurrentPage is TestPage, "Next went past an unrun test");
                    ((TestPage)form.CurrentPage).StartTest();
                    Pump(() => !form.CurrentPage.Busy, 20);
                    form.GoNext();
                    Check(form.CurrentPage is FinishPage, "not on the last page");
                    Check(!form.Portable, "install should be the default");
                    RadioButton portable = null;
                    foreach (Control c in ((FinishPage)form.CurrentPage).Controls[0].Controls)
                        if (c is RadioButton && c.Text.StartsWith("Portable")) portable = (RadioButton)c;
                    Check(portable != null && portable.Enabled, "the portable choice is missing or greyed out");
                    portable.Checked = true;
                    Check(form.Portable, "choosing portable didn't register");
                    form.GoNext();
                    Check(form.DialogResult == DialogResult.OK, "Finish didn't close with OK");
                    Check(Settings.Dir == trialDir, "a trial copy must keep its own folder even when portable is picked");
                }
                finally { form.Dispose(); }
                File.Delete(Settings.FilePath);
            }
        }

        // ---- the TV client ----

        static void XmlRefusesDoctype()
        {
            string secret = Path.Combine(Settings.Dir, "secret.txt");
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(secret, "PRETEND-SECRET");
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.RawDeviceInfo = "<?xml version=\"1.0\"?><!DOCTYPE d [<!ENTITY x SYSTEM \"file:///" + secret.Replace('\\', '/') + "\">]>" +
                    "<device-info><serial-number>&x;</serial-number><power-mode>PowerOn</power-mode></device-info>";
                DeviceInfo info = new RokuClient(tv.Ip, tv.Port).GetDeviceInfo(2000);
                Check(info == null, "a DOCTYPE reply was accepted" + (info != null && info.Serial.Contains("PRETEND") ? " AND THE FILE WAS READ" : ""));
            }
            File.Delete(secret);
        }

        static void TrickleIsCutOff()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.TrickleMs = 4000;
                var watch = Stopwatch.StartNew();
                DeviceInfo info = new RokuClient(tv.Ip, tv.Port).GetDeviceInfo(300);
                Check(info == null, "a 4-second trickle got through a 300 ms deadline");
                Check(watch.ElapsedMilliseconds < 2500, "took " + watch.ElapsedMilliseconds + " ms");
            }
        }

        static void HugeReplyRefused()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.PadBytes = RokuClient.MaxReply + 1000;
                Check(new RokuClient(tv.Ip, tv.Port).GetDeviceInfo(5000) == null, "an oversized reply was accepted");
                tv.PadBytes = 1000;
                Check(new RokuClient(tv.Ip, tv.Port).GetDeviceInfo(5000) != null, "a normal-sized reply was refused");
            }
        }

        // ---- searching and waking ----

        static void OffNetworkLeavesItAlone()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.Down = true;
                Controller c = Fast(FirstTv(SettingsFor(tv)));
                c.OnTvNetwork = () => false;
                c.Candidates = () => { throw new Exception("searched off the TV's network"); };
                c.TurnOn(TurnOnMode.Follow);
                c.TurnOff(false, false);
                Check(LogText().Contains("isn't on the TV's network"), "not logged");
            }
        }

        static void SearchIsRateLimited()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.Down = true;
                Controller c = Fast(FirstTv(SettingsFor(tv)));
                c.SearchEvery = TimeSpan.FromMinutes(10);
                int searches = 0;
                c.Candidates = () => { searches++; return new List<string>(); };
                c.TurnOff(false, false);
                c.TurnOff(false, false);
                Check(searches == 1, "searched " + searches + " times");
                Check(LogText().Contains("not searching again yet"), "the skip isn't logged");
            }
        }

        static void WakeIsRepeated()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.Down = true;
                Controller c = Fast(FirstTv(SettingsFor(tv)));
                int sent = 0;
                c.SendWake = mac => { sent++; return 2; };
                c.WakeReachMs = 500;
                c.WakeRepeatMs = 100;
                c.TurnOn(TurnOnMode.Follow);
                Check(sent >= 3, "only " + sent + " wake packets in half a second");
                Check(LogText().Contains("sent a wake packet (2 addresses)"), "the count isn't logged");
            }
        }

        static void WakeWithNoNetworkIsLogged()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.Down = true;
                Controller c = Fast(FirstTv(SettingsFor(tv)));
                c.SendWake = mac => 0;
                c.TurnOn(TurnOnMode.Follow);
                Check(LogText().Contains("no network to send a wake packet on"), "a wake that never went out isn't logged");
            }
        }

        static void WakeMacIsRefreshed()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings s = SettingsFor(tv);
                s.Save();
                tv.NetworkType = "ethernet";
                Fast(FirstTv(s)).TurnOff(false, false);
                Check(Tv(s).WakeMac == "00:11:22:33:44:66", "wake address not updated: " + Tv(s).WakeMac);
                Check(Tv(Settings.Load()).WakeMac == "00:11:22:33:44:66", "new wake address not saved");
                File.Delete(Settings.FilePath);
            }
        }

        static void NetworkMaths()
        {
            var home = new LocalNetwork();
            LocalNetwork.TryParse("192.168.1.20", out home.Address);
            LocalNetwork.TryParse("255.255.255.0", out home.Mask);
            Check(home.Contains("192.168.1.50") && !home.Contains("192.168.2.50") && !home.Contains("nonsense"), "contains /24");
            Check(home.Broadcast == "192.168.1.255", "broadcast /24: " + home.Broadcast);
            List<string> hosts = home.Hosts(null);
            Check(hosts.Count == 254 && hosts[0] == "192.168.1.1" && hosts[253] == "192.168.1.254", "hosts /24");

            var bigger = new LocalNetwork();
            LocalNetwork.TryParse("10.0.5.9", out bigger.Address);
            LocalNetwork.TryParse("255.255.252.0", out bigger.Mask);
            Check(bigger.Broadcast == "10.0.7.255", "broadcast /22: " + bigger.Broadcast);
            Check(bigger.Contains("10.0.4.1") && bigger.Contains("10.0.7.254") && !bigger.Contains("10.0.8.1"), "contains /22");
            Check(bigger.Hosts(null).Count == 1022, "hosts /22: " + bigger.Hosts(null).Count);

            var huge = new LocalNetwork();
            LocalNetwork.TryParse("172.16.40.9", out huge.Address);
            LocalNetwork.TryParse("255.255.0.0", out huge.Mask);
            List<string> near = huge.Hosts("172.16.99.7");
            Check(near.Count == 254 && near[0] == "172.16.99.1", "a /16 should only try the 254 around the TV");
            Check(huge.Hosts("10.1.1.1")[0] == "172.16.40.1", "an address off the network falls back to around the PC");
        }

        // ---- settings, rules, startup ----

        static void SettingsPauseAndComments()
        {
            var s = new Settings();
            Tv(s).Ip = "192.168.1.50";
            Tv(s).Serial = "X000000001";
            s.Paused = true;
            s.Save();
            Check(Settings.Load().Paused, "pause not kept");
            File.WriteAllText(Settings.FilePath, "ip=192.168.1.50   # the TV\r\nserial=X000000001\t# its serial\r\ninput=hdmi2 # where the PC is\r\n");
            Settings t = Settings.Load();
            Check(Tv(t).Ip == "192.168.1.50" && Tv(t).Serial == "X000000001" && Tv(t).Input == "hdmi2", "comment kept: [" + Tv(t).Ip + "] [" + Tv(t).Serial + "] [" + Tv(t).Input + "]");
            Check(!t.Paused, "paused should default to off");
            File.Delete(Settings.FilePath);
        }

        static void RulesIgnoreUndim()
        {
            var p = new RulesProbe();
            p.Idle = 0.2;
            p.Rules.Display(1);
            p.Rules.Display(2);
            p.Rules.Display(1);
            Check(p.Wants.Count == 1, "undimming counted as the monitors coming on");
            p.Rules.Display(0);
            p.Rules.Display(1);
            Check(p.Wants.Count == 3 && !p.Wants[1] && p.Wants[2], "off then on should still work");
            p.Rules.Display(2);
            p.Rules.Resumed();
            p.Rules.Display(1);
            Check(p.Wants.Count == 4 && p.Wants[3], "after sleep the next 'on' must count");
        }

        static void SetupShowsWhen()
        {
            Check(Program.WhatOpens("", false, false, true) == Program.Opening.Setup, "first run: setup");
            Check(Program.WhatOpens("", true, true, true) == Program.Opening.Settings, "opened again while running: Settings");
            Check(Program.WhatOpens("", true, false, false) == Program.Opening.Setup, "a new download, nothing running: setup");
            Check(Program.WhatOpens("", true, true, false) == Program.Opening.Setup, "a new download while running: setup (to update)");
            Check(Program.WhatOpens("", true, false, true) == Program.Opening.Follow, "home, not running: just follow");
            Check(Program.WhatOpens("setup", true, true, true) == Program.Opening.Setup, "the setup command");
            Check(Program.WhatOpens("--startup", true, false, true) == Program.Opening.Follow &&
                Program.WhatOpens("follow", true, true, false) == Program.Opening.Follow, "internal commands never open windows");
        }

        // ---- installing ----

        static InstallPaths TestPaths(string root)
        {
            return new InstallPaths
            {
                Dir = Path.Combine(root, "TVFollow"),
                StartMenuLink = Path.Combine(root, "Start menu", "TVFollow.lnk"),
                DesktopDir = Path.Combine(root, "Desktop")
            };
        }

        static void InstallDropsDownloadMark()
        {
            string root = Path.Combine(Settings.Dir, "mark-test");
            Directory.CreateDirectory(root);
            string source = Path.Combine(root, "downloaded.exe");
            File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location, source);
            // mark it the way a browser does
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/d /c echo [ZoneTransfer]>\"" + source + ":Zone.Identifier\"")
            { UseShellExecute = false, CreateNoWindow = true };
            Process.Start(psi).WaitForExit();
            Check(Installer.HasDownloadMark(source), "couldn't mark the test file");
            InstallPaths p = TestPaths(root);
            Installer.Install(p, new Settings(), source);
            Check(!Installer.HasDownloadMark(p.Exe), "the installed exe kept the downloaded mark");
            Directory.Delete(root, true);
        }

        static void InstallRetriesBusyExe()
        {
            string root = Path.Combine(Settings.Dir, "busy-test");
            InstallPaths p = TestPaths(root);
            Installer.Install(p, new Settings(), System.Reflection.Assembly.GetExecutingAssembly().Location);
            var held = new FileStream(p.Exe, FileMode.Open, FileAccess.Read, FileShare.None);
            var release = new Thread(() => { Thread.Sleep(700); held.Dispose(); });
            release.Start();
            Installer.Install(p, new Settings(), System.Reflection.Assembly.GetExecutingAssembly().Location);
            release.Join();
            Check(File.Exists(p.Exe), "exe missing after the retry");
            Directory.Delete(root, true);
        }

        static void DeleteAfterExitWorks()
        {
            string dir = Path.Combine(Settings.Dir, "odd name & %PATH% (1)");
            string neighbour = Path.Combine(Settings.Dir, "neighbour.txt");
            Directory.CreateDirectory(Path.Combine(dir, "icons"));
            File.WriteAllText(Path.Combine(dir, "TVFollow.exe"), "x");
            File.WriteAllText(neighbour, "keep me");
            Installer.DeleteAfterExit(dir);
            DateTime until = DateTime.UtcNow.AddSeconds(10);
            while (Directory.Exists(dir) && DateTime.UtcNow < until) Thread.Sleep(200);
            Check(!Directory.Exists(dir), "the folder is still there");
            Check(File.Exists(neighbour), "something next to it was deleted");
            File.Delete(neighbour);
        }

        static void UpdateFileLocked()
        {
            Updates.Save(DateTime.UtcNow, "1.1", "https://github.com/still-b0r3d/TVFollow/releases/tag/v1.1");
            using (new FileStream(Updates.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                DateTime last;
                string latest, url;
                Updates.Load(out last, out latest, out url);
                Check(last == DateTime.MinValue && latest == "", "a locked file should read as never checked");
                Updates.Save(DateTime.UtcNow, "", ""); // must not throw
            }
            File.Delete(Updates.FilePath);
        }

        static void ScannerSavedAndDuplicates()
        {
            List<string> all = TvScanner.WithSaved(new List<string> { "192.168.1.5" }, new[] { "10.0.0.7" });
            Check(all.Count == 2 && all[1] == "10.0.0.7", "saved address not added");
            Check(TvScanner.WithSaved(new List<string> { "10.0.0.7" }, new[] { "10.0.0.7" }).Count == 1, "saved address added twice");
            Check(TvScanner.WithSaved(new List<string> { "10.0.0.7" }, new[] { "10.0.0.8", "", "10.0.0.9" }).Count == 3, "every saved TV's address is tried");
            Check(TvScanner.WithSaved(new List<string>(), null).Count == 0, "no saved address");
            using (var a = new FakeTv("127.0.0.1", 0))
            using (var b = new FakeTv("127.0.0.2", a.Port))
            {
                // the same TV answering at two addresses (its saved one and a new one)
                List<FoundTv> found = TvScanner.Probe(new[] { "127.0.0.1", "127.0.0.2" }, a.Port);
                Check(found.Count == 1, "the same TV listed " + found.Count + " times");
            }
        }

        // ---- the power test ----

        static void PowerTestAlreadyOff()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.PowerMode = "DisplayOff";
                var lines = new List<string>();
                Check(RunPowerTest(tv, FastTest(), lines), "failed: " + string.Join(" | ", lines));
                Check(tv.IsOn && tv.App == "tvinput.hdmi1", "should end on and switched");
            }
        }

        static void PowerTestNoPowerOn()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.RefusePowerOn = true;
                var lines = new List<string>();
                Check(!RunPowerTest(tv, FastTest(), lines), "should fail");
                Check(lines[lines.Count - 1].Contains("didn't turn back on") && lines[lines.Count - 1].Contains("remote"), "last line: " + lines[lines.Count - 1]);
            }
        }

        static void PowerTestNoInput()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.App = "";
                tv.IgnoreInput = true;
                var lines = new List<string>();
                Check(!RunPowerTest(tv, FastTest(), lines), "should fail");
                Check(lines[lines.Count - 1].Contains("didn't switch"), "last line: " + lines[lines.Count - 1]);
            }
        }

        // ---- the setup window ----

        static void Pump(Func<bool> done, int seconds)
        {
            DateTime until = DateTime.UtcNow.AddSeconds(seconds);
            while (!done())
            {
                if (DateTime.UtcNow > until) throw new TimeoutException("the setup window got stuck");
                Application.DoEvents();
                Thread.Sleep(20);
            }
        }

        static string AllText(Control c)
        {
            string text = c.Text;
            foreach (Control child in c.Controls) if (child.Visible) text += "\n" + AllText(child);
            return text;
        }

        static SetupForm OffScreenSetup(Settings s, Func<List<FoundTv>> scan)
        {
            var form = new SetupForm(s);
            form.Scan = scan;
            form.Test.OffWaitMs = 1000;
            form.Test.StandbyCheckMs = 30;
            form.Test.PollMs = 20;
            form.Test.SendWake = mac => 0;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(-20000, -20000);
            form.ShowInTaskbar = false;
            form.Show();
            return form;
        }

        static void SetupTabOrder()
        {
            using (var form = new SetupForm(new Settings()))
            {
                Button[] b = form.FooterButtons;
                Check(b[0].Text == "Back" && b[2].Text == "Cancel", "footer buttons");
                Check(b[0].TabIndex < b[1].TabIndex && b[1].TabIndex < b[2].TabIndex, "tab order isn't Back, Next, Cancel");
            }
        }

        static void SetupWalkThrough()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.App = "tvinput.hdmi2";
                var s = new Settings();
                s.Port = tv.Port;
                SetupForm form = OffScreenSetup(s, () => TvScanner.Probe(new[] { "127.0.0.1" }, tv.Port));
                try
                {
                    Pump(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext, 15);
                    form.GoNext();                                   // to: control
                    Pump(() => form.CurrentPage.CanGoNext, 15);
                    form.GoNext();                                   // to: input (the TV is on HDMI 2)
                    Pump(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext, 15);
                    form.GoNext();                                   // to: test
                    ((TestPage)form.CurrentPage).StartTest();
                    Pump(() => !form.CurrentPage.Busy, 20);
                    Check(form.CurrentPage.CanGoNext, "the test didn't pass: " + AllText(form.CurrentPage));
                    form.GoNext();                                   // to: finish
                    form.GoNext();                                   // Finish
                    Check(form.DialogResult == DialogResult.OK, "Finish didn't close with OK");
                }
                finally { form.Dispose(); }
                Settings saved = Settings.Load();
                Check(Tv(saved).Ip == "127.0.0.1" && Tv(saved).Serial == tv.Serial, "TV not saved: " + Tv(saved).Ip + " " + Tv(saved).Serial);
                Check(Tv(saved).Input == "hdmi2", "input not saved: " + Tv(saved).Input);
                Check(Tv(saved).WakeMac == "00:11:22:33:44:55", "wake address not saved: " + Tv(saved).WakeMac);
                Check(saved.Tvs.Count == 1 && Tv(saved).Name == "Test TV", "TVs saved: " + saved.Tvs.Count + ", name " + Tv(saved).Name);
                File.Delete(Settings.FilePath);
            }
        }

        static void SetupIgnoresStaleCheck()
        {
            using (var slow = new FakeTv("127.0.0.1", 0))
            using (var limited = new FakeTv("127.0.0.2", slow.Port))
            {
                slow.DelayMs = 1500;              // answers late, and says "allowed"
                limited.Serial = "X000000002";
                limited.ControlMode = "limited";
                var s = new Settings();
                s.Port = slow.Port;
                Tv(s).Serial = slow.Serial;           // so step 1 picks the slow TV
                SetupForm form = OffScreenSetup(s, () => TvScanner.Probe(new[] { "127.0.0.1", "127.0.0.2" }, slow.Port));
                try
                {
                    Pump(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext, 20);
                    form.GoNext();                // step 2 asks the slow TV...
                    Thread.Sleep(100);
                    form.GoBack();                // ...but the user goes back
                    List<FoundTv> found = TvScanner.Probe(new[] { "127.0.0.2" }, slow.Port);
                    form.Pick(found[0]);          // and ticks the other TV instead
                    form.GoNext();
                    Pump(() => AllText(form.CurrentPage).Contains("Limited"), 10);
                    Thread.Sleep(2000);           // the slow TV's late "allowed" arrives now
                    Pump(() => true, 1);
                    Check(!form.CurrentPage.CanGoNext, "the other TV's late answer unlocked Next");
                    Check(AllText(form.CurrentPage).Contains("Limited"), "status lost: " + AllText(form.CurrentPage));
                }
                finally { form.Dispose(); }
            }
        }
    }
}
