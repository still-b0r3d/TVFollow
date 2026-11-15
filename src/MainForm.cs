using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TVFollow
{
    // TVFollow's own window, from the tray icon (a double-click, or Open TVFollow in its menu) or
    // by opening TVFollow again while it runs. Kept small, like LoafLock's: the TV, whether
    // TVFollow is following the monitors and how the TVs are right now, one big Pause / Resume
    // button, the remote, the presses of the remote TVFollow saved, and the version. Everything
    // else is in the menu bar: TVFollow (Settings…, Developer tools…, Quit) and Help. Closing it
    // only puts it out of sight: TVFollow carries on in the taskbar corner.
    class MainForm : Form
    {
        const int ColumnWidth = 340;   // the column everything lines up in
        const int PictureSize = 64;    // the TV: the icons' biggest picture .NET Framework can read
        const int MostTvLines = 4;     // more TVs than this share one line

        // how often the TVs are asked how they are, while the window is open (the tests shorten it)
        public static int RefreshMs = 5000;

        readonly Follower follower;
        readonly Settings settings;
        readonly Tray tray;
        readonly MenuStrip menu;
        readonly ToolStripMenuItem developerItem;
        readonly PictureBox picture;
        readonly Image followingPicture, pausedPicture;
        readonly Label status, presses, version;
        readonly List<Label> tvLines = new List<Label>();
        readonly Button pauseButton, remoteButton;
        readonly Font nameFont, buttonFont;
        readonly Timer refresh = new Timer();
        bool asking;    // the TVs are being asked right now
        bool forGood;   // closing for real (TVFollow is quitting), not just out of sight
        int answers;    // how many times the TVs' answers have come in

        static readonly object NotYet = "not yet";   // a TV line's Tag while there's no answer yet

        public MainForm(Follower follower, Tray tray)
        {
            this.follower = follower;
            this.tray = tray;
            settings = follower.Settings;

            SuspendLayout();
            Text = "TVFollow";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
            try { Icon = AppIcons.Load("tv.ico", SystemInformation.IconSize); } catch { }
            followingPicture = Picture("tv-on.ico");
            pausedPicture = Picture("tv-paused.ico");
            nameFont = Look.TitleFont(Font, 16f);
            buttonFont = new Font(Font.FontFamily, 11f, FontStyle.Bold);

            // ---- the menu bar ----
            menu = new MenuStrip();
            var app = new ToolStripMenuItem("&TVFollow");
            Item(app, "&Settings…", delegate { tray.ShowSettings(); });
            // only while the developer tools are switched on, as in the tray menu
            developerItem = Item(app, "&Developer tools…", delegate { tray.ShowDeveloperTools(null); });
            app.DropDownItems.Add(new ToolStripSeparator());
            Item(app, "&Quit TVFollow", delegate { Log.Write("quit from the window"); Application.ExitThread(); });
            // the Settings window's footer links
            var help = new ToolStripMenuItem("&Help");
            Item(help, "TVFollow &help", delegate { SetupForm.OpenUrl(SetupForm.HelpUrl); });
            Item(help, "Open &log", delegate { Tray.OpenLog(); });
            menu.Items.AddRange(new ToolStripItem[] { app, help });
            MainMenuStrip = menu;

            // ---- the window itself ----
            var body = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(24, 14, 24, 18)
            };

            // the TV, the name, whether it's following, and how the TVs are
            var top = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 14) };
            picture = new PictureBox { Size = new Size(PictureSize, PictureSize), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 14, 0) };
            top.Controls.Add(picture);
            var heading = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
            heading.Controls.Add(new Label { Text = "TVFollow", AutoSize = true, Font = nameFont, UseMnemonic = false, Margin = new Padding(0, 0, 0, 2) });
            status = Line(heading);
            int lines = settings.Tvs.Count > MostTvLines ? 1 : Math.Max(1, settings.Tvs.Count);
            for (int i = 0; i < lines; i++)
            {
                Label line = Line(heading);
                line.Text = settings.SeveralTvs ? "Asking the TVs…" : "Asking the TV…";
                line.Tag = NotYet;
                tvLines.Add(line);
            }
            top.Controls.Add(heading);
            body.Controls.Add(top);

            // one big button, like LoafLock's Lock keyboard: the tray's Pause following
            pauseButton = new Button { Size = new Size(ColumnWidth, 40), Font = buttonFont, UseVisualStyleBackColor = true, Margin = new Padding(0, 0, 0, 10) };
            pauseButton.Click += delegate { tray.SetPaused(!follower.Rules.Paused); };
            body.Controls.Add(pauseButton);

            remoteButton = new Button
            {
                Text = "Remote…",
                AutoSize = true,
                MinimumSize = new Size(120, 30),
                Padding = new Padding(8, 0, 8, 0),
                UseVisualStyleBackColor = true,
                Margin = new Padding(0, 0, 0, 14)
            };
            remoteButton.Click += delegate { tray.ShowRemote(null); };
            body.Controls.Add(remoteButton);

            presses = new Label { AutoSize = true, UseMnemonic = false, Margin = new Padding(0) };
            body.Controls.Add(presses);

            version = new Label { Text = Program.FullVersion, AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 10, 0, 0) };
            body.Controls.Add(version);

            Controls.Add(body);
            Controls.Add(menu);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();
            body.Top = menu.Bottom;  // under the menu bar

            Recolour();
            Fill();
            refresh.Interval = RefreshMs;
            refresh.Tick += delegate
            {
                if (!Looked) return;
                AskTvs();
                ShowPresses();
            };
            tray.PausedChanged += PausedChanged;
            follower.ScreensChanged += ScreensChanged;
            Disposed += delegate
            {
                tray.PausedChanged -= PausedChanged;
                follower.ScreensChanged -= ScreensChanged;
                refresh.Dispose();
            };
        }

        // Can it be seen? Not while it's minimized, nor while the monitors are off or the PC is
        // locked: a window left open overnight would ask every TV every few seconds all night.
        bool Looked { get { return WindowState != FormWindowState.Minimized && !follower.NobodyLooking; } }

        // the screens back on, or the PC unlocked: the TVs are asked again at once
        void ScreensChanged()
        {
            if (IsDisposed || !Visible || !Looked) return;
            AskTvs();
            ShowPresses();
        }

        // the window's lines: as wide as the column beside the TV, longer ones wrap
        Label Line(FlowLayoutPanel heading)
        {
            var label = new Label
            {
                AutoSize = true,
                UseMnemonic = false,   // a TV named "Tom & Jerry" keeps its &
                MaximumSize = new Size(ColumnWidth - PictureSize - 14, 0),
                Margin = new Padding(1, 2, 0, 0)
            };
            heading.Controls.Add(label);
            return label;
        }

        static Image Picture(string name)
        {
            Icon icon = AppIcons.Load(name, new Size(PictureSize, PictureSize));
            Image image = icon.ToBitmap();
            if (icon != SystemIcons.Application) icon.Dispose();
            return image;
        }

        static ToolStripMenuItem Item(ToolStripMenuItem parent, string text, EventHandler click)
        {
            var item = new ToolStripMenuItem(text, null, click);
            parent.DropDownItems.Add(item);
            return item;
        }

        // ---- what it shows ----

        // following or paused, the menu, and the presses saved
        void Fill()
        {
            bool paused = follower.Rules.Paused;
            picture.Image = paused ? pausedPicture : followingPicture;
            status.Text = !paused ? "Following your monitors."
                : settings.SeveralTvs ? "Paused: the TVs are left alone." : "Paused: the TV is left alone.";
            pauseButton.Text = paused ? "Resume following" : "Pause following";
            developerItem.Available = settings.DeveloperTools;
            ShowPresses();
        }

        // paused or resumed from anywhere: this button, the tray menu, Settings
        void PausedChanged(bool value) { Fill(); }

        // the switch in Settings (the tray passes it on)
        public void DeveloperToolsChanged() { developerItem.Available = settings.DeveloperTools; }

        void ShowPresses()
        {
            int today, all;
            SavedPresses.Read(settings, out today, out all);
            presses.Text = "Presses saved for you: " + today.ToString("N0") + " today · " + all.ToString("N0") + " all time";
        }

        // Every TV at once, off the window's thread: the lines change when all have answered.
        // Not again while that's going on.
        async void AskTvs()
        {
            if (asking || IsDisposed) return;
            asking = true;
            var tvs = new List<TvSettings>(settings.Tvs);
            int port = settings.Port;
            TvCheck[] checks = null;
            try { checks = await Task.Run(() => AskAll(tvs, port)); }
            catch (Exception e) { Log.Write("couldn't ask the TVs how they are: " + e.Message); }
            finally { asking = false; }
            if (checks == null || IsDisposed) return;
            ShowTvs(tvs, checks);
            answers++;
        }

        static TvCheck[] AskAll(List<TvSettings> tvs, int port)
        {
            Task<TvCheck>[] asks = tvs.ConvertAll(tv => Task.Run(() =>
            {
                try { return TvCheck.Ask(tv, port, false); }
                catch { return new TvCheck { State = TvCheck.Kind.NoAnswer }; }
            })).ToArray();
            Task.WaitAll(asks);
            return Array.ConvertAll(asks, ask => ask.Result);
        }

        void ShowTvs(List<TvSettings> tvs, TvCheck[] checks)
        {
            if (tvLines.Count == tvs.Count)
            {
                for (int i = 0; i < tvs.Count; i++)
                    SetLine(tvLines[i], TvCheck.Mark(checks[i].State), TvCheck.Sentence(checks[i].State, NameOf(tvs[i])));
                return;
            }
            // many TVs: how many are how
            int pc = 0, on = 0, off = 0, limited = 0, silent = 0;
            foreach (TvCheck check in checks)
            {
                switch (check.State)
                {
                    case TvCheck.Kind.OnPc: pc++; break;
                    case TvCheck.Kind.OnHome: case TvCheck.Kind.OnOther: on++; break;
                    case TvCheck.Kind.Off: off++; break;
                    case TvCheck.Kind.Limited: limited++; break;
                    default: silent++; break;
                }
            }
            var parts = new List<string>();
            if (pc > 0) parts.Add(pc + " showing this PC");
            if (on > 0) parts.Add(on + " on, showing something else");
            if (off > 0) parts.Add(off + " off");
            if (limited > 0) parts.Add(limited + " with Network access on Limited");
            if (silent > 0) parts.Add(silent + " not answering");
            bool? ok = limited + silent > 0 ? (bool?)false : pc == checks.Length && pc > 0 ? (bool?)true : null;
            SetLine(tvLines[0], ok, tvs.Count + " TVs: " + string.Join(", ", parts.ToArray()) + ".");
        }

        // what a TV is called on its line: as the menus call it, or "Your TV" for one TV with no name
        string NameOf(TvSettings tv)
        {
            return settings.SeveralTvs || tv.Name.Trim().Length > 0 ? settings.LabelOf(tv) : "Your TV";
        }

        static void SetLine(Label line, bool? ok, string text)
        {
            line.Text = TvCheck.Marked(ok, text);
            line.Tag = ok;
            Colour(line);
        }

        // a TV's line: green with ✔, amber with ⚠, grey while it's being asked, else plain
        static void Colour(Label line)
        {
            if (line.Tag == NotYet) line.ForeColor = Look.Quiet;
            else if (line.Tag is bool) line.ForeColor = (bool)line.Tag ? Look.Good : Look.Warn;
            else line.ForeColor = SystemColors.WindowText;
        }

        // the coloured words, in the colours Look gives them; again when Windows' colours
        // change (a high contrast theme going on or off)
        void Recolour()
        {
            status.ForeColor = Look.Quiet;
            version.ForeColor = Look.Quiet;
            foreach (Label line in tvLines) Colour(line);
        }

        protected override void OnSystemColorsChanged(EventArgs e)
        {
            base.OnSystemColorsChanged(e);
            if (version != null) Recolour();
        }

        // ---- open, out of sight, closed ----

        // the TVs are asked as the window comes up, then every few seconds while it's up
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                Fill();
                AskTvs();
                refresh.Start();
            }
            else refresh.Stop();
        }

        // Closing it puts it out of sight; TVFollow carries on, and the window comes back as it
        // was. Windows shutting down, or TVFollow quitting, closes it for real.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel || forGood || e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            Hide();
        }

        public void CloseForGood()
        {
            forGood = true;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                followingPicture.Dispose();
                pausedPicture.Dispose();
                nameFont.Dispose();
                buttonFont.Dispose();
            }
            base.Dispose(disposing);
        }

        // ---- what the tests look at ----

        internal string StatusText { get { return status.Text; } }
        internal string ButtonText { get { return pauseButton.Text; } }
        internal string PressesText { get { return presses.Text; } }
        internal string VersionLine { get { return version.Text; } }
        internal List<string> TvTexts { get { return tvLines.ConvertAll(l => l.Text); } }
        internal bool ShowsPaused { get { return picture.Image == pausedPicture; } }
        internal int Answers { get { return answers; } }
        internal Button PauseButton { get { return pauseButton; } }
        internal Button RemoteButton { get { return remoteButton; } }

        // an item of the menu bar's menus, by its words without the & marks
        internal ToolStripMenuItem MenuItem(string text)
        {
            foreach (ToolStripItem top in menu.Items)
            {
                var drop = top as ToolStripMenuItem;
                if (drop == null) continue;
                foreach (ToolStripItem item in drop.DropDownItems)
                    if (item is ToolStripMenuItem && item.Text.Replace("&", "") == text) return (ToolStripMenuItem)item;
            }
            return null;
        }
    }
}
