using System;
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
    class Tray : IDisposable
    {
        readonly NotifyIcon icon = new NotifyIcon();
        readonly Follower follower;
        readonly Controller controller;
        readonly Icon following, paused;
        readonly ToolStripMenuItem state, pause;
        ToolStripMenuItem update;
        string balloonUrl; // where clicking the current notification goes, if anywhere
        RemoteForm remote;
        SettingsForm settingsWindow;

        public event Action<bool> PausedChanged = delegate { };

        public Tray(Follower follower, Controller controller)
        {
            this.follower = follower;
            this.controller = controller;
            following = AppIcons.Load("tv.ico", SystemInformation.SmallIconSize);
            paused = AppIcons.Load("tv-paused.ico", SystemInformation.SmallIconSize);

            var menu = new ContextMenuStrip { ShowImageMargin = false };
            state = new ToolStripMenuItem { Enabled = false };
            pause = new ToolStripMenuItem("Pause following", null, delegate { SetPaused(!follower.Rules.Paused); });
            var remoteItem = new ToolStripMenuItem("Remote…", null, delegate { ShowRemote(); });
            remoteItem.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(state);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(remoteItem);
            menu.Items.Add("TV on", null, delegate { Do("on (button)", () => controller.TurnOn(TurnOnMode.PowerOnly)); });
            menu.Items.Add("TV off", null, delegate { Do("off (button)", () => controller.TurnOff(true, false)); });
            menu.Items.Add("TV to PC", null, delegate { Do("pc", () => controller.TurnOn(TurnOnMode.ToPc)); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(pause);
            menu.Items.Add("Settings…", null, delegate { ShowSettings(); });
            menu.Items.Add("Open log", null, delegate { OpenLog(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit TVFollow", null, delegate { Log.Write("quit from the tray"); Application.ExitThread(); });

            icon.ContextMenuStrip = menu;
            icon.DoubleClick += delegate { ShowSettings(); };
            icon.BalloonTipClicked += delegate { if (balloonUrl != null) SetupForm.OpenUrl(balloonUrl); };
            Show();
            icon.Visible = true;
        }

        public ContextMenuStrip Menu { get { return icon.ContextMenuStrip; } }

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
                "Your TV now follows this PC's monitors. Right-click the TV icon here for the remote and settings.", ToolTipIcon.Info);
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
            state.Text = isPaused ? "Paused: the TV is left alone" : "Following your monitors";
            pause.Text = isPaused ? "Resume following" : "Pause following";
        }

        // TV buttons talk to the network, so they run off the window's thread
        static void Do(string what, Action action)
        {
            Task.Run(() =>
            {
                try { action(); }
                catch (Exception e) { Log.Write(what + ": error: " + e.Message); }
            });
        }

        // one remote at a time; opening it again brings it to the front
        public void ShowRemote()
        {
            if (remote != null && !remote.IsDisposed) { remote.Activate(); return; }
            remote = new RemoteForm(follower.Settings, controller, follower.Settings.RemoteLayout);
            remote.Show();
            remote.Activate();
        }

        // a new layout picked in Settings: an open remote changes to it
        public void RemoteLayoutChanged()
        {
            if (remote == null || remote.IsDisposed || remote.LayoutName == follower.Settings.RemoteLayout) return;
            remote.Close();
            ShowRemote();
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
