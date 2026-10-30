using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow.Tests
{
    // The remote and the Settings window.
    static partial class TestMain
    {
        static void RunRemoteTests()
        {
            Run("remote: keyboard keys map to the remote's buttons", RemoteKeyMap);
            Run("remote: typed letters are sent the TV's way", RemoteLetters);
            Run("remote: presses arrive in order and Limited is reported", RemoteSenderOrder);
            Run("remote: all TV inputs are listed, HDMI first", RemoteAllInputs);
            Run("remote: every layout has every button, inside the window, none overlapping", RemoteLayouts);
            Run("remote: clicking buttons sends the right presses", RemoteClicks);
            Run("settings: the remote layout is kept", RemoteLayoutSetting);
            Run("settings window: options apply and save straight away", SettingsWindowApplies);
        }

        static void RemoteKeyMap()
        {
            Check(RemoteKeys.ForKey(Keys.Up) == "Up" && RemoteKeys.ForKey(Keys.Left) == "Left", "arrows");
            Check(RemoteKeys.ForKey(Keys.Enter) == "Select", "Enter is OK");
            Check(RemoteKeys.ForKey(Keys.Back) == "Back", "Backspace is Back");
            Check(RemoteKeys.ForKey(Keys.Space) == "Play" && RemoteKeys.ForKey(Keys.Home) == "Home", "Space and Home");
            Check(RemoteKeys.ForKey(Keys.Oemplus) == "VolumeUp" && RemoteKeys.ForKey(Keys.Subtract) == "VolumeDown", "+ and -");
            Check(RemoteKeys.ForKey(Keys.A) == null && RemoteKeys.ForKey(Keys.F5) == null, "other keys do nothing");
        }

        static void RemoteLetters()
        {
            Check(RemoteKeys.Letter('a') == "Lit_a" && RemoteKeys.Letter('Z') == "Lit_Z", "letters");
            Check(RemoteKeys.Letter(' ') == "Lit_%20" && RemoteKeys.Letter('&') == "Lit_%26" && RemoteKeys.Letter('/') == "Lit_%2F", "symbols are escaped");
            Check(RemoteKeys.Letter('é') == "Lit_%C3%A9", "accented letters: " + RemoteKeys.Letter('é'));
        }

        static void RemoteSenderOrder()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                var answers = new List<int>();
                var typed5 = new ManualResetEvent(false);
                var done = new ManualResetEvent(false);
                using (var sender = new RemoteSender(() => new RokuClient(tv.Ip, tv.Port)))
                {
                    sender.Answered += code =>
                    {
                        lock (answers)
                        {
                            answers.Add(code);
                            if (answers.Count == 5) typed5.Set();
                            if (answers.Count == 6) done.Set();
                        }
                    };
                    foreach (char c in "hello") sender.Press(RemoteKeys.Letter(c));
                    Check(typed5.WaitOne(10000), "the letters weren't answered");
                    tv.ControlMode = "limited";
                    sender.Press("Home");
                    Check(done.WaitOne(10000), "the last press wasn't answered");
                }
                string typed = "";
                foreach (char c in "hello") typed += tv.Count("POST /keypress/Lit_" + c) > 0 ? c.ToString() : "?";
                Check(typed == "hello", "letters lost: " + typed);
                Check(answers[0] == 200 && answers[5] == 403, "answers: " + string.Join(",", answers));
            }
        }

        static void RemoteAllInputs()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                List<KeyValuePair<string, string>> all = new RokuClient(tv.Ip, tv.Port).GetTvInputs(2000);
                Check(all != null && all.Count == 4, "expected 3 HDMI + AV");
                Check(all[0].Key == "tvinput.hdmi1" && all[2].Key == "tvinput.hdmi3" && all[3].Key == "tvinput.cvbs", "order: " + all[3].Key);
            }
        }

        static RemoteForm OffScreenRemote(Settings s, string layout)
        {
            List<Controller> tvs = Controller.For(s);
            var form = new RemoteForm(s, tvs, tvs[0], layout);
            form.Show();
            form.Location = new Point(-20000, -20000); // after Load put it by the tray
            Pump(() => true, 1);
            return form;
        }

        static void RemoteLayouts()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings s = SettingsFor(tv);
                string[] needed = { RemoteKeys.Up, RemoteKeys.Down, RemoteKeys.Left, RemoteKeys.Right, RemoteKeys.Ok, RemoteKeys.Back, RemoteKeys.Home,
                    RemoteKeys.Options, RemoteKeys.Replay, RemoteKeys.Search, RemoteKeys.Rewind, RemoteKeys.Play, RemoteKeys.Forward,
                    RemoteKeys.VolumeUp, RemoteKeys.VolumeDown, RemoteKeys.Mute };
                foreach (string layout in new[] { RemoteForm.Classic, RemoteForm.Compact, RemoteForm.Strip })
                {
                    RemoteForm form = OffScreenRemote(s, layout);
                    try
                    {
                        Check(form.LayoutName == layout, "layout not used: " + form.LayoutName);
                        List<PadButton> buttons = form.Buttons();
                        foreach (string key in needed)
                            Check(buttons.Exists(b => (string)b.Tag == key), layout + " has no " + key + " button");
                        Check(buttons.Exists(b => b.AccessibleName == "Power"), layout + " has no power button");
                        Size board = form.BoardSize;
                        for (int i = 0; i < buttons.Count; i++)
                        {
                            Rectangle r = buttons[i].Bounds;
                            Check(r.Left >= 0 && r.Top >= 0 && r.Right <= board.Width && r.Bottom <= board.Height,
                                layout + ": " + buttons[i].AccessibleName + " sticks out");
                            for (int j = i + 1; j < buttons.Count; j++)
                                Check(!r.IntersectsWith(buttons[j].Bounds), layout + ": " + buttons[i].AccessibleName + " overlaps " + buttons[j].AccessibleName);
                        }
                    }
                    finally { form.Close(); form.Dispose(); }
                }
            }
        }

        static void RemoteClicks()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings s = SettingsFor(tv);
                RemoteForm form = OffScreenRemote(s, RemoteForm.Classic);
                try
                {
                    List<PadButton> buttons = form.Buttons();
                    foreach (string key in new[] { RemoteKeys.Home, RemoteKeys.Up, RemoteKeys.Ok, RemoteKeys.Mute })
                        InvokeClick(buttons.Find(b => (string)b.Tag == key));
                    Pump(() => tv.Count("POST /keypress/VolumeMute") == 1, 10);
                    Check(tv.Count("POST /keypress/Home") == 1 && tv.Count("POST /keypress/Up") == 1 && tv.Count("POST /keypress/Select") == 1,
                        "presses missing");
                    InvokeClick(buttons.Find(b => b.AccessibleName == "Power"));
                    Pump(() => !tv.IsOn, 10);
                    Check(tv.Count("POST /keypress/PowerOff") == 1, "power should turn an on TV off");
                    // the inputs come from the TV; picking one launches it
                    Pump(() => FindChip(form, "HDMI 2") != null, 10);
                    InvokeClick(FindChip(form, "HDMI 2"));
                    Pump(() => tv.Count("POST /launch/tvinput.hdmi2") == 1, 10);
                }
                finally { form.Close(); form.Dispose(); }
            }
        }

        static PadButton FindChip(Control root, string label)
        {
            foreach (Control c in root.Controls)
            {
                var b = c as PadButton;
                if (b != null && b.Label == label) return b;
                PadButton inner = FindChip(c, label);
                if (inner != null) return inner;
            }
            return null;
        }

        static void InvokeClick(Control c)
        {
            Check(c != null, "button not found");
            typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(c, new object[] { EventArgs.Empty });
        }

        static void RemoteLayoutSetting()
        {
            var s = new Settings();
            Tv(s).Ip = "192.168.1.50";
            Tv(s).Serial = "X000000001";
            Check(s.RemoteLayout == RemoteForm.Classic, "classic should be the default");
            s.RemoteLayout = RemoteForm.Strip;
            s.Save();
            Check(Settings.Load().RemoteLayout == RemoteForm.Strip, "layout not kept");
            File.WriteAllText(Settings.FilePath, "ip=1.2.3.4\r\nremote_layout=sideways\r\n");
            Check(Settings.Load().RemoteLayout == RemoteForm.Classic, "an unknown layout should fall back to classic");
            File.Delete(Settings.FilePath);
        }

        static void SettingsWindowApplies()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings s = SettingsFor(tv);
                s.Save();
                var follower = new Follower(s, Controller.For(s)); // not started: no window, no worker
                using (var tray = new Tray(follower))
                using (var window = new SettingsForm(s, follower, tray))
                {
                    window.StartPosition = FormStartPosition.Manual;
                    window.Location = new Point(-20000, -20000);
                    window.Show();
                    Pump(() => AllText(window).Contains("Test TV"), 10);

                    CheckBox off = FindBox(window, "Turn the TV off");
                    off.Checked = false;
                    Check(!s.OffWithMonitors && !follower.Rules.OffWithMonitors, "off option not applied");
                    Check(!Settings.Load().OffWithMonitors, "off option not saved");

                    CheckBox pause = FindBox(window, "Paused");
                    pause.Checked = true;
                    Check(follower.Rules.Paused && Settings.Load().Paused, "pause not applied and saved");
                    tray.SetPaused(false);
                    Pump(() => true, 1);
                    Check(!pause.Checked, "the window didn't follow a pause change from the tray");

                    window.Choose(RemoteForm.Compact);
                    Check(Settings.Load().RemoteLayout == RemoteForm.Compact, "layout not saved");
                    window.Close();
                }
                File.Delete(Settings.FilePath);
            }
        }

        static CheckBox FindBox(Control root, string start)
        {
            foreach (Control c in root.Controls)
            {
                var box = c as CheckBox;
                if (box != null && box.Text.StartsWith(start)) return box;
                CheckBox inner = FindBox(c, start);
                if (inner != null) return inner;
            }
            return null;
        }
    }
}
