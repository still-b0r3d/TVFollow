using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow.Tests
{
    // Several TVs (1.2): the settings, the following, the commands, the tray, the remote, the
    // Settings window and setup, against two or three pretend TVs.
    static partial class TestMain
    {
        static void RunMultiTv()
        {
            Run("several TVs: saved and read back, the first in 1.1's keys", SeveralTvsRoundTrip);
            Run("several TVs: a 1.1 file, a TV listed twice, a TV that isn't complete", SeveralTvsOddFiles);
            Run("several TVs: names, numbers, and numbers for two with the same name", SeveralTvsLabels);
            Run("several TVs follow the monitors at once: a slow one doesn't hold up the other", SeveralTvsFollowAtOnce);
            Run("several TVs: sleep turns them all off at once, and pause leaves them all", SeveralTvsQuickOff);
            Run("commands: every TV, or one by its number or name", CommandPicksTvs);
            Run("tray: All TVs buttons and a menu for each TV", TrayWithSeveralTvs);
            Run("remote: picks the TV; a press goes to the TV it was pressed for", RemotePicksTv);
            Run("settings window: a part for each TV, each with its own input", SettingsWindowSeveralTvs);
            Run("setup: Next waits for a tick when several TVs are found", SetupWaitsForTick);
            Run("setup: two TVs ticked go through the steps one after the other", SetupTwoTvs);
            Run("setup: adding a TV keeps the one set up before as it was", SetupAddsTv);
            Run("setup: a TV set up before that doesn't answer stays, until unticked", SetupKeepsSilentTv);
            Run("setup: the box sends TVs set up before through the steps again", SetupRedoesTv);
        }

        // two pretend TVs on loopback, the second at .2 on the same port
        static Settings TwoTvs(FakeTv a, FakeTv b)
        {
            var s = new Settings();
            s.Port = a.Port;
            s.Tvs.Add(new TvSettings { Ip = a.Ip, Serial = a.Serial, Input = "hdmi1", WakeMac = "00:11:22:33:44:55", Name = "Living Room TV" });
            s.Tvs.Add(new TvSettings { Ip = b.Ip, Serial = b.Serial, Input = "hdmi2", WakeMac = "00:11:22:33:44:55", Name = "Office TV" });
            return s;
        }

        static List<Controller> FastTvs(Settings s)
        {
            List<Controller> tvs = Controller.For(s);
            foreach (Controller c in tvs) Fast(c);
            return tvs;
        }

        // ---- settings ----

        static void SeveralTvsRoundTrip()
        {
            var s = new Settings();
            s.Tvs.Add(new TvSettings { Ip = "192.168.1.50", Serial = "X1", Input = "hdmi1", WakeMac = "00:11:22:33:44:01", GatewayMac = "AA:BB:CC:DD:EE:01", Name = "Living Room TV" });
            s.Tvs.Add(new TvSettings { Ip = "192.168.1.51", Serial = "X2", Input = "hdmi2", WakeMac = "00:11:22:33:44:02", Name = "Den #2" });
            s.Tvs.Add(new TvSettings { Ip = "192.168.1.52", Serial = "X3", Input = "hdmi3", Name = "Tom & Jerry" });
            s.Paused = true;
            s.Save();
            string text = File.ReadAllText(Settings.FilePath);
            // what a 1.1 copy reads: the plain keys, once each, all the first TV's
            Check(Lines(text, "ip=").Count == 1 && Lines(text, "ip=")[0] == "ip=192.168.1.50", "the first TV isn't in 1.1's keys");
            Check(Lines(text, "serial=").Count == 1 && Lines(text, "input=").Count == 1, "a plain key is written twice");
            Check(Lines(text, "tv2.ip=").Count == 1 && Lines(text, "tv3.input=").Count == 1, "the other TVs' keys are missing");

            Settings t = Settings.Load();
            Check(t.Tvs.Count == 3 && t.IsComplete && t.SeveralTvs && t.Paused, "TVs read back: " + t.Tvs.Count);
            for (int i = 0; i < 3; i++)
            {
                TvSettings a = s.Tvs[i], b = t.Tvs[i];
                Check(a.Ip == b.Ip && a.Serial == b.Serial && a.Input == b.Input && a.WakeMac == b.WakeMac && a.GatewayMac == b.GatewayMac && a.Name == b.Name,
                    "TV " + (i + 1) + " came back as " + b.Ip + " " + b.Serial + " " + b.Input + " [" + b.Name + "]");
            }
            Check(t.Tvs[1].PcApp == "tvinput.hdmi2", "each TV has its own input");
            File.Delete(Settings.FilePath);
        }

        static List<string> Lines(string text, string start)
        {
            var lines = new List<string>();
            foreach (string line in text.Split(new[] { "\r\n" }, StringSplitOptions.None)) if (line.StartsWith(start)) lines.Add(line);
            return lines;
        }

        static void SeveralTvsOddFiles()
        {
            Directory.CreateDirectory(Settings.Dir);
            // a 1.1 file: one TV
            File.WriteAllText(Settings.FilePath, "ip=192.168.1.50\r\nserial=X1\r\ninput=hdmi1   # a note\r\nremote_layout=strip\r\n");
            Settings s = Settings.Load();
            Check(s.Tvs.Count == 1 && s.IsComplete && !s.SeveralTvs && s.Tvs[0].Input == "hdmi1" && s.RemoteLayout == "strip", "a 1.1 file");

            // numbers needn't follow on, the same TV twice counts once, odd keys are passed over
            File.WriteAllText(Settings.FilePath,
                "tv5.ip=192.168.1.55\r\ntv5.serial=X5\r\nip=192.168.1.50\r\nserial=X1\r\n" +
                "tv3.ip=192.168.1.53\r\ntv3.serial=X1\r\n" +           // TV 1 again
                "tv0.ip=10.0.0.1\r\ntv100.ip=10.0.0.2\r\ntvx.ip=10.0.0.3\r\ntv4.colour=red\r\n" +
                "tv5.name=Den #2\r\n");
            s = Settings.Load();
            Check(s.Tvs.Count == 2 && s.Tvs[0].Serial == "X1" && s.Tvs[1].Serial == "X5", "TVs: " + string.Join(",", s.Tvs.ConvertAll(t => t.Serial)));
            Check(s.Tvs[1].Name == "Den #2", "a name with a # was cut: [" + s.Tvs[1].Name + "]");
            Check(LogText().Contains("listed twice"), "the repeated TV isn't logged");

            // a TV whose address is junk: not set up, so setup opens rather than half the TVs following
            File.WriteAllText(Settings.FilePath, "ip=192.168.1.50\r\nserial=X1\r\ntv2.ip=my.tv\r\ntv2.serial=X2\r\n");
            s = Settings.Load();
            Check(s.Tvs.Count == 2 && !s.IsComplete, "a TV with a junk address counted as set up");
            Check(LogText().Contains("tv2.ip=my.tv"), "the junk address isn't logged with its key");
            File.Delete(Settings.FilePath);
        }

        static void SeveralTvsLabels()
        {
            var s = new Settings();
            s.Tvs.Add(new TvSettings { Name = "Living Room TV" });
            s.Tvs.Add(new TvSettings { Name = "" });
            s.Tvs.Add(new TvSettings { Name = "Roku TV" });
            s.Tvs.Add(new TvSettings { Name = "roku tv" });
            s.Tvs.Add(new TvSettings { Name = new string('x', 60) });
            Check(s.LabelOf(s.Tvs[0]) == "Living Room TV" && s.LabelOf(s.Tvs[1]) == "TV 2", "name, or TV and its number");
            Check(s.LabelOf(s.Tvs[2]) == "Roku TV (3)" && s.LabelOf(s.Tvs[3]) == "roku tv (4)", "the same name twice: " + s.LabelOf(s.Tvs[2]));
            Check(s.LabelOf(s.Tvs[4]).Length == 40 && s.LabelOf(s.Tvs[4]).EndsWith("…"), "a long name isn't shortened");
            Check(Tray.MenuText("Tom & Jerry") == "Tom && Jerry", "an & in a menu");
        }

        // ---- following ----

        static void SeveralTvsFollowAtOnce()
        {
            using (var slow = new FakeTv("127.0.0.1", 0))
            using (var quick = new FakeTv("127.0.0.2", slow.Port))
            {
                quick.Serial = "X000000002";
                foreach (FakeTv tv in new[] { slow, quick }) { tv.PowerMode = "DisplayOff"; tv.App = ""; }
                slow.DelayMs = 700; // every answer takes a while, like a TV on weak Wi-Fi
                Settings s = TwoTvs(slow, quick);
                var follower = new Follower(s, FastTvs(s));
                follower.Rules.SecondsSinceInput = () => 0.5;
                follower.StartWorkers();
                follower.Rules.MonitorsOn();
                Pump(() => quick.IsOn && quick.App == "tvinput.hdmi2", 15);
                Check(slow.App != "tvinput.hdmi1", "the slow TV was done first, so the quick one may have waited for it");
                // the slow TV says it's done once it has looked again after the input press
                Pump(() => LogText().Contains("Living Room TV: on: showing the PC"), 30);
                Check(slow.IsOn && slow.App == "tvinput.hdmi1", "the slow TV isn't showing the PC");
                Check(LogText().Contains("Office TV: on: showing the PC"), "the log doesn't say which TV: " + LogText());
            }
        }

        static void SeveralTvsQuickOff()
        {
            using (var a = new FakeTv("127.0.0.1", 0))
            using (var b = new FakeTv("127.0.0.2", a.Port))
            {
                b.Serial = "X000000002";
                b.App = "tvinput.hdmi2";
                a.DelayMs = b.DelayMs = 500;
                Settings s = TwoTvs(a, b);
                var follower = new Follower(s, FastTvs(s));
                var watch = Stopwatch.StartNew();
                follower.QuickOff();
                watch.Stop();
                Check(!a.IsOn && !b.IsOn, "a TV is still on after sleep");
                // each TV needs three answers of half a second; one after the other would be 3 s
                Check(watch.ElapsedMilliseconds < 2700, "the TVs were turned off one after the other: " + watch.ElapsedMilliseconds + " ms");

                a.PowerMode = b.PowerMode = "PowerOn";
                follower.Rules.Paused = true;
                follower.QuickOff();
                Check(a.IsOn && b.IsOn, "paused, yet sleep turned a TV off");
            }
        }

        static void CommandPicksTvs()
        {
            var s = new Settings();
            s.Tvs.Add(new TvSettings { Name = "Living Room TV" });
            s.Tvs.Add(new TvSettings { Name = "Office TV" });
            s.Tvs.Add(new TvSettings { Name = "Living Room TV" });
            List<Controller> tvs = Controller.For(s);
            Check(Program.Pick(tvs, null).Count == 3 && Program.Pick(tvs, "").Count == 3, "no TV named: all of them");
            Check(Program.Pick(tvs, "2")[0] == tvs[1] && Program.Pick(tvs, "2").Count == 1, "by number");
            Check(Program.Pick(tvs, "0") == null && Program.Pick(tvs, "4") == null && Program.Pick(tvs, "-1") == null, "a number with no TV");
            Check(Program.Pick(tvs, "office tv")[0] == tvs[1], "by name, any case");
            Check(Program.Pick(tvs, "Living Room TV").Count == 2, "a name two TVs share: both");
            Check(Program.Pick(tvs, "Living Room TV (3)")[0] == tvs[2], "by the name the menus show");
            Check(Program.Pick(tvs, "Kitchen") == null, "a name no TV has");
        }

        // ---- the windows ----

        static void TrayWithSeveralTvs()
        {
            using (var a = new FakeTv("127.0.0.1", 0))
            using (var b = new FakeTv("127.0.0.2", a.Port))
            {
                b.Serial = "X000000002";
                b.App = "12"; // watching a channel: "All TVs off" turns it off anyway
                Settings s = TwoTvs(a, b);
                s.Tvs[1].Name = "Tom & Jerry";
                var follower = new Follower(s, FastTvs(s)); // not started
                using (var tray = new Tray(follower))
                {
                    var texts = new List<string>();
                    ToolStripMenuItem allOff = null, jerry = null;
                    foreach (ToolStripItem item in tray.Menu.Items)
                    {
                        texts.Add(item.Text);
                        if (item.Text == "All TVs off") allOff = (ToolStripMenuItem)item;
                        if (item.Text == "Tom && Jerry") jerry = (ToolStripMenuItem)item;
                    }
                    Check(texts.Contains("All TVs on") && texts.Contains("All TVs to PC") && !texts.Contains("TV on"), "menu: " + string.Join(" | ", texts));
                    Check(texts.Contains("Living Room TV") && jerry != null, "a TV's own menu is missing: " + string.Join(" | ", texts));
                    Check(jerry.DropDownItems.Count == 5 && jerry.DropDownItems[0].Text == "TV on" && jerry.DropDownItems[4].Text == "Remote…", "a TV's menu");
                    allOff.PerformClick();
                    Pump(() => !a.IsOn && !b.IsOn, 10);
                    a.PowerMode = "PowerOn";
                    jerry.DropDownItems[1].PerformClick(); // Tom & Jerry's TV off: the other TV is left alone
                    Pump(() => LogText().Contains("Tom & Jerry: off (button): already off"), 10);
                    Check(a.IsOn, "a TV's own off button turned the other TV off");
                }
            }
        }

        static void RemotePicksTv()
        {
            using (var a = new FakeTv("127.0.0.1", 0))
            using (var b = new FakeTv("127.0.0.2", a.Port))
            {
                b.Serial = "X000000002";
                b.InputNames = new[] { "HDMI 1", "Game console" };
                Settings s = TwoTvs(a, b);
                List<Controller> tvs = Controller.For(s);
                var form = new RemoteForm(s, tvs, tvs[0], RemoteForm.Classic);
                form.Show();
                form.Location = new Point(-20000, -20000);
                try
                {
                    Pump(() => FindChip(form, "HDMI 3") != null, 10); // the first TV's inputs
                    Check(FindPicker(form) != null && FindPicker(form).Label == "Living Room TV", "no TV picker on a remote for two TVs");
                    InvokeClick(form.Buttons().Find(x => (string)x.Tag == RemoteKeys.Home));
                    Pump(() => a.Count("POST /keypress/Home") == 1, 10);

                    form.ShowTv(tvs[1]);
                    Check(form.Tv == tvs[1] && FindPicker(form).Label == "Office TV", "the picker didn't move to the other TV");
                    Pump(() => FindChip(form, "HDMI 2 · Game console") != null, 10); // the second TV's own inputs
                    InvokeClick(form.Buttons().Find(x => (string)x.Tag == RemoteKeys.Up));
                    Pump(() => b.Count("POST /keypress/Up") == 1, 10);
                    Check(a.Count("POST /keypress/Up") == 0 && b.Count("POST /keypress/Home") == 0, "a press went to the wrong TV");
                }
                finally { form.Close(); form.Dispose(); }

                // presses still waiting to go out when another TV is picked go where they were pressed
                a.DelayMs = 400;
                Controller current = tvs[0];
                var done = new ManualResetEvent(false);
                int answers = 0;
                using (var sender = new RemoteSender(() => new RokuClient(current.Tv.Ip, s.Port)))
                {
                    sender.Answered += code => { if (Interlocked.Increment(ref answers) == 3) done.Set(); };
                    sender.Press("Left");
                    sender.Press("Right");
                    current = tvs[1];
                    sender.Press("Down");
                    Check(done.WaitOne(10000), "the presses weren't answered");
                }
                Check(a.Count("POST /keypress/Left") == 1 && a.Count("POST /keypress/Right") == 1 && b.Count("POST /keypress/Down") == 1,
                    "a press went to the TV picked after it was pressed");
            }
        }

        static PadButton FindPicker(Control root)
        {
            foreach (Control c in root.Controls)
            {
                var b = c as PadButton;
                if (b != null && b.AccessibleName == "Pick the TV") return b;
                PadButton inner = FindPicker(c);
                if (inner != null) return inner;
            }
            return null;
        }

        static void SettingsWindowSeveralTvs()
        {
            using (var a = new FakeTv("127.0.0.1", 0))
            using (var b = new FakeTv("127.0.0.2", a.Port))
            using (var other = new FakeTv("127.0.0.3", a.Port))
            {
                b.Serial = "X000000002";
                b.Name = "Office TV";       // renamed on the TV since setup
                other.Serial = "SOMEONE-ELSE";
                Settings s = TwoTvs(a, b);
                s.Tvs[1].Name = "Old name";
                s.Tvs.Add(new TvSettings { Ip = other.Ip, Serial = "X000000003", Input = "hdmi1", Name = "Bedroom TV" }); // its address went to another Roku
                s.Save();
                var follower = new Follower(s, Controller.For(s)); // not started
                using (var tray = new Tray(follower))
                using (var window = new SettingsForm(s, follower, tray))
                {
                    window.StartPosition = FormStartPosition.Manual;
                    window.Location = new Point(-20000, -20000);
                    window.Show();
                    Pump(() => AllText(window).Contains("2.  Office TV") && AllText(window).Contains("Another Roku answers"), 10);
                    string text = AllText(window);
                    Check(text.Contains("TVs") && text.Contains("1.  Test TV") && text.Contains("3.  Bedroom TV"), "a TV's part is missing: " + text);
                    Check(FindBox(window, "Turn the TVs off") != null, "the follow options don't speak of TVs");
                    Check(s.Tvs[1].Name == "Office TV" && Settings.Load().Tvs[1].Name == "Office TV", "the new name wasn't saved");
                    Check(s.Tvs[2].Name == "Bedroom TV", "another Roku's name was saved for the TV");

                    var boxes = new List<ComboBox>();
                    FindCombos(window, boxes);
                    Check(boxes.Count == 3, "input lists: " + boxes.Count);
                    boxes[1].SelectedIndex = 2; // the second TV's third input
                    Check(s.Tvs[1].Input == "hdmi3" && s.Tvs[0].Input == "hdmi1", "the input went to the wrong TV");
                    Check(Settings.Load().Tvs[1].Input == "hdmi3", "the input wasn't saved");
                    window.Close();
                }
                File.Delete(Settings.FilePath);
            }
        }

        static void FindCombos(Control root, List<ComboBox> found)
        {
            foreach (Control c in root.Controls)
            {
                if (c is ComboBox) found.Add((ComboBox)c);
                else FindCombos(c, found);
            }
        }

        // ---- setup ----

        static void SetupWaitsForTick()
        {
            using (var a = new FakeTv("127.0.0.1", 0))
            using (var b = new FakeTv("127.0.0.2", a.Port))
            {
                b.Serial = "X000000002";
                var s = new Settings();
                s.Port = a.Port;
                SetupForm form = OffScreenSetup(s, () => TvScanner.Probe(new[] { "127.0.0.1", "127.0.0.2" }, a.Port));
                try
                {
                    Pump(() => !form.CurrentPage.Busy && AllText(form.CurrentPage).Contains("Found 2 TVs"), 15);
                    Check(!form.CurrentPage.CanGoNext, "Next with nothing ticked");
                    form.GoNext();
                    Check(form.CurrentPage is FindPage, "went past step 1 with nothing ticked");
                }
                finally { form.Dispose(); }
            }
        }

        // the setup window's pages for one TV: control, input, a test that passes
        static void WalkOneTv(SetupForm form)
        {
            Check(form.CurrentPage is ControlPage, "expected the control page, got " + form.CurrentPage.Title);
            Pump(() => form.CurrentPage.CanGoNext, 15);
            form.GoNext();
            Pump(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext, 15);
            form.GoNext();
            ((TestPage)form.CurrentPage).StartTest();
            Pump(() => !form.CurrentPage.Busy, 20);
            Check(form.CurrentPage.CanGoNext, "the test didn't pass: " + AllText(form.CurrentPage));
        }

        static void SetupTwoTvs()
        {
            using (var a = new FakeTv("127.0.0.1", 0))
            using (var b = new FakeTv("127.0.0.2", a.Port))
            {
                a.App = "tvinput.hdmi2";
                b.Serial = "X000000002";
                b.Name = "Upstairs TV"; // listed after Test TV
                b.App = "tvinput.hdmi3";
                var s = new Settings();
                s.Port = a.Port;
                SetupForm form = OffScreenSetup(s, () => TvScanner.Probe(new[] { "127.0.0.1", "127.0.0.2" }, a.Port));
                try
                {
                    Pump(() => !form.CurrentPage.Busy && AllText(form.CurrentPage).Contains("Found 2 TVs"), 15);
                    List<FoundTv> found = TvScanner.Probe(new[] { "127.0.0.2", "127.0.0.1" }, a.Port);
                    form.Pick(found.Find(f => f.Ip == "127.0.0.2"), found.Find(f => f.Ip == "127.0.0.1")); // ticked in the other order
                    form.GoNext();
                    Check(form.Tv.Ip == "127.0.0.1", "the TVs aren't taken in the list's order: " + form.Tv.Ip);
                    Check(AllText(form).Contains("TV 1 of 2: Test TV"), "the header doesn't say which TV");
                    WalkOneTv(form);
                    form.GoNext();
                    Check(form.Tv.Ip == "127.0.0.2" && AllText(form).Contains("TV 2 of 2: Upstairs TV"), "not on the second TV");
                    form.GoBack();
                    Check(form.CurrentPage is TestPage && form.Tv.Ip == "127.0.0.1" && form.CurrentPage.CanGoNext, "Back didn't return to the first TV's test");
                    form.GoNext();
                    WalkOneTv(form);
                    form.GoNext();
                    Check(form.CurrentPage is FinishPage, "not on the last page");
                    string text = AllText(form.CurrentPage);
                    Check(text.Contains("Test TV (Test room):  HDMI 2") && text.Contains("Upstairs TV (Test room):  HDMI 3"), "the summary: " + text);
                    Check(FindBox(form, "Turn the TVs off") != null, "the options don't speak of TVs");
                    form.GoNext();
                    Check(form.DialogResult == DialogResult.OK, "Finish didn't close with OK");
                }
                finally { form.Dispose(); }
                Settings saved = Settings.Load();
                Check(saved.Tvs.Count == 2, "TVs saved: " + saved.Tvs.Count);
                Check(saved.Tvs[0].Ip == "127.0.0.1" && saved.Tvs[0].Input == "hdmi2" && saved.Tvs[0].Name == "Test TV", "the first TV");
                Check(saved.Tvs[1].Ip == "127.0.0.2" && saved.Tvs[1].Serial == "X000000002" && saved.Tvs[1].Input == "hdmi3" && saved.Tvs[1].Name == "Upstairs TV", "the second TV");
                Check(LogText().Contains("setup saved: 2 TVs, inputs hdmi2, hdmi3"), "not logged");
                File.Delete(Settings.FilePath);
            }
        }

        static void SetupAddsTv()
        {
            using (var a = new FakeTv("127.0.0.1", 0))
            using (var b = new FakeTv("127.0.0.2", a.Port))
            {
                b.Serial = "X000000002";
                b.Name = "Office TV";
                b.App = "tvinput.hdmi3";
                a.ControlMode = "limited"; // it'd stop step 2, so it mustn't go through it
                var s = new Settings();
                s.Port = a.Port;
                s.Tvs.Add(new TvSettings { Ip = "127.0.0.1", Serial = a.Serial, Input = "hdmi4", WakeMac = "00:11:22:33:44:99", GatewayMac = "AA:BB:CC:DD:EE:FF", Name = "Old name" });
                SetupForm form = OffScreenSetup(s, () => TvScanner.Probe(new[] { "127.0.0.1", "127.0.0.2" }, a.Port));
                try
                {
                    Pump(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext, 15);
                    Check(AllText(form.CurrentPage).Contains("Test TV it is"), "the TV set up before isn't ticked: " + AllText(form.CurrentPage));
                    List<FoundTv> found = TvScanner.Probe(new[] { "127.0.0.1", "127.0.0.2" }, a.Port);
                    form.Pick(found.ToArray());
                    form.GoNext();
                    Check(form.Tv != null && form.Tv.Ip == "127.0.0.2", "the steps aren't on the new TV");
                    Check(AllText(form).Contains("Step 2 of 5  ·  Office TV") && !AllText(form).Contains("TV 1 of"), "the header: " + AllText(form));
                    WalkOneTv(form);
                    form.GoNext();
                    Check(form.CurrentPage is FinishPage, "not on the last page");
                    Check(AllText(form.CurrentPage).Contains("Test TV (Test room):  HDMI 4"), "the TV set up before isn't in the summary");
                    form.GoNext();
                    Check(form.DialogResult == DialogResult.OK, "Finish didn't close with OK");
                }
                finally { form.Dispose(); }
                Settings saved = Settings.Load();
                Check(saved.Tvs.Count == 2 && saved.Tvs[0].Serial == a.Serial && saved.Tvs[1].Serial == "X000000002", "TVs saved: " + saved.Tvs.Count);
                TvSettings kept = saved.Tvs[0];
                Check(kept.Input == "hdmi4", "the TV set up before lost its input: " + kept.Input);
                Check(kept.Name == "Test TV" && kept.WakeMac == "00:11:22:33:44:55", "the TV set up before wasn't refreshed from its answer");
                Check(kept.GatewayMac == "", "setup kept the old router (this may be a new home)");
                File.Delete(Settings.FilePath);
            }
        }

        static void SetupKeepsSilentTv()
        {
            using (var b = new FakeTv("127.0.0.2", 0))
            {
                b.Serial = "X000000002";
                var s = new Settings();
                s.Port = b.Port;
                // set up before, and off the network now (nothing is asked at its address: the scan is pretend)
                s.Tvs.Add(new TvSettings { Ip = "127.0.0.5", Serial = "X000000005", Input = "hdmi2", WakeMac = "00:11:22:33:44:05", GatewayMac = "AA:BB:CC:DD:EE:05", Name = "Garage TV" });
                SetupForm form = OffScreenSetup(s, () => TvScanner.Probe(new[] { "127.0.0.2" }, b.Port));
                try
                {
                    Pump(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext, 15);
                    Check(AllText(form.CurrentPage).Contains("Garage TV it is"), "the silent TV isn't listed and ticked: " + AllText(form.CurrentPage));
                    form.GoNext();
                    Check(form.CurrentPage is FinishPage, "a TV set up before went through the steps: " + form.CurrentPage.Title);
                    form.GoBack();
                    Check(form.CurrentPage is FindPage, "Back from the last page should go to step 1 when no TV went through the steps");
                    form.GoNext();
                    form.GoNext();
                    Check(form.DialogResult == DialogResult.OK, "Finish didn't close with OK");
                }
                finally { form.Dispose(); }
                Settings saved = Settings.Load();
                Check(saved.Tvs.Count == 1 && saved.Tvs[0].Ip == "127.0.0.5" && saved.Tvs[0].Input == "hdmi2" && saved.Tvs[0].GatewayMac == "AA:BB:CC:DD:EE:05",
                    "the silent TV didn't keep its settings");

                // unticked, it goes, and the TV ticked instead goes through the steps
                form = OffScreenSetup(saved, () => TvScanner.Probe(new[] { "127.0.0.2" }, b.Port));
                try
                {
                    Pump(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext, 15);
                    form.Pick(TvScanner.Probe(new[] { "127.0.0.2" }, b.Port)[0]);
                    form.GoNext();
                    WalkOneTv(form);
                    form.GoNext();
                    form.GoNext();
                    Check(form.DialogResult == DialogResult.OK, "Finish didn't close with OK");
                }
                finally { form.Dispose(); }
                saved = Settings.Load();
                Check(saved.Tvs.Count == 1 && saved.Tvs[0].Serial == "X000000002", "the unticked TV is still there");
                File.Delete(Settings.FilePath);
            }
        }

        static void SetupRedoesTv()
        {
            using (var a = new FakeTv("127.0.0.1", 0))
            {
                var s = new Settings();
                s.Port = a.Port;
                s.Tvs.Add(new TvSettings { Ip = "127.0.0.1", Serial = a.Serial, Input = "hdmi1", Name = "Test TV" });
                SetupForm form = OffScreenSetup(s, () => TvScanner.Probe(new[] { "127.0.0.1" }, a.Port));
                try
                {
                    Pump(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext, 15);
                    CheckBox redo = FindBox(form.CurrentPage, "Go through the next steps again");
                    Check(redo != null && redo.Visible, "the box isn't there");
                    redo.Checked = true;
                    form.GoNext();
                    Check(form.CurrentPage is ControlPage && form.Tv.Ip == "127.0.0.1", "the TV set up before didn't go through the steps again");
                }
                finally { form.Dispose(); }
            }
        }
    }
}
