using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TVFollow
{
    // Step 1: find the TVs on this PC's network and pick one.
    class FindPage : SetupPage
    {
        readonly Label status;
        readonly ProgressBar spinner;
        readonly ListBox list;
        readonly Button again;
        readonly FlowLayoutPanel manualRow;
        readonly TextBox address;
        readonly Button add;
        readonly Label hint;
        readonly Font bold;
        bool scanning;

        public FindPage(SetupForm setup) : base(setup)
        {
            Para("TVFollow works with TVs that have Roku built in. Turn the TV on and make sure it's on the same network as this PC.");

            status = Para("");
            status.Margin = new Padding(0, 0, 0, 6);
            spinner = new ProgressBar { Style = ProgressBarStyle.Marquee, Size = new Size(220, 8), Margin = new Padding(0, 0, 0, 10), Visible = false };
            Body.Controls.Add(spinner);

            list = new ListBox
            {
                DrawMode = DrawMode.OwnerDrawFixed,
                IntegralHeight = false,
                Height = 178,
                Tag = "stretch",
                Margin = new Padding(0, 0, 0, 10),
                BorderStyle = BorderStyle.FixedSingle
            };
            bold = new Font(list.Font, FontStyle.Bold);
            list.ItemHeight = list.Font.Height * 2 + 14;
            list.DrawItem += DrawTv;
            list.SelectedIndexChanged += delegate { Picked(); };
            list.DoubleClick += delegate { Setup.GoNext(); };
            Body.Controls.Add(list);

            FlowLayoutPanel buttons = Row();
            again = MakeButton("Search again");
            again.Click += delegate { Search(); };
            var manual = new LinkLabel { Text = "Enter the TV's address instead", AutoSize = true, Margin = new Padding(4, 8, 0, 0) };
            manual.LinkClicked += delegate { manualRow.Visible = true; address.Focus(); };
            buttons.Controls.Add(again);
            buttons.Controls.Add(manual);

            manualRow = Row();
            manualRow.Visible = false;
            address = new TextBox { Width = 160, Margin = new Padding(0, 3, 8, 0) };
            add = MakeButton("Add");
            add.Click += delegate { AddAddress(); };
            address.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AddAddress(); } };
            manualRow.Controls.Add(address);
            manualRow.Controls.Add(add);

            hint = Note("Not listed? Check that the TV is on and that this PC isn't on a guest network or a VPN. " +
                "The TV's address is under Settings → Network → About on the TV.");
            hint.Visible = false;
        }

        public override string Title { get { return "Find your TV"; } }
        public override bool Busy { get { return scanning; } }
        public override bool CanGoNext { get { return Setup.Tv != null && Setup.Tv.Info.IsTv; } }

        public override void Entered()
        {
            if (list.Items.Count == 0 && !scanning) Search();
        }

        async void Search()
        {
            scanning = true;
            again.Enabled = false;
            list.Items.Clear();
            Setup.Tv = null;
            SetStatus(status, null, "Looking for TVs on your network…");
            spinner.Visible = true;
            hint.Visible = false;
            NextChanged();

            List<FoundTv> found;
            try { found = await Task.Run(Setup.Scan); }
            catch { found = new List<FoundTv>(); }
            if (IsDisposed) return;

            scanning = false;
            again.Enabled = true;
            spinner.Visible = false;
            int tvs = 0;
            foreach (FoundTv tv in found)
            {
                list.Items.Add(tv);
                if (tv.Info.IsTv) tvs++;
            }
            if (tvs == 0)
                SetStatus(status, false, found.Count == 0 ? "No TVs found." : "Found a Roku, but no TV with Roku built in.");
            else
                SetStatus(status, null, tvs == 1 ? "Found your TV." : "Found " + tvs + " TVs. Pick the one this PC is plugged into.");
            hint.Visible = tvs == 0;

            // pick the TV from the saved settings, or the only TV there is
            foreach (FoundTv tv in found)
            {
                if (tv.Info.IsTv && (tv.Info.Serial == Setup.Settings.Serial || tvs == 1)) { list.SelectedItem = tv; break; }
            }
            NextChanged();
        }

        async void AddAddress()
        {
            string ip = address.Text.Trim();
            IPAddress parsed;
            if (!IPAddress.TryParse(ip, out parsed) || ip.Split('.').Length != 4)
            {
                SetStatus(status, false, "That doesn't look like an address. It looks like 192.168.1.50.");
                return;
            }
            add.Enabled = false;
            SetStatus(status, null, "Checking " + ip + "…");
            List<FoundTv> found = await Task.Run(() => TvScanner.Probe(new[] { ip }, Setup.Settings.Port));
            if (IsDisposed) return;
            add.Enabled = true;
            if (found.Count == 0)
            {
                SetStatus(status, false, "Nothing answered at " + ip + ".");
                return;
            }
            FoundTv tv = found[0];
            for (int i = list.Items.Count - 1; i >= 0; i--)
                if (((FoundTv)list.Items[i]).Info.Serial == tv.Info.Serial) list.Items.RemoveAt(i);
            list.Items.Add(tv);
            list.SelectedItem = tv;
            manualRow.Visible = false;
        }

        void Picked()
        {
            var tv = list.SelectedItem as FoundTv;
            Setup.Tv = tv;
            if (tv != null && !tv.Info.IsTv)
                SetStatus(status, false, "That's a Roku stick or box. It can't turn the TV on or off, so pick a TV with Roku built in.");
            else if (tv != null)
                SetStatus(status, true, tv.Title + " it is.");
            NextChanged();
        }

        void DrawTv(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0) return;
            var tv = (FoundTv)list.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color main = selected ? SystemColors.HighlightText : tv.Info.IsTv ? SystemColors.ControlText : Muted;
            Color sub = selected ? SystemColors.HighlightText : Muted;
            int line = list.Font.Height;
            var top = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 6, e.Bounds.Width - 20, line + 2);
            var bottom = new Rectangle(top.X, top.Bottom + 1, top.Width, line + 2);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(e.Graphics, tv.Title, bold, top, main, flags);
            string details = tv.Info.IsTv ? tv.Details : "Roku stick or box (can't turn a TV on or off)  ·  " + tv.Address;
            TextRenderer.DrawText(e.Graphics, details, list.Font, bottom, sub, flags);
            e.DrawFocusRectangle();
        }
    }

    // Step 2: the TV's "Control by mobile apps" setting must allow the power and input buttons.
    class ControlPage : SetupPage
    {
        readonly Label status;
        readonly Control steps;
        readonly Label after;
        readonly Timer timer = new Timer { Interval = 2000 };
        bool allowed, checking;

        public ControlPage(SetupForm setup) : base(setup)
        {
            status = Para("");
            steps = Steps(
                "Press Home (the house button).",
                "Scroll down to Settings and press OK.",
                "Choose System.",
                "Choose Advanced system settings.",
                "Choose Control by mobile apps.",
                "Choose Network access.",
                "Choose Enabled.");
            after = Note("This page notices by itself when it's done. It can take a few seconds.");
            steps.Visible = after.Visible = false;
            timer.Tick += delegate { Check(); };
        }

        public override string Title { get { return "Let this PC control the TV"; } }
        public override bool CanGoNext { get { return allowed; } }

        public override void Entered()
        {
            allowed = false;
            SetStatus(status, null, "Checking the TV's settings…");
            Check();
            timer.Start();
        }

        public override void Leaving() { timer.Stop(); }

        async void Check()
        {
            if (checking) return;
            checking = true;
            RokuClient client = Setup.Client;
            DeviceInfo info = await Task.Run(() => client.GetDeviceInfo(3000));
            checking = false;
            if (IsDisposed || !Visible) return;

            allowed = info != null && info.AllowsControl;
            if (info == null)
                SetStatus(status, false, "Can't reach the TV right now. Is it still on?");
            else if (allowed)
                SetStatus(status, true, "Your TV lets this PC control it.");
            else
                SetStatus(status, false, "Your TV's Network access setting is on Limited, which blocks the power button. " +
                    "Change it to Enabled with the TV's remote:");
            steps.Visible = after.Visible = info != null && !allowed;
            if (allowed) timer.Stop();
            NextChanged();
        }

        Control Steps(params string[] lines)
        {
            var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(12, 0, 0, 10) };
            for (int i = 0; i < lines.Length; i++)
            {
                table.Controls.Add(new Label { Text = (i + 1) + ".", AutoSize = true, ForeColor = Muted, Margin = new Padding(0, 2, 6, 2) }, 0, i);
                table.Controls.Add(new Label { Text = lines[i], AutoSize = true, Margin = new Padding(0, 2, 0, 2) }, 1, i);
            }
            Body.Controls.Add(table);
            return table;
        }
    }

    // Step 3: which TV input the PC is plugged into.
    class InputPage : SetupPage
    {
        readonly Label note;
        readonly FlowLayoutPanel choices;
        readonly Button show;
        readonly Label shown;
        bool loading;
        string loadedFor;

        public InputPage(SetupForm setup) : base(setup)
        {
            Para("Pick the TV input this PC is plugged into.");
            note = Note("");
            choices = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(12, 0, 0, 12) };
            Body.Controls.Add(choices);
            FlowLayoutPanel row = Row();
            show = MakeButton("Show it on the TV");
            show.Click += delegate { ShowOnTv(); };
            row.Controls.Add(show);
            shown = Note("");
        }

        public override string Title { get { return "Which input is this PC on?"; } }
        public override bool Busy { get { return loading; } }
        public override bool CanGoNext { get { return Selected() != null; } }

        public override void Entered()
        {
            if (loadedFor != Setup.Tv.Info.Serial) LoadInputs();
        }

        async void LoadInputs()
        {
            loading = true;
            choices.Controls.Clear();
            note.Text = "Asking the TV…";
            shown.Text = "";
            NextChanged();

            RokuClient client = Setup.Client;
            List<KeyValuePair<string, string>> inputs = null;
            string active = null;
            await Task.Run(() =>
            {
                inputs = client.GetHdmiInputs(3000);
                active = client.GetActiveApp(3000);
            });
            if (IsDisposed) return;

            if (inputs == null || inputs.Count == 0)
            {
                inputs = new List<KeyValuePair<string, string>>();
                for (int i = 1; i <= 4; i++) inputs.Add(new KeyValuePair<string, string>("tvinput.hdmi" + i, ""));
            }

            // pick: the saved input for this TV, else the one the TV is showing, else the first
            string pick = Setup.Tv.Info.Serial == Setup.Settings.Serial ? "tvinput." + Setup.Settings.Input : null;
            if (pick == null || !inputs.Exists(p => p.Key == pick)) pick = active != null && inputs.Exists(p => p.Key == active) ? active : inputs[0].Key;

            note.Text = "";
            foreach (KeyValuePair<string, string> input in inputs)
            {
                var radio = new RadioButton { Text = InputLabel(input.Key, input.Value), Tag = input, AutoSize = true, Margin = new Padding(0, 2, 0, 4) };
                radio.CheckedChanged += delegate { if (radio.Checked) Chose(radio); };
                choices.Controls.Add(radio);
                if (input.Key == pick) radio.Checked = true;
                if (input.Key == active) note.Text = "Your TV is showing " + InputLabel(input.Key, input.Value) + " right now.";
            }
            loadedFor = Setup.Tv.Info.Serial;
            loading = false;
            NextChanged();
        }

        RadioButton Selected()
        {
            foreach (Control c in choices.Controls)
            {
                var radio = c as RadioButton;
                if (radio != null && radio.Checked) return radio;
            }
            return null;
        }

        void Chose(RadioButton radio)
        {
            var input = (KeyValuePair<string, string>)radio.Tag;
            Setup.Settings.Input = input.Key.Substring("tvinput.".Length);
            Setup.InputName = InputLabel(input.Key, input.Value);
            shown.Text = "";
            NextChanged();
        }

        async void ShowOnTv()
        {
            RadioButton radio = Selected();
            if (radio == null) return;
            show.Enabled = false;
            RokuClient client = Setup.Client;
            string input = Setup.Settings.Input;
            int code = await Task.Run(() =>
            {
                int c = client.Keypress("Input" + input.ToUpperInvariant(), 4000);
                if (c < 200 || c > 299) c = client.Launch("tvinput." + input, 4000);
                return c;
            });
            if (IsDisposed) return;
            show.Enabled = true;
            if (code >= 200 && code <= 299) SetStatus(shown, null, "Sent. This PC's screen should be on the TV now.");
            else if (code == 403) SetStatus(shown, false, "The TV said no. Check its Network access setting (the previous step).");
            else SetStatus(shown, false, "The TV didn't answer.");
        }
    }

    // Step 4: turn the TV off and back on, the way TVFollow will.
    class TestPage : SetupPage
    {
        readonly Button run;
        readonly FlowLayoutPanel results;
        readonly LinkLabel skip;
        bool running, passed, skipped;
        string testedFor;

        public TestPage(SetupForm setup) : base(setup)
        {
            Para("TVFollow will turn the TV off, check it can still reach it, then turn it back on showing this PC. " +
                "It takes about 15 seconds.");
            FlowLayoutPanel row = Row();
            run = MakeButton("Run the test");
            run.Click += delegate { StartTest(); };
            skip = new LinkLabel { Text = "Skip the test", AutoSize = true, Margin = new Padding(4, 8, 0, 0) };
            skip.LinkClicked += delegate { skipped = true; NextChanged(); Setup.GoNext(); };
            row.Controls.Add(run);
            row.Controls.Add(skip);
            results = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Tag = "stretch", Margin = new Padding(0) };
            Body.Controls.Add(results);
        }

        public override string Title { get { return "Test it"; } }
        public override bool Busy { get { return running; } }
        public override bool CanGoNext { get { return passed || skipped; } }

        string Key { get { return Setup.Tv.Info.Serial + "|" + Setup.Settings.Input; } }

        public override void Entered()
        {
            if (testedFor != Key)
            {
                passed = skipped = false;
                results.Controls.Clear();
                run.Text = "Run the test";
            }
        }

        public async void StartTest()
        {
            if (running) return;
            running = true;
            passed = false;
            results.Controls.Clear();
            run.Enabled = skip.Enabled = false;
            NextChanged();

            var progress = new Progress<Tuple<bool?, string>>(line => AddLine(line.Item1, line.Item2));
            IProgress<Tuple<bool?, string>> report = progress;
            RokuClient tv = Setup.Client;
            string input = Setup.Settings.Input;
            string mac = Setup.Tv.Info.WakeMac;
            PowerTest test = Setup.Test;
            Log.Write("setup test started (input " + input + ")");
            bool ok = await Task.Run(() => test.Run(tv, input, mac, (o, t) =>
            {
                if (o != null) Log.Write("setup test: " + (o.Value ? "ok: " : "problem: ") + t);
                report.Report(Tuple.Create(o, t));
            }));
            Log.Write("setup test " + (ok ? "passed" : "failed"));
            if (IsDisposed) return;

            running = false;
            passed = ok;
            testedFor = Key;
            run.Enabled = skip.Enabled = true;
            run.Text = ok ? "Run it again" : "Test again";
            NextChanged();
        }

        void AddLine(bool? ok, string text)
        {
            // a "working on it" line is replaced by whatever comes next
            if (results.Controls.Count > 0 && "working".Equals(results.Controls[results.Controls.Count - 1].Tag))
                results.Controls.RemoveAt(results.Controls.Count - 1);
            int width = Math.Max(100, Body.ClientSize.Width - Body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            var label = new Label { AutoSize = true, Margin = new Padding(0, 2, 0, 6), MaximumSize = new Size(width, 0) };
            if (ok == null)
            {
                label.Text = "…  " + text;
                label.ForeColor = Muted;
                label.Tag = "working";
            }
            else
            {
                label.Text = (ok.Value ? "✔  " : "✖  ") + text;
                label.ForeColor = ok.Value ? Good : Bad;
            }
            results.Controls.Add(label);
        }
    }

    // Step 5: what TVFollow will do, and Finish.
    class FinishPage : SetupPage
    {
        readonly Label summary;
        readonly CheckBox off, on, startup, shortcuts;

        public FinishPage(SetupForm setup) : base(setup)
        {
            summary = Para("");
            off = Option("Turn the TV off when this PC's monitors turn off", 6);
            on = Option("Turn the TV on and switch it to this PC when you come back", 6);
            startup = Option("Start TVFollow when you sign in to Windows", 6);
            shortcuts = Option("Put TV on, TV off and TV to PC shortcuts on the desktop", 14);
            off.CheckedChanged += delegate { Setup.Settings.OffWithMonitors = off.Checked; };
            on.CheckedChanged += delegate { Setup.Settings.OnWithMonitors = on.Checked; };
            startup.CheckedChanged += delegate { Setup.Settings.StartWithWindows = startup.Checked; };
            shortcuts.CheckedChanged += delegate { Setup.Settings.DesktopShortcuts = shortcuts.Checked; };
            Note("TVFollow only turns the TV off while it's showing this PC, so it won't interrupt anyone watching something else. " +
                "It runs from the TV icon in the taskbar corner: right-click it for TV buttons, Pause and these settings.");
        }

        CheckBox Option(string text, int gap)
        {
            var box = new CheckBox { Text = text, AutoSize = true, Margin = new Padding(0, 0, 0, gap) };
            Body.Controls.Add(box);
            return box;
        }

        public override string Title { get { return "All set"; } }

        public override void Entered()
        {
            string where = Setup.Tv.Info.Location.Length > 0 ? " (" + Setup.Tv.Info.Location + ")" : "";
            summary.Text = "TV: " + Setup.Tv.Title + where + "\r\nInput: " + Setup.InputName;
            off.Checked = Setup.Settings.OffWithMonitors;
            on.Checked = Setup.Settings.OnWithMonitors;
            startup.Checked = Setup.Settings.StartWithWindows;
            shortcuts.Checked = Setup.Settings.DesktopShortcuts;
        }
    }
}
