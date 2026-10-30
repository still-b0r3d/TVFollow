using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TVFollow
{
    static class AppIcons
    {
        // one of the icons built into the exe, at the size given
        public static Icon Load(string name, Size size)
        {
            using (Stream res = Assembly.GetExecutingAssembly().GetManifestResourceStream("TVFollow." + name))
                return res == null ? SystemIcons.Application : new Icon(res, size);
        }
    }

    // The TV in the taskbar corner: the remote, TV buttons, Pause, Settings, the log and Quit.
    // With several TVs the TV buttons do every TV, and each TV has a menu of its own.
    class Tray : IDisposable
    {
        readonly NotifyIcon icon = new NotifyIcon();
        readonly Follower follower;
        readonly List<Controller> tvs;
        readonly Icon following, paused;
        readonly ToolStripMenuItem state, pause;
        readonly List<KeyValuePair<ToolStripMenuItem, Controller>> tvMenus = new List<KeyValuePair<ToolStripMenuItem, Controller>>();
        ToolStripMenuItem update;
        string balloonUrl; // where clicking the current notification goes, if anywhere
        RemoteForm remote;
        Controller remoteTv; // the TV the remote was last on, so it opens on it again
        SettingsForm settingsWindow;

        public event Action<bool> PausedChanged = delegate { };

        public Tray(Follower follower)
        {
            this.follower = follower;
            tvs = follower.Controllers;
            following = AppIcons.Load("tv.ico", SystemInformation.SmallIconSize);
            paused = AppIcons.Load("tv-paused.ico", SystemInformation.SmallIconSize);

            var menu = new ContextMenuStrip { ShowImageMargin = false };
            state = new ToolStripMenuItem { Enabled = false };
            pause = new ToolStripMenuItem("Pause following", null, delegate { SetPaused(!follower.Rules.Paused); });
            var remoteItem = new ToolStripMenuItem("Remote…", null, delegate { ShowRemote(null); });
            remoteItem.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(state);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(remoteItem);
            bool several = tvs.Count > 1;
            menu.Items.Add(several ? "All TVs on" : "TV on", null, delegate { Do("on (button)", tvs, c => c.TurnOn(TurnOnMode.PowerOnly)); });
            menu.Items.Add(several ? "All TVs off" : "TV off", null, delegate { Do("off (button)", tvs, c => c.TurnOff(true, false)); });
            menu.Items.Add(several ? "All TVs to PC" : "TV to PC", null, delegate { Do("pc", tvs, c => c.TurnOn(TurnOnMode.ToPc)); });
            if (several) foreach (Controller tv in tvs) menu.Items.Add(TvMenu(tv));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(pause);
            menu.Items.Add("Settings…", null, delegate { ShowSettings(); });
            menu.Items.Add("Open log", null, delegate { OpenLog(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit TVFollow", null, delegate { Log.Write("quit from the tray"); Application.ExitThread(); });
            // a TV renamed while TVFollow runs (the Settings window notices) shows its new name
            menu.Opening += delegate { foreach (var pair in tvMenus) pair.Key.Text = MenuText(pair.Value.Label); };

            icon.ContextMenuStrip = menu;
            icon.DoubleClick += delegate { ShowSettings(); };
            icon.BalloonTipClicked += delegate { if (balloonUrl != null) SetupForm.OpenUrl(balloonUrl); };
            Show();
            icon.Visible = true;
        }

        public ContextMenuStrip Menu { get { return icon.ContextMenuStrip; } }

        // one TV's own buttons, and its remote
        ToolStripMenuItem TvMenu(Controller tv)
        {
            var item = new ToolStripMenuItem(MenuText(tv.Label));
            var one = new List<Controller> { tv };
            item.DropDownItems.Add("TV on", null, delegate { Do("on (button)", one, c => c.TurnOn(TurnOnMode.PowerOnly)); });
            item.DropDownItems.Add("TV off", null, delegate { Do("off (button)", one, c => c.TurnOff(true, false)); });
            item.DropDownItems.Add("TV to PC", null, delegate { Do("pc", one, c => c.TurnOn(TurnOnMode.ToPc)); });
            item.DropDownItems.Add(new ToolStripSeparator());
            item.DropDownItems.Add("Remote…", null, delegate { ShowRemote(tv); });
            ((ToolStripDropDownMenu)item.DropDown).ShowImageMargin = false;
            tvMenus.Add(new KeyValuePair<ToolStripMenuItem, Controller>(item, tv));
            return item;
        }

        // a menu shows & as an underline under the next letter; a TV named "Tom & Jerry" keeps it
        public static string MenuText(string text) { return text.Replace("&", "&&"); }

        // A newer TVFollow is out: a menu item from now on, and a notification if notify is set.
        public void ShowUpdate(string version, string url, bool notify)
        {
            if (update == null)
            {
                update = new ToolStripMenuItem { Font = new Font(icon.ContextMenuStrip.Font, FontStyle.Bold) };
                update.Click += delegate { SetupForm.OpenUrl((string)update.Tag); };
                icon.ContextMenuStrip.Items.Insert(1, update);
            }
            update.Text = "Get TVFollow " + version + "…";
            update.Tag = url;
            if (!notify) return;
            balloonUrl = url;
            icon.ShowBalloonTip(10000, "TVFollow " + version + " is out",
                "Click here to see what's new and download it. You're on " + Program.Version + ".", ToolTipIcon.Info);
            Log.Write("told about TVFollow " + version);
        }

        public void Greet()
        {
            balloonUrl = null;
            icon.ShowBalloonTip(6000, "TVFollow is running",
                (tvs.Count > 1 ? "Your TVs now follow" : "Your TV now follows") +
                " this PC's monitors. Right-click the TV icon here for the remote and settings.", ToolTipIcon.Info);
        }

        public void SetPaused(bool value)
        {
            follower.Rules.Paused = value;
            Log.Write(value ? "paused" : "following again");
            // kept in the settings, so a restart or a trip through setup doesn't undo it
            follower.Settings.Paused = value;
            try { follower.Settings.Save(); }
            catch (Exception e) { Log.Write("couldn't save the pause: " + e.Message); }
            Show();
            PausedChanged(value);
        }

        void Show()
        {
            bool isPaused = follower.Rules.Paused;
            icon.Icon = isPaused ? paused : following;
            icon.Text = isPaused ? "TVFollow: paused" : "TVFollow: following your monitors"; // 63 characters at most
            state.Text = isPaused ? (tvs.Count > 1 ? "Paused: the TVs are left alone" : "Paused: the TV is left alone") : "Following your monitors";
            pause.Text = isPaused ? "Resume following" : "Pause following";
        }

        // TV buttons talk to the network, so they run off the window's thread, each TV at once
        static void Do(string what, List<Controller> which, Action<Controller> action)
        {
            Task.Run(() => Controller.Each(which, what, action));
        }

        // One remote at a time; opening it again brings it to the front. tv: the TV it should be
        // on (a TV's own menu), or null for the TV it was on last.
        public void ShowRemote(Controller tv)
        {
            if (remote != null && !remote.IsDisposed)
            {
                if (tv != null) remote.ShowTv(tv);
                remote.Activate();
                return;
            }
            remote = new RemoteForm(follower.Settings, tvs, tv ?? remoteTv ?? tvs[0], follower.Settings.RemoteLayout);
            RemoteForm opened = remote;
            opened.FormClosed += delegate { remoteTv = opened.Tv; };
            opened.Show();
            opened.Activate();
        }

        // a new layout picked in Settings: an open remote changes to it, on the same TV
        public void RemoteLayoutChanged()
        {
            if (remote == null || remote.IsDisposed || remote.LayoutName == follower.Settings.RemoteLayout) return;
            remote.Close();
            ShowRemote(null);
        }

        public void ShowSettings()
        {
            if (settingsWindow != null && !settingsWindow.IsDisposed) { settingsWindow.Activate(); return; }
            settingsWindow = new SettingsForm(follower.Settings, follower, this);
            settingsWindow.Show();
            settingsWindow.Activate();
        }

        public static void OpenLog()
        {
            try
            {
                if (!File.Exists(Log.FilePath)) Log.Write("log opened");
                // Notepad by its full path, never whatever "notepad.exe" sits in the current folder
                Process.Start(Path.Combine(Environment.SystemDirectory, "notepad.exe"), "\"" + Log.FilePath + "\"");
            }
            catch (Exception e) { Log.Write("couldn't open the log: " + e.Message); }
        }

        public void Dispose()
        {
            if (remote != null && !remote.IsDisposed) remote.Close();
            if (settingsWindow != null && !settingsWindow.IsDisposed) settingsWindow.Close();
            icon.Visible = false;
            icon.Dispose();
        }
    }
}
