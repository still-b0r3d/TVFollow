using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TVFollow
{
    // The Settings window of the running TVFollow (tray → Settings…, or opening TVFollow again):
    // a page for each part, picked from the list down its left side, like Windows' own Settings.
    // Every change applies and saves right away. Adding, removing or changing TVs, or switching
    // between installed and portable, runs the setup wizard. The developer tools are pages here
    // too (DevPages): the Developer tools page has their switch, and the two app pages are listed
    // only while it's on.
    class SettingsForm : Form
    {
        public const string TvsPage = "tvs", FollowingPage = "following", RemotePage = "remote", PcPage = "pc",
            UpdatesPage = "updates", DeveloperPage = "developer", AppPage = "app", AppOptionsPage = "options";

        readonly Settings settings;
        readonly Follower follower;
        readonly Tray tray;
        readonly NavList nav = new NavList();
        readonly Panel side = new Panel { Dock = DockStyle.Left, Width = 200, Padding = new Padding(8, 12, 8, 8) };   // the list's strip
        readonly Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 52 };
        LinkLabel about;   // the footer's two lines
        readonly Panel pages = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.Window };
        readonly Dictionary<string, Column> pageOf = new Dictionary<string, Column>();
        readonly List<NavList.Page> allPages = new List<NavList.Page>();
        readonly List<TvLines> tvs = new List<TvLines>();
        readonly Label mode, updateResult, developerAbout;
        readonly CheckBox off, on, paused, startup, shortcuts, updates, developer;
        readonly RadioButton classic, compact, strip;
        readonly Font titleFont;
        readonly DevPages dev;   // null when there are no developer tools to show (some tests)
        Column building;         // the page the helpers below add to
        string shown;            // the page on show
        bool loading = true;

        // one TV's part of the TVs page: its name and address, how it is right now, and its input
        class TvLines
        {
            public TvSettings Tv;
            public string Number = "";  // "2.  " in front of its name when there are several
            public Label Name, State;
            public ComboBox Input;
        }

        // how the TV's address is shown (the screenshot tool shows a home-network one)
        public static Func<string, string> ShownAddress = ip => ip;

        public SettingsForm(Settings settings, Follower follower, Tray tray)
        {
            this.settings = settings;
            this.follower = follower;
            this.tray = tray;

            SuspendLayout();
            Text = "TVFollow settings";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(780, 560);
            BackColor = SystemColors.Window;
            try { Icon = AppIcons.Load("tv.ico", SystemInformation.IconSize); } catch { }
            titleFont = TitleFont(Font);

            bool several = settings.SeveralTvs;
            Page(TvsPage, several ? "TVs" : "TV", (char)0xE7F4,
                several ? "The TVs that turn on and off with this PC's monitors." : "The TV that turns on and off with this PC's monitors.");
            for (int i = 0; i < settings.Tvs.Count; i++) tvs.Add(AddTv(settings.Tvs[i], several ? (i + 1) + ".  " : ""));
            Row().Controls.Add(MakeButton("Add or change TVs…", delegate { RunSetup(); }));

            Page(FollowingPage, "Following", (char)0xE7E8, "What " + (several ? "the TVs do" : "the TV does") + " when this PC's monitors turn off and on.");
            off = Check(several ? "Turn the TVs off when this PC's monitors turn off" : "Turn the TV off when this PC's monitors turn off",
                delegate { settings.OffWithMonitors = off.Checked; follower.Rules.OffWithMonitors = off.Checked; Save(); });
            on = Check(several ? "Turn the TVs on and switch them to this PC when you come back" : "Turn the TV on and switch it to this PC when you come back",
                delegate { settings.OnWithMonitors = on.Checked; follower.Rules.OnWithMonitors = on.Checked; Save(); });
            paused = Check(several ? "Paused (leave the TVs alone until resumed)" : "Paused (leave the TV alone until resumed)",
                delegate { if (paused.Checked != follower.Rules.Paused) tray.SetPaused(paused.Checked); });

            Page(RemotePage, "Remote", (char)0xE8EA, "The remote that opens from the TV icon in the taskbar corner.");
            Heading("Layout");
            FlowLayoutPanel layouts = Row();
            classic = Choice(layouts, "Classic (tall)", RemoteForm.Classic);
            compact = Choice(layouts, "Compact (square)", RemoteForm.Compact);
            strip = Choice(layouts, "Wide strip", RemoteForm.Strip);
            FlowLayoutPanel openRow = Row();
            openRow.Margin = new Padding(0, 12, 0, 8);
            openRow.Controls.Add(MakeButton("Open remote", delegate { tray.ShowRemote(null); }));

            Page(PcPage, "This PC", (char)0xE770, "How TVFollow is set up on this PC.");
            mode = Line("");
            startup = Check("Start TVFollow when you sign in to Windows", delegate { settings.StartWithWindows = startup.Checked; Save(); ApplyInstall(); });
            shortcuts = Check("Put TV on, TV off and TV to PC shortcuts on the desktop", delegate { settings.DesktopShortcuts = shortcuts.Checked; Save(); ApplyInstall(); });
            var setupLink = new LinkLabel { Text = "Switch between installed and portable: run setup again", AutoSize = true, Margin = new Padding(0, 10, 0, 12) };
            setupLink.LinkClicked += delegate { RunSetup(); };
            building.Controls.Add(setupLink);

            Page(UpdatesPage, "Updates", (char)0xE895, "New versions of TVFollow come from its page on GitHub.");
            updates = Check("Tell me when there's a new version (checks GitHub once a week)", delegate { settings.CheckUpdates = updates.Checked; Save(); });
            FlowLayoutPanel updateRow = Row();
            updateRow.Margin = new Padding(0, 8, 0, 8);
            updateRow.Controls.Add(MakeButton("Check now", delegate { CheckNow(); }));
            updateResult = new Label { AutoSize = true, Margin = new Padding(4, 8, 0, 0), ForeColor = SetupPage.Muted };
            updateRow.Controls.Add(updateResult);

            Page(DeveloperPage, "Developer tools", (char)0xEC7A, "For putting your own apps on the TV. Most people never need them.");
            developer = Check("Turn on the developer tools", delegate { DeveloperChanged(); });
            developerAbout = Line("");
            developerAbout.ForeColor = SetupPage.Muted;
            developerAbout.Margin = new Padding(20, 0, 0, 10);
            if (tray.Dev != null)
            {
                Column app = Page(AppPage, "App on the TV", (char)0xE7B8, "The app you put on the TV in developer mode.");
                Column options = Page(AppOptionsPage, "App options", (char)0xE713, "What TVFollow does for the app while this PC is on.");
                dev = new DevPages(tray.Dev, this, pageOf[DeveloperPage], app, options, tray.Dev.Tvs[0]);
            }

            nav.Dock = DockStyle.Fill;
            nav.Font = Font;
            nav.SelectedIndexChanged += delegate { if (nav.Selected != null) Reveal(nav.Selected.Key); };
            side.Controls.Add(nav);
            BuildFooter();
            Recolour();

            // docked in reverse: the footer across the bottom, then the list down the left, then the pages
            Controls.Add(pages);
            Controls.Add(side);
            Controls.Add(footer);
            // Tab goes the way the window reads: the list, the page that's open, the footer.
            // (Left as added, it went from the list to the footer, and reached the page last.)
            side.TabIndex = 0;
            pages.TabIndex = 1;
            footer.TabIndex = 2;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();

            Fill();
            ListPages();
            ShowPage(TvsPage);
            tray.PausedChanged += SyncPause;
            FormClosed += delegate
            {
                tray.PausedChanged -= SyncPause;
                if (dev != null) dev.Dispose();
            };
            Load += delegate { foreach (TvLines tv in tvs) LoadTv(tv); };
            // the list has the focus, so the arrow keys move between pages straight away
            Shown += delegate { nav.Focus(); };
        }

        // the pages' titles: Segoe UI Semibold where Windows has it
        static Font TitleFont(Font body)
        {
            var font = new Font("Segoe UI Semibold", 15f);
            if (font.Name == "Segoe UI Semibold") return font;
            font.Dispose();
            return new Font(body.FontFamily, 15f, FontStyle.Bold);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) titleFont.Dispose();
            base.Dispose(disposing);
        }

        // ---- pages ----

        // the pages listed down the left: all of them, but the app pages only while the developer
        // tools are switched on
        void ListPages()
        {
            string was = shown;
            nav.BeginUpdate();
            nav.Items.Clear();
            foreach (NavList.Page page in allPages)
                if (settings.DeveloperTools || (page.Key != AppPage && page.Key != AppOptionsPage)) nav.Items.Add(page);
            nav.EndUpdate();
            if (was != null) ShowPage(pageOf.ContainsKey(was) && Listed(was) ? was : DeveloperPage);
        }

        bool Listed(string key)
        {
            foreach (NavList.Page page in nav.Pages) if (page.Key == key) return true;
            return false;
        }

        // Shows a page (the tray's Developer tools… opens on one). An app page while the tools are
        // switched off opens the Developer tools page, where their switch is.
        public void ShowPage(string key)
        {
            if (!Listed(key)) key = pageOf.ContainsKey(key) ? DeveloperPage : TvsPage;
            for (int i = 0; i < nav.Items.Count; i++)
                if (((NavList.Page)nav.Items[i]).Key == key && nav.SelectedIndex != i) nav.SelectedIndex = i;
            Reveal(key);
        }

        void Reveal(string key)
        {
            if (key == shown) return;
            shown = key;
            SuspendLayout();
            foreach (KeyValuePair<string, Column> pair in pageOf) pair.Value.Visible = pair.Key == key;
            ResumeLayout(true);
            if (dev != null) dev.Active = key == DeveloperPage || key == AppPage || key == AppOptionsPage;
        }

        // for the tray: the developer tools on one TV (null for the one already shown)
        public void ShowDeveloperTools(Controller tv)
        {
            if (dev != null && tv != null) dev.ShowTv(tray.Dev.For(tv));
            ShowPage(settings.DeveloperTools ? AppPage : DeveloperPage);
        }

        // for the tests and the screenshots
        public string CurrentPage { get { return shown; } }
        public List<string> ListedPages { get { return nav.Pages.ConvertAll(p => p.Key); } }
        public DevPages Dev { get { return dev; } }

        // ---- building ----

        // a new page, with its title and a line about it, listed down the left; the helpers
        // below add to it from here on
        Column Page(string key, string name, char icon, string about)
        {
            var page = new Column { Padding = new Padding(28, 18, 28, 12), Visible = false, BackColor = SystemColors.Window };
            pageOf[key] = page;
            allPages.Add(new NavList.Page(key, name, icon));
            pages.Controls.Add(page);
            building = page;
            page.Controls.Add(new Label { Text = name, AutoSize = true, Font = titleFont, UseMnemonic = false, Margin = new Padding(0, 0, 0, 4) });
            Label line = Line(about);
            line.ForeColor = SetupPage.Muted;
            line.Margin = new Padding(0, 0, 0, 16);
            return page;
        }

        TvLines AddTv(TvSettings tv, string number)
        {
            var lines = new TvLines { Tv = tv, Number = number };
            lines.Name = Line("");
            lines.Name.Font = new Font(Font, FontStyle.Bold);
            lines.State = Line("");
            FlowLayoutPanel inputRow = Row();
            inputRow.Controls.Add(new Label { Text = "Input this PC is on:", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
            lines.Input = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Margin = new Padding(0, 2, 8, 0) };
            lines.Input.SelectedIndexChanged += delegate { InputChanged(lines); };
            inputRow.Controls.Add(lines.Input);
            inputRow.Controls.Add(MakeButton("Show it on the TV", delegate { ShowOnTv(lines.Tv); }));
            inputRow.Margin = new Padding(0, 0, 0, 18); // a gap before the next TV, or the button
            return lines;
        }

        void Heading(string text)
        {
            building.Controls.Add(new Label { Text = text, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 6, 0, 6) });
        }

        Label Line(string text)
        {
            // UseMnemonic off: a TV named "Tom & Jerry" keeps its &
            var label = new Label { Text = text, AutoSize = true, Margin = new Padding(0, 0, 0, 6), UseMnemonic = false };
            building.Controls.Add(label);
            return label;
        }

        FlowLayoutPanel Row()
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            building.Controls.Add(row);
            return row;
        }

        CheckBox Check(string text, EventHandler changed)
        {
            var box = new CheckBox { Text = text, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            box.CheckedChanged += (s, e) => { if (!loading) changed(s, e); };
            building.Controls.Add(box);
            return box;
        }

        RadioButton Choice(FlowLayoutPanel row, string text, string layout)
        {
            var radio = new RadioButton { Text = text, AutoSize = true, Tag = layout, Margin = new Padding(0, 0, 16, 0) };
            radio.CheckedChanged += delegate
            {
                if (loading || !radio.Checked) return;
                settings.RemoteLayout = layout;
                Save();
                tray.RemoteLayoutChanged();
            };
            row.Controls.Add(radio);
            return radio;
        }

        static Button MakeButton(string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(0, 28), Padding = new Padding(8, 0, 8, 0), UseVisualStyleBackColor = true, Margin = new Padding(0, 0, 8, 0) };
            b.Click += click;
            return b;
        }

        void BuildFooter()
        {
            // two lines, like setup's: the version with its build and the links don't fit on one
            about = new LinkLabel
            {
                AutoSize = true,
                Location = new Point(20, 10),
                Text = "TVFollow " + Program.FullVersion + "\r\nHelp  ·  Open log  ·  made by " + SetupForm.Maker,
                ActiveLinkColor = SystemColors.HotTrack,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            string text = about.Text;
            about.Links.Clear();
            about.Links.Add(text.IndexOf("Help"), 4, SetupForm.HelpUrl);
            about.Links.Add(text.IndexOf("Open log"), 8, "log");
            about.Links.Add(text.LastIndexOf(SetupForm.Maker), SetupForm.Maker.Length, SetupForm.MakerUrl);
            about.LinkClicked += (s, e) =>
            {
                if ((string)e.Link.LinkData == "log") Tray.OpenLog();
                else SetupForm.OpenUrl((string)e.Link.LinkData);
            };
            var close = new Button { Text = "Close", AutoSize = true, MinimumSize = new Size(92, 30), UseVisualStyleBackColor = true, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            close.Location = new Point(footer.Width - 112, 11);
            close.Click += delegate { Close(); };
            CancelButton = close;
            footer.Controls.Add(about);
            footer.Controls.Add(close);
        }

        // The grey parts, the list's strip and the footer, in the colours Look gives them. Again
        // whenever Windows' colours change: a high contrast theme going on or off changes them.
        void Recolour()
        {
            side.BackColor = NavList.Back;
            footer.BackColor = Look.Footer;
            about.LinkColor = Look.FooterLink;
        }

        protected override void OnSystemColorsChanged(EventArgs e)
        {
            base.OnSystemColorsChanged(e);
            if (about != null) Recolour();
        }

        // ---- filling in ----

        void Fill()
        {
            loading = true;
            foreach (TvLines tv in tvs)
            {
                tv.Name.Text = tv.Number + (tv.Tv.Name.Length > 0 ? tv.Tv.Name + "  ·  " : "TV at ") + ShownAddress(tv.Tv.Ip);
                tv.State.Text = "Asking the TV…";
                tv.State.ForeColor = SetupPage.Muted;
                SetInputs(tv, null, tv.Tv.PcApp);
            }
            off.Checked = settings.OffWithMonitors;
            on.Checked = settings.OnWithMonitors;
            paused.Checked = follower.Rules.Paused;
            classic.Checked = settings.RemoteLayout == RemoteForm.Classic;
            compact.Checked = settings.RemoteLayout == RemoteForm.Compact;
            strip.Checked = settings.RemoteLayout == RemoteForm.Strip;
            if (!compact.Checked && !strip.Checked) classic.Checked = true;
            startup.Checked = settings.StartWithWindows;
            shortcuts.Checked = settings.DesktopShortcuts;
            updates.Checked = settings.CheckUpdates;
            developer.Checked = settings.DeveloperTools;
            ShowDeveloperState();
            bool portable = Settings.IsPortable;
            mode.Text = portable
                ? "Portable: settings are in the \"" + Settings.PortableFolderName + "\" folder next to TVFollow.exe."
                : "Installed for this Windows account.";
            startup.Enabled = shortcuts.Enabled = !portable;
            loading = false;
        }

        // the line under the switch, and the developer mode part of the page with it
        void ShowDeveloperState()
        {
            bool on = settings.DeveloperTools;
            developerAbout.Text = on
                ? "Their other pages, App on the TV and App options, are listed on the left. The tray menu's Developer tools… opens them too."
                : "Turned on, they help with the TV's developer mode, put your app on the TV, and can open the app or switch the TV's inputs for it.";
            if (dev != null) dev.ShowModePart(on);
        }

        void SyncPause(bool value)
        {
            loading = true;
            paused.Checked = value;
            loading = false;
        }

        async void LoadTv(TvLines tv)
        {
            var client = new RokuClient(tv.Tv.Ip, settings.Port);
            DeviceInfo info = null;
            List<KeyValuePair<string, string>> inputs = null;
            string showing = null;
            await Task.Run(() =>
            {
                info = client.GetDeviceInfo(2500);
                if (info != null)
                {
                    inputs = client.GetHdmiInputs(2500);
                    showing = client.GetActiveApp(2500);
                }
            });
            if (IsDisposed) return;
            if (info == null)
            {
                SetState(tv, false, "Not answering right now. Is it on, and on this network?");
                return;
            }
            if (info.Serial != tv.Tv.Serial)
            {
                // the router gave the TV's address to something else: the TV gets looked for when
                // TVFollow next needs it, and its name stays as it was
                SetState(tv, false, "Another Roku answers at this address right now. TVFollow looks for your TV by itself when it needs it.");
                return;
            }
            if (info.Name.Length > 0 && info.Name != tv.Tv.Name)
            {
                // renamed on the TV: the menus and the log call it by its new name from now on
                tv.Tv.Name = info.Name;
                Save();
            }
            string where = info.Location.Length > 0 ? "  ·  " + info.Location : "";
            tv.Name.Text = tv.Number + (info.Name.Length > 0 ? info.Name : "TV") + where + "  ·  " + ShownAddress(tv.Tv.Ip);
            if (!info.AllowsControl)
                SetState(tv, false, "Network access is on Limited, so TVFollow can't press its buttons. On the TV: Settings → System → " +
                    "Advanced system settings → Control by mobile apps → Network access → Enabled.");
            else if (!info.IsOn) SetState(tv, null, "Off right now.");
            else if (showing == tv.Tv.PcApp) SetState(tv, true, "On, showing this PC.");
            else if (showing == "") SetState(tv, null, "On, showing Roku's home screen.");
            else SetState(tv, null, "On, showing something else.");
            if (inputs != null && inputs.Count > 0)
            {
                loading = true;
                SetInputs(tv, inputs, tv.Tv.PcApp);
                loading = false;
            }
        }

        static void SetState(TvLines tv, bool? ok, string text)
        {
            tv.State.Text = (ok == null ? "" : ok.Value ? "✔  " : "⚠  ") + text;
            tv.State.ForeColor = ok == null ? SystemColors.ControlText : ok.Value ? SetupPage.Good : SetupPage.Warn;
        }

        class InputItem
        {
            public string Id, Text;
            public override string ToString() { return Text; }
        }

        static void SetInputs(TvLines tv, List<KeyValuePair<string, string>> inputs, string selected)
        {
            ComboBox input = tv.Input;
            input.Items.Clear();
            if (inputs == null)
            {
                inputs = new List<KeyValuePair<string, string>>();
                for (int i = 1; i <= 4; i++) inputs.Add(new KeyValuePair<string, string>("tvinput.hdmi" + i, ""));
            }
            foreach (KeyValuePair<string, string> pair in inputs)
            {
                string hdmi = "HDMI " + pair.Key.Substring("tvinput.hdmi".Length);
                var item = new InputItem { Id = pair.Key, Text = pair.Value.Length == 0 || pair.Value == hdmi ? hdmi : hdmi + "  ·  " + pair.Value };
                input.Items.Add(item);
                if (pair.Key == selected) input.SelectedItem = item;
            }
            if (input.SelectedIndex < 0 && input.Items.Count > 0) input.SelectedIndex = 0;
        }

        // ---- changes ----

        void Save()
        {
            try { settings.Save(); }
            catch (Exception e) { Log.Write("couldn't save the settings: " + e.Message); }
        }

        void InputChanged(TvLines tv)
        {
            var item = tv.Input.SelectedItem as InputItem;
            if (loading || item == null) return;
            tv.Tv.Input = item.Id.Substring("tvinput.".Length);
            Log.Write("settings: " + (settings.SeveralTvs ? settings.LabelOf(tv.Tv) + "'s " : "") + "input " + tv.Tv.Input);
            Save();
        }

        void DeveloperChanged()
        {
            settings.DeveloperTools = developer.Checked;
            Log.Write(developer.Checked ? "settings: developer tools on" : "settings: developer tools off");
            Save();
            ShowDeveloperState();
            ListPages();
            tray.DeveloperToolsChanged();
        }

        void ShowOnTv(TvSettings tv)
        {
            var client = new RokuClient(tv.Ip, settings.Port);
            string id = tv.PcApp;
            DevTv tools = tray.Dev != null ? tray.Dev.Tvs.Find(d => d.Tv == tv) : null;
            if (tools != null) tools.Controller.NoteInputSwitch();
            Task.Run(() => client.Launch(id, 3000));
        }

        // start-with-Windows and the desktop shortcuts are part of the install
        void ApplyInstall()
        {
            if (Settings.IsPortable) return;
            try { Installer.Install(InstallPaths.ForThisPC(), settings, Application.ExecutablePath); }
            catch (Exception e)
            {
                Log.Write("couldn't update the install: " + e.Message);
                MessageBox.Show(this, "Couldn't change that: " + e.Message, "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // the setup wizard, as its own copy: when it finishes it takes over from this one
        void RunSetup()
        {
            try
            {
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, "setup")
                {
                    WorkingDirectory = Settings.ExeDir,
                    UseShellExecute = false
                });
                Close();
            }
            catch (Exception e) { Log.Write("couldn't open setup: " + e.Message); }
        }

        async void CheckNow()
        {
            updateResult.Text = "Asking GitHub…";
            Release release = await Task.Run(() => Updates.FetchLatest());
            if (IsDisposed) return;
            if (release == null) updateResult.Text = "Couldn't reach GitHub.";
            else if (release.Tag == null || !Updates.IsNewer(release.Version, Program.Version)) updateResult.Text = "You have the latest version.";
            else
            {
                updateResult.Text = "TVFollow " + release.Version + " is out.";
                if (Updates.IsOurPage(release.Url)) tray.ShowUpdate(release.Version, release.Url, false);
            }
        }

        // for the tests
        public void Choose(string layout)
        {
            (layout == RemoteForm.Compact ? compact : layout == RemoteForm.Strip ? strip : classic).Checked = true;
        }
    }
}
