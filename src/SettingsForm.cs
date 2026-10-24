using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TVFollow
{
    // The Settings window of the running TVFollow (tray → Settings…, or opening TVFollow again).
    // Every change applies and saves right away. Changing the TV, or switching between installed
    // and portable, runs the setup wizard.
    class SettingsForm : Form
    {
        readonly Settings settings;
        readonly Follower follower;
        readonly Tray tray;
        readonly Column body = new Column();
        readonly Label tvName, tvState, mode, updateResult;
        readonly ComboBox input;
        readonly CheckBox off, on, paused, startup, shortcuts, updates;
        readonly RadioButton classic, compact, strip;
        bool loading = true;

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
            ClientSize = new Size(520, 640);
            BackColor = SystemColors.Window;
            try { Icon = AppIcons.Load("tv.ico", SystemInformation.IconSize); } catch { }
            body.Padding = new Padding(24, 16, 24, 8);

            Heading("TV");
            tvName = Line("");
            tvState = Line("");
            FlowLayoutPanel inputRow = Row();
            inputRow.Controls.Add(new Label { Text = "Input this PC is on:", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
            input = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Margin = new Padding(0, 2, 8, 0) };
            input.SelectedIndexChanged += delegate { InputChanged(); };
            inputRow.Controls.Add(input);
            inputRow.Controls.Add(MakeButton("Show it on the TV", delegate { ShowOnTv(); }));
            Row().Controls.Add(MakeButton("Change TV…", delegate { RunSetup(); }));

            Heading("Following");
            off = Check("Turn the TV off when this PC's monitors turn off", delegate { settings.OffWithMonitors = off.Checked; follower.Rules.OffWithMonitors = off.Checked; Save(); });
            on = Check("Turn the TV on and switch it to this PC when you come back", delegate { settings.OnWithMonitors = on.Checked; follower.Rules.OnWithMonitors = on.Checked; Save(); });
            paused = Check("Paused (leave the TV alone until resumed)", delegate { if (paused.Checked != follower.Rules.Paused) tray.SetPaused(paused.Checked); });

            Heading("Remote");
            FlowLayoutPanel layouts = Row();
            classic = Choice(layouts, "Classic (tall)", RemoteForm.Classic);
            compact = Choice(layouts, "Compact (square)", RemoteForm.Compact);
            strip = Choice(layouts, "Wide strip", RemoteForm.Strip);
            Row().Controls.Add(MakeButton("Open remote", delegate { tray.ShowRemote(); }));

            Heading("This PC");
            mode = Line("");
            startup = Check("Start TVFollow when you sign in to Windows", delegate { settings.StartWithWindows = startup.Checked; Save(); ApplyInstall(); });
            shortcuts = Check("Put TV on, TV off and TV to PC shortcuts on the desktop", delegate { settings.DesktopShortcuts = shortcuts.Checked; Save(); ApplyInstall(); });
            var setupLink = new LinkLabel { Text = "Switch between installed and portable: run setup again", AutoSize = true, Margin = new Padding(0, 2, 0, 12) };
            setupLink.LinkClicked += delegate { RunSetup(); };
            body.Controls.Add(setupLink);

            Heading("Updates");
            updates = Check("Tell me when there's a new version (checks GitHub once a week)", delegate { settings.CheckUpdates = updates.Checked; Save(); });
            FlowLayoutPanel updateRow = Row();
            updateRow.Controls.Add(MakeButton("Check now", delegate { CheckNow(); }));
            updateResult = new Label { AutoSize = true, Margin = new Padding(4, 8, 0, 0), ForeColor = SetupPage.Muted };
            updateRow.Controls.Add(updateResult);

            Controls.Add(body);
            Controls.Add(Footer());

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();

            Fill();
            tray.PausedChanged += SyncPause;
            FormClosed += delegate { tray.PausedChanged -= SyncPause; };
            Load += delegate { LoadTv(); };
        }

        // ---- building ----

        void Heading(string text)
        {
            var label = new Label { Text = text, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 6, 0, 6) };
            body.Controls.Add(label);
        }

        Label Line(string text)
        {
            var label = new Label { Text = text, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            body.Controls.Add(label);
            return label;
        }

        FlowLayoutPanel Row()
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            body.Controls.Add(row);
            return row;
        }

        CheckBox Check(string text, EventHandler changed)
        {
            var box = new CheckBox { Text = text, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            box.CheckedChanged += (s, e) => { if (!loading) changed(s, e); };
            body.Controls.Add(box);
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

        Control Footer()
        {
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Color.FromArgb(243, 243, 243) };
            var about = new LinkLabel
            {
                AutoSize = true,
                Location = new Point(20, 18),
                Text = "TVFollow " + Program.Version + "  ·  Help  ·  Open log  ·  made by " + SetupForm.Maker,
                LinkColor = SetupPage.Muted,
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
            return footer;
        }

        // ---- filling in ----

        void Fill()
        {
            loading = true;
            tvName.Text = "TV at " + ShownAddress(settings.Ip);
            tvState.Text = "Asking the TV…";
            tvState.ForeColor = SetupPage.Muted;
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
            bool portable = Settings.IsPortable;
            mode.Text = portable
                ? "Portable: settings are in the \"" + Settings.PortableFolderName + "\" folder next to TVFollow.exe."
                : "Installed for this Windows account.";
            startup.Enabled = shortcuts.Enabled = !portable;
            SetInputs(null, "tvinput." + settings.Input);
            loading = false;
        }

        void SyncPause(bool value)
        {
            loading = true;
            paused.Checked = value;
            loading = false;
        }

        async void LoadTv()
        {
            var client = new RokuClient(settings.Ip, settings.Port);
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
                SetState(false, "Not answering right now. Is it on, and on this network?");
                return;
            }
            string where = info.Location.Length > 0 ? "  ·  " + info.Location : "";
            tvName.Text = (info.Name.Length > 0 ? info.Name : "TV") + where + "  ·  " + ShownAddress(settings.Ip);
            if (!info.AllowsControl)
                SetState(false, "Network access is on Limited, so TVFollow can't press its buttons. On the TV: Settings → System → " +
                    "Advanced system settings → Control by mobile apps → Network access → Enabled.");
            else if (!info.IsOn) SetState(null, "Off right now.");
            else if (showing == settings.PcApp) SetState(true, "On, showing this PC.");
            else SetState(null, "On, showing something else.");
            if (inputs != null && inputs.Count > 0)
            {
                loading = true;
                SetInputs(inputs, settings.PcApp);
                loading = false;
            }
        }

        void SetState(bool? ok, string text)
        {
            tvState.Text = (ok == null ? "" : ok.Value ? "✔  " : "⚠  ") + text;
            tvState.ForeColor = ok == null ? SystemColors.ControlText : ok.Value ? SetupPage.Good : SetupPage.Warn;
        }

        class InputItem
        {
            public string Id, Text;
            public override string ToString() { return Text; }
        }

        void SetInputs(List<KeyValuePair<string, string>> inputs, string selected)
        {
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

        void InputChanged()
        {
            var item = input.SelectedItem as InputItem;
            if (loading || item == null) return;
            settings.Input = item.Id.Substring("tvinput.".Length);
            Log.Write("settings: input " + settings.Input);
            Save();
        }

        void ShowOnTv()
        {
            var client = new RokuClient(settings.Ip, settings.Port);
            string id = settings.PcApp;
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
