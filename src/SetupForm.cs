using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TVFollow
{
    // The setup window: find the TV, let this PC control it, pick the input, test, finish.
    // Opens on first run, and any time TVFollow is opened while it's already running.
    class SetupForm : Form
    {
        public const string HelpUrl = "https://github.com/still-b0r3d/TVFollow";
        public const string MakerUrl = "https://github.com/still-b0r3d";
        public const string Maker = "still b0r3d";

        public readonly Settings Settings;  // filled in step by step, saved on Finish
        public FoundTv Tv;                   // picked on the first page
        public string InputName = "";        // what the TV calls the picked input, for the last page
        public bool Portable;                // chosen on the last page: run from this folder, install nothing
        public Func<List<FoundTv>> Scan;     // how the first page looks for TVs (tests swap it)
        public PowerTest Test = new PowerTest();

        readonly List<SetupPage> pages = new List<SetupPage>();
        int current = -1;
        readonly Label title = new Label();
        readonly Label stepText = new Label();
        readonly Button back = new Button();
        readonly Button next = new Button();
        readonly Button cancel = new Button();

        public SetupForm(Settings settings)
        {
            Settings = settings;
            Scan = () => TvScanner.FindAll(Settings.Port, Settings.Ip);

            SuspendLayout();
            Text = "TVFollow setup";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(640, 500);
            BackColor = SystemColors.Window;
            // the TV icon built into the program, whichever exe is running (the tests' has no icon)
            try { Icon = AppIcons.Load("tv.ico", SystemInformation.IconSize); } catch { }

            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 16, 28, 8), BackColor = SystemColors.Window };
            pages.Add(new FindPage(this));
            pages.Add(new ControlPage(this));
            pages.Add(new InputPage(this));
            pages.Add(new TestPage(this));
            pages.Add(new FinishPage(this));
            foreach (SetupPage page in pages)
            {
                page.Visible = false;
                host.Controls.Add(page);
            }

            Controls.Add(host);
            Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(225, 225, 225) });
            Controls.Add(BuildHeader());
            Controls.Add(BuildFooter());

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();

            Load += delegate { ShowPage(0); };
            FormClosing += (s, e) =>
            {
                // only a running test is worth waiting for (it may have the TV switched off), and
                // never when Windows itself is signing out or shutting down
                if (DialogResult != DialogResult.OK && CurrentPage is TestPage && CurrentPage.Busy &&
                    e.CloseReason != CloseReason.WindowsShutDown && e.CloseReason != CloseReason.TaskManagerClosing)
                {
                    MessageBox.Show(this, "Wait for the test to finish first, so the TV isn't left off.", "TVFollow",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Cancel = true;
                }
            };
        }

        public SetupPage CurrentPage { get { return current >= 0 ? pages[current] : null; } }

        public RokuClient Client { get { return new RokuClient(Tv.Ip, Settings.Port); } }

        Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = SystemColors.Window };
            var picture = new PictureBox { Location = new Point(28, 16), Size = new Size(40, 40), SizeMode = PictureBoxSizeMode.Zoom };
            try { picture.Image = AppIcons.Load("tv.ico", new Size(48, 48)).ToBitmap(); } catch { }
            title.AutoSize = true;
            title.Location = new Point(80, 14);
            title.Font = new Font("Segoe UI Semibold", 14F);
            stepText.AutoSize = true;
            stepText.Location = new Point(82, 44);
            stepText.ForeColor = SetupPage.Muted;
            header.Controls.Add(picture);
            header.Controls.Add(title);
            header.Controls.Add(stepText);
            return header;
        }

        Control BuildFooter()
        {
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.FromArgb(243, 243, 243) };
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 12, 20, 0),
                BackColor = footer.BackColor
            };
            foreach (Button b in new[] { cancel, next, back })
            {
                b.AutoSize = true;
                b.MinimumSize = new Size(92, 30);
                b.Margin = new Padding(8, 0, 0, 0);
                b.UseVisualStyleBackColor = true;
                buttons.Controls.Add(b);
            }
            cancel.Text = "Cancel";
            next.Text = "Next";
            back.Text = "Back";
            // tab in the order they're on screen (they're laid out right to left)
            back.TabIndex = 0;
            next.TabIndex = 1;
            cancel.TabIndex = 2;
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            next.Click += delegate { GoNext(); };
            back.Click += delegate { GoBack(); };
            CancelButton = cancel;

            var about = new LinkLabel
            {
                AutoSize = true,
                Location = new Point(24, 20),
                Text = "TVFollow " + Program.Version + "  ·  Help  ·  made by " + Maker,
                LinkColor = SetupPage.Muted,
                ActiveLinkColor = SystemColors.HotTrack,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            string text = about.Text;
            about.Links.Clear();
            about.Links.Add(text.IndexOf("Help"), 4, HelpUrl);
            about.Links.Add(text.LastIndexOf(Maker), Maker.Length, MakerUrl);
            about.LinkClicked += (s, e) => OpenUrl((string)e.Link.LinkData);

            footer.Controls.Add(buttons);
            footer.Controls.Add(about);
            return footer;
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(url); } catch { }
        }

        void ShowPage(int index)
        {
            if (current >= 0)
            {
                pages[current].Leaving();
                pages[current].Visible = false;
            }
            current = index;
            SetupPage page = pages[current];
            title.Text = page.Title;
            stepText.Text = "Step " + (current + 1) + " of " + pages.Count;
            page.Visible = true;
            page.Entered();
            UpdateButtons();
            // Hiding the old page would hand focus to the next control in tab order, which could
            // be Cancel, and Enter would then throw the setup away. Focus the new page instead.
            if (!page.SelectNextControl(null, true, true, true, false)) FocusNext();
        }

        public void GoNext()
        {
            SetupPage page = CurrentPage;
            if (page == null || !page.CanGoNext || page.Busy) return;
            if (current < pages.Count - 1) ShowPage(current + 1);
            else Finish();
        }

        public void GoBack()
        {
            SetupPage page = CurrentPage;
            if (page == null || current == 0 || page.Busy) return;
            ShowPage(current - 1);
        }

        public void UpdateButtons()
        {
            SetupPage page = CurrentPage;
            if (page == null) return;
            bool last = current == pages.Count - 1;
            back.Enabled = current > 0 && !page.Busy;
            next.Text = last ? "Finish" : "Next";
            next.Enabled = page.CanGoNext && !page.Busy;
            AcceptButton = next.Enabled ? next : null;
            if (cancel.Focused) FocusNext();
        }

        // Next if it's ready; otherwise nothing, so Enter waits for Next (the AcceptButton)
        void FocusNext()
        {
            if (next.Enabled) next.Focus();
            else ActiveControl = null;
        }

        // for the tests: the footer buttons in tab order
        public Button[] FooterButtons { get { return new[] { back, next, cancel }; } }

        void Finish()
        {
            Settings.Ip = Tv.Ip;
            Settings.Serial = Tv.Info.Serial;
            Settings.WakeMac = Tv.Info.WakeMac;
            // the settings go where the chosen mode keeps them (a trial copy keeps its own folder)
            if (!TVFollow.Settings.IsTrial) TVFollow.Settings.Dir = Portable ? TVFollow.Settings.PortableDir : TVFollow.Settings.DefaultDir;
            try { Settings.Save(); }
            catch (Exception e)
            {
                MessageBox.Show(this, "Couldn't save the settings: " + e.Message, "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Log.Write("setup saved: input " + Settings.Input);
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // A top-to-bottom column that wraps its labels and stretches controls tagged "stretch".
    class Column : FlowLayoutPanel
    {
        public Column()
        {
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoScroll = true;
            Dock = DockStyle.Fill;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            int width = ClientSize.Width - Padding.Horizontal;
            foreach (Control c in Controls)
            {
                int w = Math.Max(60, width - c.Margin.Horizontal);
                if (c is Label && c.AutoSize)
                {
                    if (c.MaximumSize.Width != w) c.MaximumSize = new Size(w, 0);
                }
                else if ("stretch".Equals(c.Tag) && c.Width != w)
                {
                    c.Width = w;
                }
            }
            base.OnLayout(levent);
        }
    }

    abstract class SetupPage : UserControl
    {
        public static readonly Color Good = Color.FromArgb(26, 127, 55);
        public static readonly Color Warn = Color.FromArgb(154, 103, 0);
        public static readonly Color Bad = Color.FromArgb(200, 30, 40);
        public static readonly Color Muted = Color.FromArgb(100, 100, 100);

        protected readonly SetupForm Setup;
        protected readonly Column Body = new Column();

        protected SetupPage(SetupForm setup)
        {
            Setup = setup;
            Dock = DockStyle.Fill;
            AutoScaleMode = AutoScaleMode.Inherit;
            BackColor = SystemColors.Window;
            Controls.Add(Body);
        }

        public abstract string Title { get; }
        public virtual bool CanGoNext { get { return true; } }
        public virtual bool Busy { get { return false; } }
        public virtual void Entered() { }
        public virtual void Leaving() { }

        protected void NextChanged() { Setup.UpdateButtons(); }

        protected Label Para(string text)
        {
            var label = new Label { AutoSize = true, Text = text, Margin = new Padding(0, 0, 0, 12) };
            Body.Controls.Add(label);
            return label;
        }

        protected Label Note(string text)
        {
            Label label = Para(text);
            label.ForeColor = Muted;
            return label;
        }

        protected FlowLayoutPanel Row()
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 12) };
            Body.Controls.Add(row);
            return row;
        }

        protected static Button MakeButton(string text)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(0, 30),
                Padding = new Padding(10, 0, 10, 0),
                Margin = new Padding(0, 0, 10, 0),
                UseVisualStyleBackColor = true
            };
        }

        // ok: null for plain text, true for a tick, false for a warning
        protected static void SetStatus(Label label, bool? ok, string text)
        {
            label.Text = (ok == null ? "" : ok.Value ? "✔  " : "⚠  ") + text;
            label.ForeColor = ok == null ? SystemColors.ControlText : ok.Value ? Good : Warn;
        }

        protected static string InputLabel(string id, string name)
        {
            string hdmi = "HDMI " + id.Substring("tvinput.hdmi".Length);
            return name.Length == 0 || name == hdmi ? hdmi : hdmi + "  ·  " + name;
        }
    }
}
