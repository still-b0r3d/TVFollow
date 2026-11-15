using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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
            Run("remote: Ctrl+Backspace keeps the typing box and the TV in sync", RemoteCtrlBackspace);
            Run("remote: TV to PC status clears on success and reports trouble", RemoteToPcStatus);
            Run("remote: presses arrive in order and Limited is reported", RemoteSenderOrder);
            Run("remote: all TV inputs are listed, HDMI first", RemoteAllInputs);
            Run("remote: every layout has every button, inside the window, none overlapping", RemoteLayouts);
            Run("remote: clicking buttons sends the right presses", RemoteClicks);
            Run("settings: the remote layout is kept", RemoteLayoutSetting);
            Run("settings window: options apply and save straight away", SettingsWindowApplies);
            Run("settings window: Show it on the TV reports success and trouble", SettingsShowOnTvFeedback);
            Run("settings window: Settings… brings back the open one, even from minimized", SettingsBroughtBack);
        }

        static void RemoteKeyMap()
        {
            Check(RemoteKeys.ForKey(Keys.Up) == "Up" && RemoteKeys.ForKey(Keys.Left) == "Left", "arrows");
            Check(RemoteKeys.ForKey(Keys.Enter) == "Select", "Enter is OK");
            Check(RemoteKeys.ForKey(Keys.Back) == "Back", "Backspace is Back");
            Check(RemoteKeys.ForKey(Keys.Space) == "Play" && RemoteKeys.ForKey(Keys.Home) == "Home", "Space and Home");
            Check(RemoteKeys.ForKey(Keys.Oemplus) == "VolumeUp" && RemoteKeys.ForKey(Keys.Oemplus | Keys.Shift) == "VolumeUp" &&
                RemoteKeys.ForKey(Keys.Subtract) == "VolumeDown", "+, Shift+= and -");
            Check(RemoteKeys.ForKey(Keys.A) == null && RemoteKeys.ForKey(Keys.F5) == null &&
                RemoteKeys.ForKey(Keys.Oemplus | Keys.Control) == null, "other keys do nothing");

            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                RemoteForm form = OffScreenRemote(SettingsFor(tv), RemoteForm.Classic);
                try
                {
                    var processCmdKey = typeof(Control).GetMethod("ProcessCmdKey",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    var m = new Message();
                    // while typing, + goes to the typing box rather than TV volume
                    Check(form.Typing.Focused, "typing box starts focused");
                    Check(!(bool)processCmdKey.Invoke(form, new object[] { m, Keys.Oemplus | Keys.Shift }),
                        "Shift+= was stolen from the typing box");
                    // once the remote itself has focus, Shift+= turns volume up
                    form.ActiveControl = null;
                    Check((bool)processCmdKey.Invoke(form, new object[] { m, Keys.Oemplus | Keys.Shift }),
                        "Shift+= wasn't handled by the remote");
                    Pump(() => tv.Count("POST /keypress/VolumeUp") == 1, 10);
                }
                finally { form.Close(); form.Dispose(); }
            }
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
                    // the inputs come from the TV; picking one launches it, and the developer tools
                    // know the home screen on the way there isn't a trip home
                    Pump(() => FindChip(form, "HDMI 2") != null, 10);
                    DateTime picked = DateTime.UtcNow;
                    InvokeClick(FindChip(form, "HDMI 2"));
                    Pump(() => tv.Count("POST /launch/tvinput.hdmi2") == 1, 10);
                    Check(form.Tv.InputSwitchedAt >= picked, "picking an input didn't say so to the developer tools");
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

        static void SettingsBroughtBack()
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
                    {
                        ToolStripItem item = TrayItem(tray, "Settings…");
                        item.PerformClick();
                        Check(OpenWindows<SettingsForm>().Count == 1, "Settings… didn't open the Settings window");
                        SettingsForm window = OpenWindows<SettingsForm>()[0];
                        Check(window.Location.X < -10000, "the Settings window opened on the screen, at " + window.Location);
                        Pump(() => !AllText(window).Contains("Asking the TV"), 10);
                        // once more: the same window comes back, even from minimized (double-clicking
                        // the icon and opening TVFollow again go the same way)
                        MinimizeOffScreen(window);
                        Check(window.WindowState == FormWindowState.Minimized && IsIconic(window.Handle), "the test couldn't minimize the window");
                        item.PerformClick();
                        Check(OpenWindows<SettingsForm>().Count == 1 && OpenWindows<SettingsForm>()[0] == window, "a second click opened a second Settings window");
                        Check(window.WindowState == FormWindowState.Normal && !IsIconic(window.Handle), "a minimized Settings window stayed minimized");
                    }
                }
                finally
                {
                    Tray.PlaceWindow = place;
                    foreach (SettingsForm left in OpenWindows<SettingsForm>()) left.Close();
                }
                File.Delete(Settings.FilePath);
            }
        }

        [DllImport("user32.dll")] static extern bool GetKeyboardState(byte[] keys);
        [DllImport("user32.dll")] static extern bool SetKeyboardState(byte[] keys);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        static void RemoteCtrlBackspace()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                RemoteForm form = OffScreenRemote(SettingsFor(tv), RemoteForm.Classic);
                try
                {
                    TextBox box = form.Typing;
                    Check(box.IsHandleCreated, "typing box has no window handle");
                    bool sawDel = false;
                    box.TextChanged += delegate { if (box.Text.IndexOf('\x7F') >= 0) sawDel = true; };
                    Action pump60Ms = () =>
                    {
                        DateTime until = DateTime.UtcNow.AddMilliseconds(60);
                        while (DateTime.UtcNow < until)
                        {
                            Application.DoEvents();
                            Thread.Sleep(10);
                        }
                        Application.DoEvents();
                    };
                    Action<string> typeChars = text =>
                    {
                        foreach (char letter in text)
                            PostMessage(box.Handle, 0x0102, (IntPtr)letter, (IntPtr)1);
                        pump60Ms();
                    };
                    Action ctrlBackspace = () =>
                    {
                        var keys = new byte[256];
                        GetKeyboardState(keys);
                        keys[0x11] |= 0x80;   // Ctrl held, for this thread only
                        keys[0xA2] |= 0x80;
                        SetKeyboardState(keys);
                        try
                        {
                            PostMessage(box.Handle, 0x0100, (IntPtr)0x08, (IntPtr)0x000E0001);                   // WM_KEYDOWN
                            pump60Ms();
                            PostMessage(box.Handle, 0x0101, (IntPtr)0x08, (IntPtr)unchecked((int)0xC00E0001));   // WM_KEYUP
                            pump60Ms();
                        }
                        finally
                        {
                            keys[0x11] &= 0x7F;
                            keys[0xA2] &= 0x7F;
                            SetKeyboardState(keys);
                        }
                    };
                    Action noDel = () => Check(!sawDel && box.Text.IndexOf('\x7F') < 0, "typing box held 0x7F: [" + box.Text + "]");

                    // type "hello world": the box holds "hello world" and the TV got 11 letters
                    typeChars("hello world");
                    Pump(() => tv.Presses == 11, 10);
                    noDel();
                    Check(box.Text == "hello world", "typed text: [" + box.Text + "]");

                    // Ctrl+Backspace: the box holds "hello " (with its space) and the TV got 5 Backspaces
                    ctrlBackspace();
                    noDel();
                    Check(box.Text == "hello ", "after first Ctrl+Backspace: [" + box.Text + "]");
                    Pump(() => tv.Count("POST /keypress/Backspace") == 5, 10);

                    // Ctrl+Backspace again: the box is empty and the TV got 6 more
                    ctrlBackspace();
                    noDel();
                    Check(box.Text == "", "after second Ctrl+Backspace: [" + box.Text + "]");
                    Pump(() => tv.Count("POST /keypress/Backspace") == 11, 10);

                    // Ctrl+Backspace on the empty box: nothing more goes to the TV
                    ctrlBackspace();
                    noDel();
                    Check(box.Text == "", "on empty box: [" + box.Text + "]");

                    // with an emoji in the last word ("ab 😀cd"): 3 Backspaces for "😀cd"
                    // (waiting for those 6 letters also checks Ctrl+Backspace on the empty box sent nothing before them)
                    typeChars("ab \U0001F600cd");
                    Pump(() => tv.Presses == 11 + 11 + 6, 10);
                    noDel();
                    Check(box.Text == "ab \U0001F600cd", "emoji text: [" + box.Text + "]");
                    Check(tv.Count("POST /keypress/Backspace") == 11, "Ctrl+Backspace on the empty box sent a Backspace");

                    ctrlBackspace();
                    noDel();
                    Check(box.Text == "ab ", "after emoji Ctrl+Backspace: [" + box.Text + "]");
                    Pump(() => tv.Count("POST /keypress/Backspace") == 11 + 3, 10);

                    // check everything reached the pretend TV in order
                    var expected = new List<string>();
                    foreach (char c in "hello world") expected.Add("POST /keypress/" + RemoteKeys.Letter(c));
                    for (int i = 0; i < 11; i++) expected.Add("POST /keypress/Backspace");
                    foreach (string k in new[] { "Lit_a", "Lit_b", "Lit_%20", "Lit_%F0%9F%98%80", "Lit_c", "Lit_d" })
                        expected.Add("POST /keypress/" + k);
                    for (int i = 0; i < 3; i++) expected.Add("POST /keypress/Backspace");
                    List<string> sent = tv.Requests().FindAll(r => r.StartsWith("POST "));
                    Check(string.Join("|", sent) == string.Join("|", expected),
                        "wrong order: got [" + string.Join(" ", sent) + "], want [" + string.Join(" ", expected) + "]");

                    // selecting text and pressing Ctrl+Backspace deletes the selection and sends its presses
                    typeChars("xy");
                    Pump(() => tv.Count("POST /keypress/Lit_y") == 1, 10);
                    box.Select(1, 3); // "b x"
                    ctrlBackspace();
                    noDel();
                    Check(box.Text == "ay", "after selection Ctrl+Backspace: [" + box.Text + "]");
                    Pump(() => tv.Count("POST /keypress/Backspace") == 11 + 3 + 3, 10);
                }
                finally { form.Close(); form.Dispose(); }
            }
        }

        static void RemoteToPcStatus()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.PowerMode = "DisplayOff";
                tv.App = "";
                Settings s = SettingsFor(tv);
                List<Controller> tvs = Controller.For(s);
                Fast(tvs[0]);
                using (var form = new RemoteForm(s, tvs, tvs[0], RemoteForm.Classic))
                {
                    form.Show();
                    form.Location = new Point(-20000, -20000);
                    Pump(() => true, 1);
                    PadButton toPc = FindChip(form, "TV to PC");
                    Check(toPc != null, "no TV to PC button");

                    InvokeClick(toPc);
                    Check(AllText(form).Contains("Switching the TV to this PC"), "expected switching status: " + AllText(form));
                    Pump(() => tv.App == "tvinput.hdmi1" && !AllText(form).Contains("Switching"), 10);

                    tv.Down = true;
                    InvokeClick(toPc);
                    Pump(() => AllText(form).Contains("isn't answering"), 10);

                    tv.Down = false;
                    tv.PowerMode = "DisplayOff";
                    tv.ControlMode = "limited";
                    InvokeClick(toPc);
                    Pump(() => AllText(form).Contains("Limited"), 10);

                    tv.ControlMode = "enabled";
                    tv.PowerMode = "PowerOn";
                    tv.App = "";
                    tv.IgnoreInput = true;
                    InvokeClick(toPc);
                    Pump(() => AllText(form).Contains("didn't switch to this PC"), 10);
                    form.Close();
                }
            }
        }

        static void SettingsShowOnTvFeedback()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings s = SettingsFor(tv);
                s.Save();
                var follower = new Follower(s, Controller.For(s));
                using (var tray = new Tray(follower))
                using (var window = new SettingsForm(s, follower, tray))
                {
                    window.StartPosition = FormStartPosition.Manual;
                    window.Location = new Point(-20000, -20000);
                    window.Show();
                    Button show = FindControl<Button>(window, b => b.Text == "Show it on the TV");
                    Check(show != null, "Show it on the TV button missing");

                    show.PerformClick();
                    Pump(() => AllText(window).Contains("Sent. This PC's screen"), 10);

                    tv.RefuseLaunch = true;
                    show.PerformClick();
                    Pump(() => AllText(window).Contains("The TV said no"), 10);

                    tv.RefuseLaunch = false;
                    tv.Down = true;
                    show.PerformClick();
                    Pump(() => AllText(window).Contains("didn't answer"), 10);
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
