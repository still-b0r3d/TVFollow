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

    // The TV in the taskbar corner: TV buttons, Pause, Settings, the log and Quit.
    class Tray : IDisposable
    {
        readonly NotifyIcon icon = new NotifyIcon();
        readonly Follower follower;
        readonly Controller controller;
        readonly Icon following, paused;
        readonly ToolStripMenuItem state, pause;

        public Tray(Follower follower, Controller controller)
        {
            this.follower = follower;
            this.controller = controller;
            following = AppIcons.Load("tv.ico", SystemInformation.SmallIconSize);
            paused = AppIcons.Load("tv-paused.ico", SystemInformation.SmallIconSize);

            var menu = new ContextMenuStrip();
            state = new ToolStripMenuItem { Enabled = false };
            pause = new ToolStripMenuItem("Pause following", null, delegate { SetPaused(!follower.Rules.Paused); });
            menu.Items.Add(state);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("TV on", null, delegate { Do("on (button)", () => controller.TurnOn(TurnOnMode.PowerOnly)); });
            menu.Items.Add("TV off", null, delegate { Do("off (button)", () => controller.TurnOff(true, false)); });
            menu.Items.Add("TV to PC", null, delegate { Do("pc", () => controller.TurnOn(TurnOnMode.ToPc)); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(pause);
            menu.Items.Add("Settings…", null, delegate { OpenSettings(); });
            menu.Items.Add("Open log", null, delegate { OpenLog(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit TVFollow", null, delegate { Log.Write("quit from the tray"); Application.ExitThread(); });

            icon.ContextMenuStrip = menu;
            icon.DoubleClick += delegate { OpenSettings(); };
            Show();
            icon.Visible = true;
        }

        public void Greet()
        {
            icon.ShowBalloonTip(6000, "TVFollow is running",
                "Your TV now follows this PC's monitors. Right-click the TV icon here for TV buttons and settings.", ToolTipIcon.Info);
        }

        void SetPaused(bool value)
        {
            follower.Rules.Paused = value;
            Log.Write(value ? "paused from the tray" : "following again");
            Show();
        }

        void Show()
        {
            bool isPaused = follower.Rules.Paused;
            icon.Icon = isPaused ? paused : following;
            icon.Text = isPaused ? "TVFollow: paused" : "TVFollow: following your monitors"; // 63 characters at most
            state.Text = isPaused ? "Paused: the TV is left alone" : "Following your monitors";
            pause.Checked = isPaused;
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

        // The setup window runs as its own copy; when it's finished, it takes over from this one.
        static void OpenSettings()
        {
            try { Process.Start(Application.ExecutablePath, "setup"); }
            catch (Exception e) { Log.Write("couldn't open settings: " + e.Message); }
        }

        static void OpenLog()
        {
            try
            {
                if (!File.Exists(Log.FilePath)) Log.Write("log opened from the tray");
                Process.Start("notepad.exe", "\"" + Log.FilePath + "\"");
            }
            catch (Exception e) { Log.Write("couldn't open the log: " + e.Message); }
        }

        public void Dispose()
        {
            icon.Visible = false;
            icon.Dispose();
        }
    }
}
