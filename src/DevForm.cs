using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TVFollow
{
    // The developer tools window (Developer tools… in Settings or the tray menu): developer mode on
    // one TV, the password chosen on it, the app pushed to it, and the two background options.
    class DevForm : Form
    {
        const string Steps =
            "To turn it on, use the TV's own remote. The TV ignores the code below when TVFollow sends it, " +
            "so TVFollow's remote can't do it.\r\n" +
            "    1.  Press Home 3 times, Up 2 times, then Right, Left, Right, Left, Right.\r\n" +
            "    2.  On the Developer settings screen, choose Enable installer and restart, agree to Roku's " +
            "license, and pick a password.\r\n" +
            "    3.  The TV restarts. Type the same password below.\r\n" +
            "If the TV asks, first sign up for Roku's free developer program at developer.roku.com, with the " +
            "Roku account the TV uses. TVFollow keeps checking, and says here when it's on.";

        const string OffSteps = "To turn it off, press the same code on the TV's remote and choose Disable installer and restart.";

        public static int PollMs = 4000;

        readonly DevTools tools;
        readonly Settings settings;
        DevTv tv;   // the TV the window is on
        readonly Column body = new Column();
        readonly ComboBox picker;   // several TVs only
        readonly Label modeState, modeSteps, passwordResult, appState, result, folderLine;
        readonly TextBox password;
        readonly CheckBox showPassword, pushBack, onSave;
        readonly Button check, pushZip, pushFolder, pushExample, open, remove, screenshot, appLog;
        readonly Timer poll = new Timer();
        readonly List<KeyValuePair<DevTv, Action<DevReport>>> hooks = new List<KeyValuePair<DevTv, Action<DevReport>>>();
        bool loading, asking, busy;
        DevStatus last;
        DevLogForm log;

        public DevForm(DevTools tools, DevTv first)
        {
            this.tools = tools;
            settings = tools.Settings;
            tv = first;

            SuspendLayout();
            Text = "TVFollow developer tools";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(580, 700);
            BackColor = SystemColors.Window;
            try { Icon = AppIcons.Load("tv.ico", SystemInformation.IconSize); } catch { }
            body.Padding = new Padding(24, 16, 24, 8);

            if (tools.Tvs.Count > 1)
            {
                FlowLayoutPanel row = Row();
                row.Controls.Add(new Label { Text = "TV:", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
                picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320, Margin = new Padding(0, 2, 0, 0) };
                foreach (DevTv t in tools.Tvs) picker.Items.Add(t.Label);
                picker.SelectedIndex = Math.Max(0, tools.Tvs.IndexOf(tv));
                picker.SelectedIndexChanged += delegate { if (!loading && picker.SelectedIndex >= 0) Switch(tools.Tvs[picker.SelectedIndex]); };
                row.Controls.Add(picker);
            }
            Muted(Line("Developer mode lets you put your own Roku apps on the TV. While it's on, the TV runs an app " +
                "installer on your home network, which only opens with the password you choose."));

            Heading("Developer mode");
            modeState = Line("Asking the TV…");
            modeSteps = Muted(Line(""));

            Heading("Password");
            Muted(Line("The one you chose on the TV when you turned developer mode on. TVFollow keeps it encrypted " +
                "for your Windows account."));
            FlowLayoutPanel passwordRow = Row();
            password = new TextBox { Width = 220, UseSystemPasswordChar = true, Margin = new Padding(0, 3, 10, 0) };
            password.TextChanged += delegate { if (!loading) PasswordChanged(); };
            showPassword = new CheckBox { Text = "Show", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
            showPassword.CheckedChanged += delegate { password.UseSystemPasswordChar = !showPassword.Checked; };
            check = MakeButton("Check", delegate { Run(t => t.CheckPassword(), passwordResult); });
            passwordRow.Controls.Add(password);
            passwordRow.Controls.Add(showPassword);
            passwordRow.Controls.Add(check);
            passwordResult = Line("");

            Heading("App on the TV");
            appState = Line("");
            FlowLayoutPanel pushRow = Row();
            pushZip = MakeButton("Push a .zip…", delegate { PushZip(); });
            pushFolder = MakeButton("Push a folder…", delegate { PushFolder(); });
            pushExample = MakeButton("Push the example app", delegate { PushApp(() => DevPackage.Example(), ""); });
            pushRow.Controls.Add(pushZip);
            pushRow.Controls.Add(pushFolder);
            pushRow.Controls.Add(pushExample);
            FlowLayoutPanel appRow = Row();
            open = MakeButton("Open on the TV", delegate { Run(t => t.Open(), result); });
            remove = MakeButton("Remove", delegate { Remove(); });
            screenshot = MakeButton("Screenshot…", delegate { Screenshot(); });
            appLog = MakeButton("App log…", delegate { ShowLog(); });
            appRow.Controls.Add(open);
            appRow.Controls.Add(remove);
            appRow.Controls.Add(screenshot);
            appRow.Controls.Add(appLog);
            result = Line("");

            Heading("Options");
            pushBack = Check("Put the app back when it disappears from the TV", delegate
            {
                tv.Tv.DevPushBack = pushBack.Checked;
                Save();
            });
            Muted(Line("TVFollow keeps a copy of the app you pushed last. If the TV loses it (after a Roku update, say), " +
                "TVFollow puts it back the next time it turns the TV off with your monitors.")).Margin = new Padding(20, 0, 0, 8);
            onSave = Check("Push the app's folder again whenever a file in it is saved", delegate
            {
                tv.Tv.DevOnSave = onSave.Checked;
                Save();
                tools.Refresh();
                ShowFolder();
            });
            folderLine = Muted(Line(""));
            folderLine.Margin = new Padding(20, 0, 0, 8);

            Controls.Add(body);
            Controls.Add(Footer());
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();

            Fill();
            foreach (DevTv t in tools.Tvs) Hook(t);
            poll.Interval = PollMs;
            poll.Tick += delegate { Ask(); };
            Load += delegate
            {
                // on a small screen, as tall as fits; the rest scrolls
                int most = Screen.FromControl(this).WorkingArea.Height - (Height - ClientSize.Height) - 20;
                if (ClientSize.Height > most)
                {
                    ClientSize = new Size(ClientSize.Width, Math.Max(360, most));
                    if (StartPosition == FormStartPosition.CenterScreen) CenterToScreen();
                }
                Ask();
                poll.Start();
            };
            // not straight into the password box with it all selected
            Shown += delegate { ActiveControl = null; };
            FormClosed += delegate
            {
                poll.Stop();
                poll.Dispose();
                foreach (var pair in hooks) pair.Key.Background -= pair.Value;
                if (log != null && !log.IsDisposed) log.Close();
            };
        }

        public DevTv Tv { get { return tv; } }

        // ---- building ----

        void Heading(string text)
        {
            body.Controls.Add(new Label { Text = text, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 8, 0, 6) });
        }

        Label Line(string text)
        {
            var label = new Label { Text = text, AutoSize = true, Margin = new Padding(0, 0, 0, 6), UseMnemonic = false };
            body.Controls.Add(label);
            return label;
        }

        static Label Muted(Label label)
        {
            label.ForeColor = SetupPage.Muted;
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
            var box = new CheckBox { Text = text, AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
            box.CheckedChanged += (s, e) => { if (!loading) changed(s, e); };
            body.Controls.Add(box);
            return box;
        }

        static Button MakeButton(string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(0, 28), Padding = new Padding(6, 0, 6, 0), UseVisualStyleBackColor = true, Margin = new Padding(0, 0, 8, 0) };
            b.Click += click;
            return b;
        }

        Control Footer()
        {
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Color.FromArgb(243, 243, 243) };
            var help = new LinkLabel
            {
                AutoSize = true,
                Location = new Point(20, 18),
                Text = "How the developer tools work",
                LinkColor = SetupPage.Muted,
                ActiveLinkColor = SystemColors.HotTrack,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            help.LinkClicked += delegate { SetupForm.OpenUrl(SetupForm.HelpUrl + "#developer-tools"); };
            var close = new Button { Text = "Close", AutoSize = true, MinimumSize = new Size(92, 30), UseVisualStyleBackColor = true, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            close.Location = new Point(footer.Width - 112, 11);
            close.Click += delegate { Close(); };
            CancelButton = close;
            footer.Controls.Add(help);
            footer.Controls.Add(close);
            return footer;
        }

        // ---- filling in ----

        void Fill()
        {
            loading = true;
            password.Text = tv.Tv.DevPassword;
            pushBack.Checked = tv.Tv.DevPushBack;
            onSave.Checked = tv.Tv.DevOnSave;
            loading = false;
            last = null;
            modeState.Text = "Asking the TV…";
            modeState.ForeColor = SetupPage.Muted;
            modeSteps.Text = "";
            appState.Text = "";
            passwordResult.Text = "";
            result.Text = "";
            ShowFolder();
            UpdateButtons();
        }

        void ShowFolder()
        {
            string folder = tv.Tv.DevFolder;
            onSave.Enabled = folder.Length > 0;
            folderLine.Text = folder.Length == 0
                ? "Push a folder first. This works on the folder you pushed last."
                : (tv.Tv.DevOnSave && tv.Watching ? "Watching " : "The folder: ") + folder;
        }

        // the TV's state, asked every few seconds while the window is open
        async void Ask()
        {
            if (asking || IsDisposed) return;
            asking = true;
            DevTv on = tv;
            DevStatus s = await Task.Run(() => on.Status(2500));
            asking = false;
            if (IsDisposed) return;
            // the window moved to another TV meanwhile: ask about that one
            if (on != tv) { Ask(); return; }
            last = s;
            ShowStatus(s);
        }

        void ShowStatus(DevStatus s)
        {
            if (s.OtherRoku) Set(modeState, false, "Another Roku answers at this TV's address right now. TVFollow finds your TV again the next time it turns it on or off.");
            else if (!s.Answering) Set(modeState, false, "The TV isn't answering. Is it on, and on this network?");
            else if (s.DevMode) Set(modeState, true, "Developer mode is on.");
            else Set(modeState, null, "Developer mode is off.");
            modeSteps.Text = !s.Answering ? "" : s.DevMode ? OffSteps : Steps;

            if (!s.Answering) appState.Text = "";
            else if (!s.DevMode) appState.Text = "Apps can be pushed once developer mode is on.";
            else if (!s.AppKnown) appState.Text = "The TV won't say which app is on it" + (s.Limited ? " while Network access is on Limited." : " right now.");
            else appState.Text = s.App == null ? "No pushed app on the TV." : "On the TV now: " + s.App;
            ShowFolder();
            UpdateButtons();
        }

        static void Set(Label label, bool? ok, string text)
        {
            label.Text = (ok == null ? "" : ok.Value ? "✔  " : "⚠  ") + text;
            label.ForeColor = ok == null ? SystemColors.ControlText : ok.Value ? SetupPage.Good : SetupPage.Warn;
        }

        static void Show(Label label, DevReport r)
        {
            Set(label, r.Ok, r.Text.Length > 0 ? r.Text : (r.Ok ? "Done." : "That didn't work."));
        }

        void UpdateButtons()
        {
            bool ready = !busy && last != null && last.Answering && last.DevMode;
            bool app = ready && (last.App != null || !last.AppKnown);
            check.Enabled = pushZip.Enabled = pushFolder.Enabled = pushExample.Enabled = appLog.Enabled = ready;
            open.Enabled = remove.Enabled = screenshot.Enabled = app;
        }

        // ---- changes ----

        void Switch(DevTv to)
        {
            tv = to;
            Fill();
            Ask();
        }

        void Save()
        {
            try { settings.Save(); }
            catch (Exception e) { Log.Write("couldn't save the settings: " + e.Message); }
        }

        void PasswordChanged()
        {
            tv.Tv.DevPassword = password.Text;
            passwordResult.Text = "";
            Save();
        }

        // a push on save or an app put back, from the background
        void Hook(DevTv t)
        {
            Action<DevReport> shown = r =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        if (IsDisposed || t != tv) return;
                        Show(result, r);
                        Ask();
                    }));
                }
                catch (InvalidOperationException) { }  // closing
            };
            t.Background += shown;
            hooks.Add(new KeyValuePair<DevTv, Action<DevReport>>(t, shown));
        }

        async void Run(Func<DevTv, DevReport> work, Label where)
        {
            if (busy) return;
            busy = true;
            UpdateButtons();
            Set(where, null, "Asking the TV…");
            where.ForeColor = SetupPage.Muted;
            DevTv on = tv;
            DevReport r = await Task.Run(() => work(on));
            busy = false;
            if (IsDisposed) return;
            if (on == tv) Show(where, r);
            UpdateButtons();
            Ask();
        }

        void PushZip()
        {
            using (var dialog = new OpenFileDialog { Title = "Push an app to the TV", Filter = "Roku app (*.zip)|*.zip|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string path = dialog.FileName;
                PushApp(() => DevPackage.FromZip(path), "");
            }
        }

        void PushFolder()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Pick your app's folder: the one with the manifest file in it.", ShowNewFolderButton = false })
            {
                if (tv.Tv.DevFolder.Length > 0 && Directory.Exists(tv.Tv.DevFolder)) dialog.SelectedPath = tv.Tv.DevFolder;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string path = dialog.SelectedPath;
                PushApp(() => DevPackage.FromFolder(path), null);
            }
        }

        // Makes the app (off this thread: a folder can take a moment to zip) and pushes it.
        // folder: "" when it's a zip or the example, so pushing on save stops; null for a folder,
        // whose own path is then remembered.
        public async void PushApp(Func<DevPackage> make, string folder)
        {
            if (busy) return;
            busy = true;
            UpdateButtons();
            Set(result, null, "Getting the app ready…");
            result.ForeColor = SetupPage.Muted;
            DevTv on = tv;
            DevPackage app = null;
            string problem = null;
            await Task.Run(() =>
            {
                try { app = make(); }
                catch (DevPackageException e) { problem = e.Message; }
                catch (Exception e) { problem = "Couldn't read it: " + e.Message; }
            });
            DevReport r;
            if (app == null) r = new DevReport(false, problem);
            else
            {
                if (!IsDisposed && on == tv)
                {
                    Set(result, null, "Pushing " + app + " (" + DevPackage.Size(app.Zip.Length) + ")…");
                    result.ForeColor = SetupPage.Muted;
                }
                r = await Task.Run(() => on.Push(app, "pushed"));
                // pushing on save works on the folder pushed last
                on.Tv.DevFolder = folder ?? app.Root;
                Save();
                tools.Refresh();
            }
            busy = false;
            if (IsDisposed) return;
            if (on == tv) Show(result, r);
            ShowFolder();
            UpdateButtons();
            Ask();
        }

        void Remove()
        {
            string what = last != null && last.App != null ? last.App.ToString() : "the app";
            if (MessageBox.Show(this, "Take " + what + " off the TV? TVFollow forgets its copy too, so it won't put it back.",
                "TVFollow", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Run(t => t.Remove(), result);
        }

        async void Screenshot()
        {
            if (busy) return;
            busy = true;
            UpdateButtons();
            Set(result, null, "Asking the TV for a picture…");
            result.ForeColor = SetupPage.Muted;
            DevTv on = tv;
            string appName = last != null && last.App != null ? last.App.Name : "";
            byte[] picture = null;
            string extension = "";
            DevReport r = await Task.Run(() =>
            {
                byte[] p;
                string e;
                DevReport got = on.Screenshot(out p, out e);
                picture = p;
                extension = e;
                return got;
            });
            busy = false;
            if (IsDisposed) return;
            UpdateButtons();
            if (on != tv) return;   // the window moved to another TV meanwhile
            if (!r.Ok) { Show(result, r); return; }
            string name = FileSafe(appName) + " " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + "." + extension;
            using (var dialog = new SaveFileDialog
            {
                Title = "Save the picture",
                FileName = name.Trim(),
                Filter = extension == "png" ? "PNG picture (*.png)|*.png" : "JPEG picture (*.jpg)|*.jpg",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) { result.Text = ""; return; }
                try
                {
                    File.WriteAllBytes(dialog.FileName, picture);
                    Show(result, new DevReport(true, "Saved the picture: " + dialog.FileName));
                }
                catch (Exception e) { Show(result, new DevReport(false, "Couldn't save it: " + e.Message)); }
            }
        }

        static string FileSafe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Length > 0 ? name : "App";
        }

        void ShowLog()
        {
            if (log != null && !log.IsDisposed)
            {
                if (log.Ip == tv.Tv.Ip) { log.Activate(); return; }
                log.Close();
            }
            log = new DevLogForm(tv.Tv.Ip, tv.Label);
            log.Show();
        }
    }
}
