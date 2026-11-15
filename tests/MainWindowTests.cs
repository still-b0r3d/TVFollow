using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow.Tests
{
    // TVFollow's main window, and the presses of the remote it counts.
    static partial class TestMain
    {
        static void RunMainWindowTests()
        {
            Run("main window: double-clicking the tray icon or Open TVFollow opens it, one at a time; closing hides it", MainWindowFromTray);
            Run("main window: opening TVFollow again while it runs shows it", MainWindowOpenedAgain);
            Run("main window: the big button pauses and resumes, and the tray and Settings go with it both ways", MainWindowPauses);
            Run("main window: Remote… opens the remote", MainWindowRemote);
            Run("main window: Settings… opens Settings; Developer tools… is there only while they're on", MainWindowMenus);
            Run("main window: how each TV is, asked as it opens and every few seconds while it's open", MainWindowTvLines);
            Run("main window: the TVs aren't asked while it's minimized, the monitors are off or the PC is locked; at once when they're back", MainWindowQuietWhenUnseen);
            Run("main window: in high contrast its coloured words take Windows' colours", MainWindowHighContrast);
            Run("presses saved: only what the following did, and only when the TV said yes; the log adds up the same", PressesOnlyFollowing);
            Run("presses saved: a held settings.ini never holds up the TV; a burst is one write; quitting and shutdown write what's waiting", PressesWrittenLater);
            Run("presses saved: today starts again at midnight, in the open window too; all time carries on; both in settings.ini", PressesTodayAndAllTime);
            Run("presses saved: the first run counts them from the log, by the following's rule, only once, and again next start if it can't", PressesFromTheLog);
        }

        // a Label of the window's showing these words
        static Label LabelSaying(Control parent, string text)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is Label && c.Text == text) return (Label)c;
                Label inside = LabelSaying(c, text);
                if (inside != null) return inside;
            }
            return null;
        }

        // the window, answering its messages, asks the TV nothing for a while
        static void AsksNothing(FakeTv tv, string when)
        {
            DateTime settle = DateTime.UtcNow.AddMilliseconds(500);
            while (DateTime.UtcNow < settle) { Application.DoEvents(); Thread.Sleep(20); }  // an answer already on its way
            int asked = tv.Count("GET /query/device-info");
            DateTime until = DateTime.UtcNow.AddMilliseconds(1200);
            while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(20); }
            int more = tv.Count("GET /query/device-info") - asked;
            Check(more == 0, "the window asked the TV " + more + " times " + when);
        }

        static void MainWindowQuietWhenUnseen()
        {
            int every = MainForm.RefreshMs;
            try
            {
                using (var tv = new FakeTv("127.0.0.1", 0))
                {
                    tv.App = "tvinput.hdmi1";
                    WithTray(tv, (s, follower, tray) =>
                    {
                        // every 150 ms: never minimized, nor dark, nor locked
                        MainForm.RefreshMs = 150;
                        tray.ShowMain();
                        MainForm window = TheMainWindow();
                        Until(() => window.Answers > 0, 10, "the window never heard from the TV");
                        MinimizeOffScreen(window);
                        Check(IsIconic(window.Handle) && window.WindowState == FormWindowState.Minimized, "the test couldn't minimize the window");
                        AsksNothing(tv, "while it was minimized");
                        window.WindowState = FormWindowState.Normal;
                        int answers = window.Answers;
                        Until(() => window.Answers > answers, 10, "the window didn't ask again once it wasn't minimized");

                        follower.Rules.Display(0);
                        Check(follower.NobodyLooking, "the monitors off don't count as nobody looking");
                        AsksNothing(tv, "with the monitors off");
                        follower.Rules.Display(1);
                        follower.SessionLocked(true);
                        AsksNothing(tv, "with the PC locked");
                        follower.SessionLocked(false);
                        // a dim that goes on long enough to count as the monitors off
                        uint ms = 1000;
                        follower.Rules.Clock = () => ms;
                        follower.Rules.Display(2);
                        ms += (uint)(FollowRules.LongDimSec * 1000) + 1000;
                        follower.Rules.CheckDim();
                        AsksNothing(tv, "with the monitors dimmed long enough to count as off");
                        follower.Rules.Display(1);
                        Check(!follower.NobodyLooking, "with the monitors back on and the PC unlocked, nobody is looking still");

                        // back: asked at once, not at the next look (an hour away here)
                        window.Dispose();
                        MainForm.RefreshMs = 3600000;
                        tray.ShowMain();
                        window = TheMainWindow();
                        Until(() => window.Answers == 1, 10, "the window didn't ask as it opened");
                        follower.Rules.Display(0);
                        follower.Rules.Display(1);
                        Until(() => window.Answers == 2, 10, "the window didn't ask at once when the monitors came back on");
                        follower.SessionLocked(true);
                        follower.SessionLocked(false);
                        Until(() => window.Answers == 3, 10, "the window didn't ask at once when the PC was unlocked");
                    });
                }
            }
            finally { MainForm.RefreshMs = every; }
        }

        static void MainWindowHighContrast()
        {
            Func<bool> before = Look.HighContrast;
            try
            {
                using (var tv = new FakeTv("127.0.0.1", 0))
                {
                    tv.App = "tvinput.hdmi1";
                    WithTray(tv, (s, follower, tray) =>
                    {
                        Look.HighContrast = () => false;
                        tray.ShowMain();
                        MainForm window = TheMainWindow();
                        Until(() => window.Answers > 0, 10, "the window never heard from the TV");
                        CheckColours(window, false, "as it opens");
                        // a high contrast theme goes on while it's open, then off again
                        Look.HighContrast = () => true;
                        ColoursChanged(window);
                        CheckColours(window, true, "once high contrast goes on");
                        Look.HighContrast = () => false;
                        ColoursChanged(window);
                        CheckColours(window, false, "once high contrast goes off again");
                        // and a window opened while it's on
                        window.Dispose();
                        Look.HighContrast = () => true;
                        tray.ShowMain();
                        window = TheMainWindow();
                        Until(() => window.Answers > 0, 10, "the window opened in high contrast never heard from the TV");
                        CheckColours(window, true, "opened in high contrast");
                    });
                }
            }
            finally { Look.HighContrast = before; }
        }

        // the status and version line quiet, the TV's line (showing the PC) good
        static void CheckColours(MainForm window, bool contrast, string when)
        {
            Label status = LabelSaying(window, window.StatusText), version = LabelSaying(window, window.VersionLine);
            Label line = LabelSaying(window, window.TvTexts[0]);
            Check(status != null && version != null && line != null, when + ": the test couldn't find the window's lines");
            Color quiet = contrast ? SystemColors.GrayText : SetupPage.Muted;
            Color good = contrast ? SystemColors.WindowText : SetupPage.Good;
            Check(status.ForeColor == quiet && version.ForeColor == quiet, when + ": the status and version line are " + status.ForeColor + " and " + version.ForeColor + ", not " + quiet);
            Check(line.ForeColor == good, when + ": the TV's line (" + line.Text + ") is " + line.ForeColor + ", not " + good);
            Check(window.BackColor == SystemColors.Window && window.ForeColor == SystemColors.WindowText, when + ": the window is " + window.ForeColor + " on " + window.BackColor);
        }

        // A tray for one pretend TV (its follower not started), with its windows off the screen.
        // Whatever the test left open is closed afterwards.
        static void WithTray(FakeTv tv, Action<Settings, Follower, Tray> test)
        {
            Settings s = SettingsFor(tv);
            s.Save();
            var follower = new Follower(s, Controller.For(s));
            Action<Form> place = Tray.PlaceWindow;
            Tray.PlaceWindow = OffScreen;
            try
            {
                using (var tray = new Tray(follower)) test(s, follower, tray);
            }
            finally
            {
                Tray.PlaceWindow = place;
                foreach (MainForm left in OpenWindows<MainForm>()) left.Dispose();
                foreach (SettingsForm left in OpenWindows<SettingsForm>()) left.Close();
                foreach (RemoteForm left in OpenWindows<RemoteForm>()) left.Close();
                File.Delete(Settings.FilePath);
            }
        }

        // what a double-click on the tray icon does: Windows tells the icon, and it raises its DoubleClick
        static void DoubleClickIcon(Tray tray)
        {
            var icon = (NotifyIcon)typeof(Tray).GetField("icon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tray);
            MethodInfo raise = typeof(NotifyIcon).GetMethod("OnDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(raise != null, "this Windows Forms has no NotifyIcon.OnDoubleClick for the test to call");
            raise.Invoke(icon, new object[] { EventArgs.Empty });
        }

        static MainForm TheMainWindow()
        {
            List<MainForm> open = OpenWindows<MainForm>();
            Check(open.Count == 1, open.Count + " main windows are open, not 1");
            return open[0];
        }

        // waits for something the window's thread does, answering its messages meanwhile
        static void Until(Func<bool> done, int seconds, string what)
        {
            DateTime until = DateTime.UtcNow.AddSeconds(seconds);
            while (!done())
            {
                if (DateTime.UtcNow > until) throw new Exception(what);
                Application.DoEvents();
                Thread.Sleep(20);
            }
        }

        static void MainWindowFromTray()
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
                    MainForm window;
                    using (var tray = new Tray(follower))
                    {
                        // TVFollow starts in the tray: no window until someone asks for it
                        Check(OpenWindows<MainForm>().Count == 0, "the main window opened by itself");
                        ToolStripItem first = tray.Menu.Items[0];
                        Check(first.Text == "Open TVFollow" && first.Font.Bold, "the tray menu starts with " + first.Text + (first.Font.Bold ? " (bold)" : " (not bold)"));
                        foreach (ToolStripItem item in tray.Menu.Items)
                            Check(item == first || !item.Font.Bold, item.Text + " is bold too: only the default, Open TVFollow, should be");

                        DoubleClickIcon(tray);
                        window = TheMainWindow();
                        Check(window.Visible && window.Location.X < -10000, "the window isn't open off the screen: " + window.Location);
                        Check(OpenWindows<SettingsForm>().Count == 0, "double-clicking the icon opened Settings");
                        Check(window.Text == "TVFollow" && window.VersionLine == Program.FullVersion, "the window says " + window.Text + " / " + window.VersionLine);

                        // again, and from the menu: the same window, brought back from minimized
                        MinimizeOffScreen(window);
                        Check(IsIconic(window.Handle), "the test couldn't minimize the window");
                        first.PerformClick();
                        Check(TheMainWindow() == window, "Open TVFollow opened a second window");
                        Check(window.WindowState == FormWindowState.Normal && !IsIconic(window.Handle), "a minimized window stayed minimized");

                        // closed, it's only out of sight: TVFollow carries on, and the window comes back
                        window.Close();
                        Check(!window.IsDisposed && !window.Visible, "closing the window " + (window.IsDisposed ? "ended it" : "left it on show"));
                        DoubleClickIcon(tray);
                        Check(TheMainWindow() == window && window.Visible, "the window didn't come back");
                    }
                    // TVFollow quitting (the tray going) closes it for good
                    Check(window.IsDisposed, "the window outlived the tray");
                }
                finally
                {
                    Tray.PlaceWindow = place;
                    foreach (MainForm left in OpenWindows<MainForm>()) left.Dispose();
                    foreach (SettingsForm left in OpenWindows<SettingsForm>()) left.Close();
                }
                File.Delete(Settings.FilePath);
            }
        }

        static void MainWindowOpenedAgain()
        {
            // the signal is the trial folder's own, never the real TVFollow's
            string suffix = Instance.SuffixFor(true, Settings.Dir);
            Check(Settings.IsTrial && suffix.Length > 0 && Instance.ShowName.EndsWith(suffix), "the tests' settings folder isn't a trial one: " + Settings.Dir);
            Check(Program.WhatOpens("", true, true, true) == Program.Opening.Window, "opening TVFollow again while it runs doesn't ask for its window");
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                WithTray(tv, (s, follower, tray) =>
                {
                    follower.Listen();
                    try
                    {
                        // what opening TVFollow again does (Program.Main, Opening.Window)
                        Instance.AskToShow();
                        Until(() => OpenWindows<MainForm>().Count == 1, 10, "opening TVFollow again didn't open the main window");
                        MainForm window = TheMainWindow();
                        Check(window.Visible && OpenWindows<SettingsForm>().Count == 0, "opening TVFollow again opened " + (window.Visible ? "Settings too" : "a hidden window"));
                        window.Close();
                        Instance.AskToShow();
                        Until(() => window.Visible, 10, "opening TVFollow again didn't bring the closed window back");
                        Check(TheMainWindow() == window, "a second window opened");
                    }
                    finally { follower.StopListening(); }
                });
            }
        }

        static void MainWindowPauses()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                WithTray(tv, (s, follower, tray) =>
                {
                    tray.ShowMain();
                    MainForm window = TheMainWindow();
                    Check(window.ButtonText == "Pause following" && window.StatusText == "Following your monitors." && !window.ShowsPaused,
                        "while following: " + window.ButtonText + " / " + window.StatusText);
                    tray.ShowSettings();
                    CheckBox paused = FindBox(OpenWindows<SettingsForm>()[0], "Paused");

                    window.PauseButton.PerformClick();
                    Check(follower.Rules.Paused && Settings.Load().Paused, "the big button didn't pause (and save it)");
                    Check(window.ButtonText == "Resume following" && window.StatusText == "Paused: the TV is left alone." && window.ShowsPaused,
                        "paused: " + window.ButtonText + " / " + window.StatusText + (window.ShowsPaused ? "" : " / not the paused TV"));
                    Check(TrayItem(tray, "Resume following") != null && TrayItem(tray, "Paused: the TV is left alone") != null, "the tray menu didn't go with it: " + ShownItems(tray));
                    Check(paused.Checked, "Settings' Paused box didn't go with it");

                    // the other way: the tray's item
                    TrayItem(tray, "Resume following").PerformClick();
                    Check(!follower.Rules.Paused && window.ButtonText == "Pause following" && window.StatusText == "Following your monitors." && !window.ShowsPaused,
                        "resumed from the tray: " + window.ButtonText + " / " + window.StatusText);
                    // and Settings' box
                    paused.Checked = true;
                    Check(window.ButtonText == "Resume following" && window.ShowsPaused, "paused in Settings: " + window.ButtonText);
                    window.PauseButton.PerformClick();
                    Check(!follower.Rules.Paused && !paused.Checked && TrayItem(tray, "Pause following") != null && !Settings.Load().Paused,
                        "resumed with the big button: the tray says " + ShownItems(tray));
                });
            }
        }

        static void MainWindowRemote()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                WithTray(tv, (s, follower, tray) =>
                {
                    tray.ShowMain();
                    MainForm window = TheMainWindow();
                    Check(OpenWindows<RemoteForm>().Count == 0, "a remote was open already");
                    window.RemoteButton.PerformClick();
                    Check(OpenWindows<RemoteForm>().Count == 1, "Remote… didn't open the remote");
                    RemoteForm remote = OpenWindows<RemoteForm>()[0];
                    remote.Location = new Point(-20000, -20000);
                    window.RemoteButton.PerformClick();
                    Check(OpenWindows<RemoteForm>().Count == 1 && OpenWindows<RemoteForm>()[0] == remote, "a second click opened a second remote");
                    remote.Close();
                });
            }
        }

        static void MainWindowMenus()
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
                        tray.ShowMain();
                        MainForm window = TheMainWindow();
                        foreach (string item in new[] { "Settings…", "Developer tools…", "Quit TVFollow", "TVFollow help", "Open log" })
                            Check(window.MenuItem(item) != null, "the window's menus have no " + item);
                        ToolStripMenuItem developer = window.MenuItem("Developer tools…");
                        Check(!developer.Available, "Developer tools… is in the menu while they're switched off");

                        window.MenuItem("Settings…").PerformClick();
                        Check(OpenWindows<SettingsForm>().Count == 1, "Settings… didn't open the Settings window");
                        SettingsForm settings = OpenWindows<SettingsForm>()[0];
                        Check(settings.Location.X < -10000, "Settings opened on the screen, at " + settings.Location);
                        window.MenuItem("Settings…").PerformClick();
                        Check(OpenWindows<SettingsForm>().Count == 1, "a second Settings window opened");

                        // the switch, ticked in Settings: the item comes, and opens their pages
                        CheckBox on = FindBox(settings, "Turn on the developer tools");
                        on.Checked = true;
                        Check(developer.Available, "ticking the switch didn't bring Developer tools… into the window's menu");
                        settings.ShowPage(SettingsForm.TvsPage);
                        developer.PerformClick();
                        Check(OpenWindows<SettingsForm>().Count == 1 && settings.CurrentPage == SettingsForm.AppPage, "Developer tools… opened Settings on " + settings.CurrentPage);
                        on.Checked = false;
                        Check(!developer.Available, "switching them off left Developer tools… in the window's menu");

                        // a window opened while they're on has the item from the start
                        s.DeveloperTools = true;
                        using (var another = new MainForm(follower, tray))
                            Check(another.MenuItem("Developer tools…").Available, "a window opened with the developer tools on hasn't got the item");
                        s.DeveloperTools = false;
                        settings.Close();
                    }
                }
                finally
                {
                    Tray.PlaceWindow = place;
                    foreach (MainForm left in OpenWindows<MainForm>()) left.Dispose();
                    foreach (SettingsForm left in OpenWindows<SettingsForm>()) left.Close();
                }
                File.Delete(Settings.FilePath);
            }
        }

        static void MainWindowTvLines()
        {
            int every = MainForm.RefreshMs;
            MainForm.RefreshMs = 150;
            try
            {
                using (var tv = new FakeTv("127.0.0.1", 0))
                {
                    tv.App = "tvinput.hdmi1";
                    WithTray(tv, (s, follower, tray) =>
                    {
                        Tv(s).Name = "Test TV";
                        tray.ShowMain();
                        MainForm window = TheMainWindow();
                        Until(() => window.Answers > 0, 10, "the window never heard from the TV: " + string.Join(" / ", window.TvTexts.ToArray()));
                        Check(window.TvTexts.Count == 1 && window.TvTexts[0] == "✔  Test TV is on, showing this PC.", "the TV's line: " + window.TvTexts[0]);

                        // asked again every few seconds while the window is open
                        tv.PowerMode = "DisplayOff";
                        Until(() => window.TvTexts[0] == "Test TV is off.", 10, "the line didn't change when the TV went off: " + window.TvTexts[0]);
                        tv.PowerMode = "PowerOn";
                        tv.Down = true;
                        Until(() => window.TvTexts[0] == "⚠  Test TV isn't answering right now.", 15, "the line for a TV that doesn't answer: " + window.TvTexts[0]);
                        tv.Down = false;
                        Until(() => window.TvTexts[0].StartsWith("✔"), 10, "the line didn't come back: " + window.TvTexts[0]);

                        // never while it's out of sight
                        window.Close();
                        int answers = window.Answers;
                        DateTime settle = DateTime.UtcNow.AddMilliseconds(500);
                        while (DateTime.UtcNow < settle) { Application.DoEvents(); Thread.Sleep(20); }  // an answer already on its way
                        int asked = tv.Count("GET /query/device-info");
                        DateTime until = DateTime.UtcNow.AddMilliseconds(1200);
                        while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(20); }
                        Check(tv.Count("GET /query/device-info") == asked, "the window asked the TV " + (tv.Count("GET /query/device-info") - asked) + " times while it was out of sight");
                        // back, it asks at once
                        tray.ShowMain();
                        Until(() => window.Answers > answers + 1, 10, "the window didn't ask again when it came back");
                    });
                }

                // two TVs: a line each, by the names the menus use
                using (var a = new FakeTv("127.0.0.1", 0))
                using (var b = new FakeTv("127.0.0.2", a.Port))
                {
                    b.Serial = "X000000002";
                    b.PowerMode = "DisplayOff";
                    Settings s = TwoTvs(a, b);
                    s.Save();
                    var follower = new Follower(s, Controller.For(s));
                    Action<Form> place = Tray.PlaceWindow;
                    Tray.PlaceWindow = OffScreen;
                    try
                    {
                        using (var tray = new Tray(follower))
                        {
                            tray.ShowMain();
                            MainForm window = TheMainWindow();
                            Until(() => window.Answers > 0, 10, "the window never heard from the TVs");
                            string lines = string.Join(" / ", window.TvTexts.ToArray());
                            Check(lines == "✔  Living Room TV is on, showing this PC. / Office TV is off.", "the TVs' lines: " + lines);
                            tray.SetPaused(true);
                            Check(window.StatusText == "Paused: the TVs are left alone.", "paused with two TVs: " + window.StatusText);
                        }
                    }
                    finally
                    {
                        Tray.PlaceWindow = place;
                        foreach (MainForm left in OpenWindows<MainForm>()) left.Dispose();
                    }
                    File.Delete(Settings.FilePath);
                }
            }
            finally { MainForm.RefreshMs = every; }
        }

        static int AllTime(Settings s)
        {
            int today, all;
            SavedPresses.Read(s, out today, out all);
            return all;
        }

        static void PressesOnlyFollowing()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings s = SettingsFor(tv);
                s.PressesKept = true;   // counted from the log already, as at every start after the first
                Controller c = Fast(FirstTv(s));
                Func<string> log = () => " The log: " + LogText().Replace("\r\n", " / ");

                // the following turns the TV on from standby, onto the home screen: its power on, then the input
                tv.PowerMode = "DisplayOff";
                tv.App = "";
                c.TurnOn(TurnOnMode.Follow);
                Check(tv.IsOn && tv.App == "tvinput.hdmi1", "the TV didn't come on to the PC." + log());
                Check(AllTime(s) == 2, "on from standby to the PC: " + AllTime(s) + " presses saved, not 2." + log());
                // on and showing the PC already: nothing to do, nothing saved
                c.TurnOn(TurnOnMode.Follow);
                Check(AllTime(s) == 2, "a TV already on the PC counted: " + AllTime(s) + log());
                // one that shows the home screen on its way to the input gets the press twice: still one saved
                c.TurnOff(true, false);
                tv.App = "12";
                tv.TransitHomeMs = 5000;
                c.TurnOn(TurnOnMode.Follow);
                tv.TransitHomeMs = 0;
                Check(tv.App == "tvinput.hdmi1" && tv.Count("POST /keypress/InputHDMI1") >= 3, "the TV didn't take the input twice: " +
                    tv.Count("POST /keypress/InputHDMI1") + " presses." + log());
                Check(AllTime(s) == 4, "on, by way of the home screen: " + AllTime(s) + " presses saved, not 4." + log());
                // the monitors go off, then the PC goes to sleep
                c.TurnOff(false, false);
                Check(!tv.IsOn && AllTime(s) == 5, "the following's off: " + AllTime(s) + log());
                tv.PowerMode = "PowerOn";
                c.TurnOff(false, true);
                Check(!tv.IsOn && AllTime(s) == 6, "the quick off at sleep: " + AllTime(s) + log());

                // the buttons (the tray's, the remote's, the shortcuts') are presses someone made
                c.TurnOn(TurnOnMode.PowerOnly);
                c.TurnOff(true, false);
                tv.App = "";
                c.TurnOn(TurnOnMode.ToPc);
                Check(tv.IsOn && tv.App == "tvinput.hdmi1", "TV to PC didn't work." + log());
                Check(AllTime(s) == 6, "the buttons counted: " + AllTime(s) + log());

                // the TV saying no (Network access on Limited) saves nobody a press
                tv.ControlMode = "limited";
                c.TurnOff(false, false);
                tv.PowerMode = "DisplayOff";
                c.TurnOn(TurnOnMode.Follow);
                Check(AllTime(s) == 6, "presses the TV refused counted: " + AllTime(s) + log());
                tv.ControlMode = "enabled";

                // woken from deep standby straight onto the PC: the wake was the press
                tv.Down = true;
                tv.App = "tvinput.hdmi1";
                c.SendWake = mac => { tv.ComesUpAfter = 2; return 2; };
                c.TurnOn(TurnOnMode.Follow);
                Check(tv.IsOn && AllTime(s) == 7, "woken onto the PC: " + AllTime(s) + log());
                // one that comes up showing an app was on all along, only slow to answer: left alone
                tv.Down = true;
                tv.App = "12";
                c.TurnOn(TurnOnMode.Follow);
                Check(tv.App == "12" && AllTime(s) == 7, "a TV left alone counted: " + AllTime(s) + log());

                Settings back = Settings.Load();
                Check(back.PressesKept && back.PressesAllTime == 7, "settings.ini has " + back.PressesAllTime + " presses saved, not 7");
                // what the first start would take from these lines, as this Controller wrote them: the same
                LogAddsUpTo(7, log);
                File.Delete(Settings.FilePath);
            }

            // several TVs: one count for them all
            using (var a = new FakeTv("127.0.0.1", 0))
            using (var b = new FakeTv("127.0.0.2", a.Port))
            {
                b.Serial = "X000000002";
                b.App = "tvinput.hdmi2";
                Settings s = TwoTvs(a, b);
                s.PressesKept = true;
                List<Controller> tvs = FastTvs(s);
                Controller.Each(tvs, "off", c => c.TurnOff(false, false));
                Check(!a.IsOn && !b.IsOn && AllTime(s) == 2, "two TVs turned off: " + AllTime(s) + " presses saved, not 2");
                // then both wake from deep standby, onto the home screen: each a wake and a switch
                a.Down = b.Down = true;
                a.App = b.App = "";
                foreach (Controller c in tvs)
                {
                    FakeTv which = c.Tv.Serial == b.Serial ? b : a;
                    c.SendWake = mac => { which.ComesUpAfter = 2; return 2; };
                }
                Controller.Each(tvs, "on", c => c.TurnOn(TurnOnMode.Follow));
                Check(a.App == "tvinput.hdmi1" && b.App == "tvinput.hdmi2" && AllTime(s) == 6, "two TVs woken onto the home screen: " +
                    AllTime(s) + " presses saved, not 6. The log: " + LogText().Replace("\r\n", " / "));
                // the log has both parts' lines, the two TVs' in among each other
                LogAddsUpTo(13, () => " The log: " + LogText().Replace("\r\n", " / "));
                File.Delete(Settings.FilePath);
            }
        }

        // the log's lines, counted as the first start counts them, add up to this (all today)
        static void LogAddsUpTo(int count, Func<string> log)
        {
            int today, all;
            SavedPresses.FromLog(LogText().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries), DateTime.Now.ToString("yyyy-MM-dd"), out today, out all);
            Check(all == count && today == count, "the log adds up to " + all + " (" + today + " today), the count to " + count + "." + log());
        }

        static void PressesWrittenLater()
        {
            int after = SavedPresses.WriteAfterMs, retryMs = Settings.ReadRetryMs, retries = Settings.ReadRetries;
            // as TVFollow has them: a press written 300 ms later here, a held file tried for 3 s
            SavedPresses.WriteAfterMs = 300;
            Settings.ReadRetryMs = 250;
            Settings.ReadRetries = 12;
            try
            {
                using (var tv = new FakeTv("127.0.0.1", 0))
                {
                    Settings s = SettingsFor(tv);
                    s.PressesKept = true;
                    s.Save();
                    Controller c = Fast(FirstTv(s));
                    tv.PowerMode = "DisplayOff";
                    tv.App = "";
                    int writes = SavedPresses.Writes;
                    // another program (a sync or backup tool) holds settings.ini while the monitors come on
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    using (new FileStream(Settings.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        c.TurnOn(TurnOnMode.Follow);
                        watch.Stop();
                        Check(tv.IsOn && tv.App == "tvinput.hdmi1" && AllTime(s) == 2, "the TV didn't come on to the PC with 2 presses saved: " + AllTime(s));
                        Check(watch.ElapsedMilliseconds < 1500, "a held settings.ini held up the TV on its way to the PC: " + watch.ElapsedMilliseconds + " ms");
                    }
                    // once it's free, both are written, in one go
                    Until(() => Settings.Load().PressesAllTime == 2, 10, "the presses weren't written once settings.ini was free");
                    Thread.Sleep(700);
                    Check(SavedPresses.Writes == writes + 1, "the power on and the switch took " + (SavedPresses.Writes - writes) + " writes, not 1");

                    // a burst: one write, a moment later
                    SavedPresses.Count(s);
                    SavedPresses.Count(s);
                    SavedPresses.Count(s);
                    Check(Settings.Load().PressesAllTime == 2, "the burst was written straight away");
                    Until(() => Settings.Load().PressesAllTime == 5, 10, "the burst was never written");
                    Thread.Sleep(700);
                    Check(SavedPresses.Writes == writes + 2, "three presses together took " + (SavedPresses.Writes - writes - 1) + " writes, not 1");

                    // TVFollow quitting: what's waiting is written then and there (Program.Follow does this)
                    SavedPresses.Count(s);
                    SavedPresses.Flush();
                    Check(Settings.Load().PressesAllTime == 6, "quitting didn't write the press waiting");

                    // Windows shutting down: the following's own off, then the write, before Windows ends TVFollow
                    var follower = new Follower(s, Controller.For(s));   // not started
                    var args = new object[] { Message.Create(IntPtr.Zero, 0x16 /* WM_ENDSESSION */, (IntPtr)1, IntPtr.Zero) };
                    typeof(Follower).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(follower, args);
                    Check(!tv.IsOn, "the TV wasn't turned off at shutdown. The log: " + LogText().Replace("\r\n", " / "));
                    Settings back = Settings.Load();
                    Check(back.PressesAllTime == 7 && AllTime(s) == 7, "the off at shutdown: " + AllTime(s) + " counted, " + back.PressesAllTime + " written, not 7");
                    File.Delete(Settings.FilePath);
                }
            }
            finally
            {
                SavedPresses.Flush();
                SavedPresses.WriteAfterMs = after;
                Settings.ReadRetryMs = retryMs;
                Settings.ReadRetries = retries;
                File.Delete(Settings.FilePath);
            }
        }

        static void PressesTodayAndAllTime()
        {
            Func<DateTime> clock = SavedPresses.Now;
            DateTime now = new DateTime(2026, 11, 15, 23, 59, 0);
            SavedPresses.Now = () => now;
            int every = MainForm.RefreshMs;
            MainForm.RefreshMs = 150;
            try
            {
                using (var tv = new FakeTv("127.0.0.1", 0))
                {
                    WithTray(tv, (s, follower, tray) =>
                    {
                        int today, all;
                        s.PressesKept = true;   // counted from the log already
                        SavedPresses.Count(s);
                        SavedPresses.Count(s);
                        SavedPresses.Read(s, out today, out all);
                        Check(today == 2 && all == 2, "two presses: " + today + " today, " + all + " all time");
                        Settings back = Settings.Load();
                        Check(back.PressesKept && back.PressesToday == 2 && back.PressesAllTime == 2 && back.PressesDay == "2026-11-15",
                            "settings.ini: " + File.ReadAllText(Settings.FilePath));
                        tray.ShowMain();
                        MainForm window = TheMainWindow();
                        Check(window.PressesText == "Presses saved for you: 2 today · 2 all time", "the window says " + window.PressesText);

                        // midnight: today's starts again, before anything new is counted
                        now = now.AddMinutes(2);
                        SavedPresses.Read(s, out today, out all);
                        Check(today == 0 && all == 2, "after midnight: " + today + " today, " + all + " all time");
                        // the open window sees it at its next look, without being closed and opened again
                        Until(() => window.PressesText == "Presses saved for you: 0 today · 2 all time", 10,
                            "after midnight the open window still says 2 today");
                        SavedPresses.Count(s);
                        SavedPresses.Read(s, out today, out all);
                        Check(today == 1 && all == 3, "the next day's first press: " + today + " today, " + all + " all time");
                        back = Settings.Load();
                        Check(back.PressesToday == 1 && back.PressesAllTime == 3 && back.PressesDay == "2026-11-16", "settings.ini the next day: " + File.ReadAllText(Settings.FilePath));

                        // a TVFollow started a day later reads today's as 0, and all of them as they were
                        now = now.AddDays(1);
                        SavedPresses.Read(back, out today, out all);
                        Check(today == 0 && all == 3, "read a day later: " + today + " today, " + all + " all time");
                        // and never counts the log again
                        File.WriteAllText(Log.FilePath, "2026-11-17 08:00:00  off: power off -> 200\r\n");
                        SavedPresses.FromLogOnce(back);
                        Check(AllTime(back) == 3, "counts kept in settings.ini were counted from the log again: " + AllTime(back));
                    });
                }
            }
            finally
            {
                SavedPresses.Now = clock;
                MainForm.RefreshMs = every;
            }
        }

        static void PressesFromTheLog()
        {
            Func<DateTime> clock = SavedPresses.Now;
            // today (the Controller's own lines further down are dated by the real clock)
            DateTime now = DateTime.Today.AddHours(12);
            SavedPresses.Now = () => now;
            string day = now.ToString("yyyy-MM-dd");   // as the log writes it
            int retryMs = Settings.ReadRetryMs, retries = Settings.ReadRetries;
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                // the log's older half, then the log, as TVFollow has written them since 1.0
                File.WriteAllLines(Log.FilePath + ".old", new[]
                {
                    "2026-10-23 23:21:00  started v1.0, following the monitors",
                    "2026-10-24 08:00:00  monitors on (last input 0.1s ago)",
                    "2026-10-24 08:00:01  on: TV not answering, sent a wake packet (2 addresses)",
                    "2026-10-24 08:00:05  on: TV woke up (PowerOn)",                        // 1: woken onto the PC
                    "2026-10-24 08:00:05  on: already showing the PC",
                    "2026-10-24 23:00:00  off: sending power off",
                    "2026-10-24 23:00:00  off: power off -> 200",                           // 2
                    "2026-10-25 08:00:01  on: TV not answering, sent a wake packet (2 addresses)",
                    "2026-10-25 08:00:05  on: TV woke up (PowerOn)",                        // 3: onto the home screen (up to 1.2.1 left there)
                    "2026-10-25 08:00:05  on: TV is on and showing the Roku home screen, leaving it alone",
                    "2026-10-26 08:00:01  on: TV not answering, sent a wake packet (2 addresses)",
                    "2026-10-26 08:00:04  on: TV woke up (PowerOn)",                        // an app on: it was on all along
                    "2026-10-26 08:00:04  on: TV is on and showing 12, leaving it alone",
                    "2026-10-27 08:00:01  on: TV not answering, and there was no network to send a wake packet on",
                    "2026-10-27 08:00:09  on: TV woke up (PowerOn)",                        // no wake packet went out
                    "2026-10-27 08:00:09  on: already showing the PC"
                });
                File.WriteAllLines(Log.FilePath, new[]
                {
                    "2026-11-14 07:00:00  on: power on (was DisplayOff) -> 200",             // 4
                    "2026-11-14 07:00:02  on: switching to the PC -> 200",                  // 5
                    "2026-11-14 07:00:04  on: showing the PC",
                    "2026-11-14 08:59:58  on: TV not answering, sent a wake packet (2 addresses)",
                    "2026-11-14 09:00:00  on: TV woke up (DisplayOff)",                     // not on yet:
                    "2026-11-14 09:00:01  on: power on (was DisplayOff) -> 200",             // 6, the power on is the press
                    "2026-11-14 10:00:00  off: power off -> 403",
                    "2026-11-14 10:00:00  off: power off -> 0",
                    "2026-11-14 11:00:00  on: power on (was DisplayOff) -> 403",
                    "2026-11-14 11:00:00  on: switching to the PC -> 403",
                    "2026-11-14 11:00:00  pc: TV woke up (PowerOn)",
                    "2026-11-14 11:00:00  pc: power on (was DisplayOff) -> 200",
                    "2026-11-14 11:00:00  pc: switching to the PC -> 200",
                    "2026-11-14 11:00:00  on (button): power on (was DisplayOff) -> 200",
                    "2026-11-14 11:00:00  off (button): power off -> 200",
                    "2026-11-14 11:00:00  on: TV not answering, sent a wake packet (2 addresses)",
                    "2026-11-14 11:00:00  on: TV answered right away (PowerOn), so it didn't need waking",
                    "2026-11-14 11:00:00  off: you're back, so the TV stays on; back to the PC -> 200",
                    "2026-11-14 11:00:00  unhandled: System.Exception: something broke",
                    "   at TVFollow.Controller.TurnOff()",
                    "2026-11-14 12:00:00  on: TV not answering, sent a wake packet (2 addresses)",
                    "2026-11-14 12:00:04  on: TV woke up (PowerOn)",                        // 7: onto the home screen,
                    "2026-11-14 12:00:06  on: switching to the PC -> 200",                  // 8, then to the PC (1.3)
                    day + " 05:59:58  Den TV: on: TV not answering, sent a wake packet (2 addresses)",
                    day + " 05:59:59  Office: on: power on (was DisplayOff) -> 200",         // 9, today, with several TVs
                    day + " 06:00:00  Den TV: on: TV woke up (PowerOn)",                    // 10, today
                    day + " 06:00:03  Den TV: on: switching to the PC -> 200",              // 11, today
                    day + " 06:30:00  Office: off: power off -> 200",                       // 12, today
                    day + " 06:30:00  Office: off (button): power off -> 200",
                    day + " 06:31:00  Office: pc: power on (was DisplayOff) -> 200"
                });
                var s = new Settings();
                SavedPresses.FromLogOnce(s);
                int today, all;
                SavedPresses.Read(s, out today, out all);
                Check(all == 12 && today == 4, "from the log: " + all + " all time and " + today + " today, not 12 and 4");
                Settings back = Settings.Load();
                Check(back.PressesKept && back.PressesAllTime == 12 && back.PressesToday == 4, "not kept in settings.ini: " +
                    (File.Exists(Settings.FilePath) ? File.ReadAllText(Settings.FilePath) : "no file"));

                // once only: the log's later lines were counted as they happened, not again
                File.AppendAllText(Log.FilePath, day + " 07:00:00  off: power off -> 200\r\n");
                SavedPresses.FromLogOnce(back);
                SavedPresses.FromLogOnce(s);
                Check(AllTime(back) == 12 && AllTime(s) == 12, "the log was counted again: " + AllTime(back) + " and " + AllTime(s));

                // a log another program holds for a moment is tried again, like settings.ini
                File.Delete(Settings.FilePath);
                Settings.ReadRetryMs = 50;
                Settings.ReadRetries = 40;   // two seconds
                var held = new Settings();
                var hold = new FileStream(Log.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                using (new System.Threading.Timer(delegate { hold.Dispose(); }, null, 300, Timeout.Infinite))
                    SavedPresses.FromLogOnce(held);
                hold.Dispose();
                SavedPresses.Read(held, out today, out all);
                Check(held.PressesKept && all == 13 && today == 5, "with the log held for a moment: " + all + " all time, " + today + " today, not 13 and 5");

                // held all along: not settled, nothing written, and the next start tries again
                using (var tv = new FakeTv("127.0.0.1", 0))
                {
                    Settings.ReadRetries = 4;
                    Settings locked = SettingsFor(tv);
                    locked.Save();
                    using (new FileStream(Log.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        SavedPresses.FromLogOnce(locked);
                    Check(!locked.PressesKept && !Settings.Load().PressesKept && AllTime(locked) == 0, "with the log locked the counts were settled at " + AllTime(locked));
                    // meanwhile the following saves two presses: shown, not written (the log has them)
                    Controller c = Fast(FirstTv(locked));
                    tv.PowerMode = "DisplayOff";
                    tv.App = "";
                    c.TurnOn(TurnOnMode.Follow);
                    SavedPresses.Read(locked, out today, out all);
                    Check(all == 2 && today == 2 && !Settings.Load().PressesKept, "while not settled: " + all + " counted, " + today + " today; settings.ini " +
                        (Settings.Load().PressesKept ? "has them" : "hasn't"));
                    // the next start: the log's numbers, those two among them once, today's too
                    Settings next = Settings.Load();
                    SavedPresses.FromLogOnce(next);
                    SavedPresses.Read(next, out today, out all);
                    Check(next.PressesKept && all == 15 && today == 7, "the next start: " + all + " all time, " + today + " today, not 15 and 7. The log: " +
                        LogText().Replace("\r\n", " / "));
                    Check(Settings.Load().PressesAllTime == 15, "the next start didn't keep them");
                }

                // no log at all (a new copy just set up): 0
                File.Delete(Settings.FilePath);
                File.Delete(Log.FilePath);
                File.Delete(Log.FilePath + ".old");
                var fresh = new Settings();
                SavedPresses.FromLogOnce(fresh);
                Check(fresh.PressesKept && AllTime(fresh) == 0, "with no log: " + AllTime(fresh));
            }
            finally
            {
                SavedPresses.Now = clock;
                Settings.ReadRetryMs = retryMs;
                Settings.ReadRetries = retries;
                File.Delete(Log.FilePath + ".old");
                File.Delete(Settings.FilePath);
            }
        }
    }
}
