using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TVFollow
{
    // One ticked TV in this run of setup: what was found, and what's saved for it on Finish.
    class SetupTv
    {
        public FoundTv Found;
        public TvSettings Tv;           // for a TV set up before, a copy of its settings
        public bool SetUpBefore;        // it keeps its settings unless it goes through steps 2 to 4 again
        public string InputName = "";   // what the TV calls the picked input, for the last page
        public string Tested;           // serial|input the test passed or was skipped for
    }

    // The setup window: find the TVs, then for each new one let this PC control it, pick the
    // input and test; finish. Opens on first run, for a new download, and from Settings.
    class SetupForm : Form
    {
        public const string HelpUrl = "https://github.com/still-b0r3d/TVFollow";
        public const string MakerUrl = "https://b0r3d.org";
        public const string Maker = "b0r3d.org";

        public readonly Settings Settings;  // filled in step by step, saved on Finish
        public bool Redo;                    // chosen on the first page: TVs set up before go through the steps again too
        public bool Portable;                // chosen on the last page: run from this folder, install nothing
        public Func<List<FoundTv>> Scan;     // how the first page looks for TVs (tests swap it)
        public PowerTest Test = new PowerTest();

        readonly List<SetupPage> pages = new List<SetupPage>();
        readonly FindPage find;
        int current = -1;
        readonly Dictionary<string, SetupTv> known = new Dictionary<string, SetupTv>(); // by serial: choices survive going back
        List<SetupTv> chosen = new List<SetupTv>();  // the ticked TVs, in the order they're saved
        List<SetupTv> walk = new List<SetupTv>();    // the ones steps 2 to 4 go through
        int step;                                     // which of those the steps are on
        readonly Label title = new Label { UseMnemonic = false };
        readonly Label stepText = new Label { UseMnemonic = false };
        readonly Button back = new Button();
        readonly Button next = new Button();
        readonly Button cancel = new Button();

        public SetupForm(Settings settings)
        {
            Settings = settings;
            Scan = () => TvScanner.FindAll(Settings.Port, Settings.Tvs.ConvertAll(t => t.Ip));

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
            find = new FindPage(this);
            pages.Add(find);
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

        // the ticked TVs, in the order they're saved
        public List<SetupTv> Chosen { get { return chosen; } }

        // the TV steps 2 to 4 are on
        public SetupTv Current { get { return step < walk.Count ? walk[step] : null; } }

        public FoundTv Tv { get { return Current == null ? null : Current.Found; } }

        public RokuClient Client { get { return new RokuClient(Tv.Ip, Settings.Port); } }

        // is this TV in the settings already?
        public bool IsSetUp(string serial) { return Settings.Tvs.Exists(t => t.Serial == serial); }

        // for the tests: tick exactly these TVs on the first page, as clicking them would
        public void Pick(params FoundTv[] tvs) { find.TickOnly(tvs); }

        // "hdmi2" as the windows show it
        public static string InputText(string input)
        {
            return input.StartsWith("hdmi") ? "HDMI " + input.Substring("hdmi".Length) : input;
        }

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
            stepText.Text = "Step " + (current + 1) + " of " + pages.Count + WhichTv();
            page.Visible = true;
            page.Entered();
            UpdateButtons();
            // Hiding the old page would hand focus to the next control in tab order, which could
            // be Cancel, and Enter would then throw the setup away. Focus the new page instead.
            if (!page.SelectNextControl(null, true, true, true, false)) FocusNext();
        }

        // which TV steps 2 to 4 are on, once more than one TV is in play
        string WhichTv()
        {
            if (current < 1 || current > 3 || chosen.Count < 2 || Current == null) return "";
            return "  ·  " + (walk.Count > 1 ? "TV " + (step + 1) + " of " + walk.Count + ": " : "") + Current.Found.Title;
        }

        // Steps 2 to 4 go round once for each TV that goes through them; with none (only TVs set
        // up before, say after a new download), Next on step 1 goes straight to the last page.
        public void GoNext()
        {
            SetupPage page = CurrentPage;
            if (page == null || !page.CanGoNext || page.Busy) return;
            if (page is FindPage)
            {
                Plan();
                ShowPage(walk.Count > 0 ? 1 : pages.Count - 1);
            }
            else if (page is TestPage && step < walk.Count - 1)
            {
                step++;
                ShowPage(1);
            }
            else if (page is TestPage) ShowPage(pages.Count - 1);
            else if (current < pages.Count - 1) ShowPage(current + 1);
            else Finish();
        }

        public void GoBack()
        {
            SetupPage page = CurrentPage;
            if (page == null || current == 0 || page.Busy) return;
            if (page is ControlPage && step > 0)
            {
                step--;
                ShowPage(3); // the previous TV's test
            }
            else if (page is FinishPage) ShowPage(walk.Count > 0 ? 3 : 0);
            else ShowPage(current - 1);
        }

        // Leaving step 1: the ticked TVs in the order they're saved (the ones set up before first,
        // in their old order, then the new ones), and the ones steps 2 to 4 go through: the new
        // ones, and the others too if asked. Never a TV set up before that didn't answer: its old
        // address may belong to another Roku by now, and the test would turn that one off.
        void Plan()
        {
            List<FoundTv> ticked = find.Ticked;
            chosen = new List<SetupTv>();
            foreach (TvSettings saved in Settings.Tvs)
            {
                FoundTv found = ticked.Find(f => f.Info.Serial == saved.Serial);
                if (found != null) chosen.Add(Remember(found, saved));
            }
            foreach (FoundTv found in ticked)
                if (!IsSetUp(found.Info.Serial)) chosen.Add(Remember(found, null));
            walk = chosen.FindAll(t => t.Found.Answering && (!t.SetUpBefore || Redo));
            step = 0;
        }

        SetupTv Remember(FoundTv found, TvSettings saved)
        {
            SetupTv t;
            if (!known.TryGetValue(found.Info.Serial, out t))
            {
                t = new SetupTv { SetUpBefore = saved != null };
                // a new TV has no input yet; the input page picks one
                t.Tv = saved != null ? saved.Copy() : new TvSettings { Input = "" };
                t.InputName = saved != null ? InputText(saved.Input) : "";
                known[found.Info.Serial] = t;
            }
            t.Found = found;
            return t;
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
            var before = new List<TvSettings>(Settings.Tvs);
            Settings.Tvs.Clear();
            foreach (SetupTv t in chosen)
            {
                TvSettings tv = t.Tv;
                if (t.Found.Answering)
                {
                    tv.Ip = t.Found.Ip;
                    tv.Serial = t.Found.Info.Serial;
                    tv.WakeMac = t.Found.Info.WakeMac;
                    tv.Name = t.Found.Info.Name;
                    tv.GatewayMac = ""; // this may be a new home: the router is learned again once the TV answers
                }
                // a TV set up before that didn't answer keeps what it had
                Settings.Tvs.Add(tv);
            }
            // the settings go where the chosen mode keeps them (a trial copy keeps its own folder)
            string dirBefore = TVFollow.Settings.Dir;
            if (!TVFollow.Settings.IsTrial) TVFollow.Settings.Dir = Portable ? TVFollow.Settings.PortableDir : TVFollow.Settings.DefaultDir;
            try { Settings.Save(); }
            catch (Exception e)
            {
                // as it was, so going back to step 1 still knows which TVs were set up before
                // (the kept TVs' changes were made on copies)
                Settings.Tvs.Clear();
                Settings.Tvs.AddRange(before);
                TVFollow.Settings.Dir = dirBefore;
                MessageBox.Show(this, "Couldn't save the settings: " + e.Message, "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Log.Write(Settings.SeveralTvs
                ? "setup saved: " + Settings.Tvs.Count + " TVs, inputs " + string.Join(", ", Settings.Tvs.ConvertAll(t => t.Input))
                : "setup saved: input " + Settings.Tvs[0].Input);
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
            // UseMnemonic off: TVs' and inputs' own names show up in these, and an & stays an &
            var label = new Label { AutoSize = true, Text = text, Margin = new Padding(0, 0, 0, 12), UseMnemonic = false };
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
