using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace TVFollow
{
    // Step 1: find the TVs on this PC's network and tick the ones it's plugged into. TVs set up
    // before come ticked, and stay listed even when they don't answer.
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
        readonly CheckBox redo;
        readonly Font bold;
        readonly HashSet<string> ticked = new HashSet<string>(); // by serial, so a search again keeps them
        bool scanning, searched;

        public FindPage(SetupForm setup) : base(setup)
        {
            Para("TVFollow works like the Roku phone app: it sends your TV's remote presses over your home network. " +
                "It works with TVs that have Roku built in. Turn the TV on and make sure it's on the same network as this PC. " +
                "Plugged into more than one? Tick each of them.");

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
            // the list isn't in the window yet, so it would measure with the old default font
            list.Font = setup.Font;
            bold = new Font(setup.Font, FontStyle.Bold);
            list.ItemHeight = Math.Max(list.Font.Height, bold.Height) * 2 + 16;
            list.DrawItem += DrawTv;
            // a click anywhere on a TV ticks or unticks it; Space does the same for the one with focus
            list.MouseUp += (s, e) =>
            {
                int i = list.IndexFromPoint(e.Location);
                if (e.Button == MouseButtons.Left && i >= 0 && list.GetItemRectangle(i).Contains(e.Location)) Toggle(i);
            };
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Space || list.SelectedIndex < 0) return;
                Toggle(list.SelectedIndex);
                e.Handled = e.SuppressKeyPress = true;
            };
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
            manualRow.Controls.Add(address);
            manualRow.Controls.Add(add);

            hint = Note("Not listed? Check that the TV is on and that this PC isn't on a guest network or a VPN. " +
                "The TV's address is under Settings → Network → About on the TV.");
            hint.Visible = false;

            redo = new CheckBox { Text = "Go through the next steps again for TVs set up already", AutoSize = true, Margin = new Padding(0, 0, 0, 8), Visible = false };
            redo.CheckedChanged += delegate { Setup.Redo = redo.Checked; };
            Body.Controls.Add(redo);
        }

        // the ticked TVs, in the list's order (each TV once)
        public List<FoundTv> Ticked
        {
            get
            {
                var tvs = new List<FoundTv>();
                foreach (FoundTv tv in list.Items)
                    if (ticked.Contains(tv.Info.Serial) && !tvs.Exists(t => t.Info.Serial == tv.Info.Serial)) tvs.Add(tv);
                return tvs;
            }
        }

        // the TV with this serial on the list, or null
        FoundTv Listed(string serial)
        {
            foreach (FoundTv tv in list.Items) if (tv.Info.Serial == serial) return tv;
            return null;
        }

        // for the tests and the screenshot tool: exactly these TVs ticked (added to the end of
        // the list if they aren't on it)
        public void TickOnly(FoundTv[] tvs)
        {
            ticked.Clear();
            foreach (FoundTv tv in tvs)
            {
                bool listed = false;
                foreach (FoundTv f in list.Items) if (f.Info.Serial == tv.Info.Serial) listed = true;
                if (!listed) list.Items.Add(tv);
                ticked.Add(tv.Info.Serial);
            }
            list.Invalidate();
            Picked();
        }

        // Enter in the address box adds that address. Without this the window's Next button
        // would take the Enter first and carry on with whichever TV was already picked.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter && address.Focused)
            {
                AddAddress();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public override string Title { get { return "Find your TV"; } }
        public override bool Busy { get { return scanning; } }
        public override bool CanGoNext { get { return !scanning && Ticked.Count > 0; } }

        public override void Entered()
        {
            if (list.Items.Count == 0 && !scanning) Search();
        }

        async void Search()
        {
            scanning = true;
            again.Enabled = false;
            list.Items.Clear();
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
            // an address typed in while the search ran is on the list already, and stays there:
            // the search may not reach it (another subnet is the usual reason to type one in)
            foreach (FoundTv tv in found)
                if (Listed(tv.Info.Serial) == null) list.Items.Add(tv);
            // a TV set up before that didn't answer stays on the list, ticked, so finishing setup
            // doesn't drop it unseen: it keeps its settings unless it's unticked
            foreach (TvSettings saved in Setup.Settings.Tvs)
            {
                if (Listed(saved.Serial) != null) continue;
                var info = new DeviceInfo { Serial = saved.Serial, Name = saved.Name, IsTv = true };
                list.Items.Add(new FoundTv { Ip = saved.Ip, Info = info, Answering = false });
            }
            int tvs = 0;
            foreach (FoundTv tv in list.Items) if (tv.Info.IsTv) tvs++;
            // the first time: the TVs set up before, or the only TV there is. After that, what was
            // ticked stays ticked, if it's still listed.
            if (!searched)
            {
                foreach (FoundTv tv in list.Items)
                    if (tv.Info.IsTv && (Setup.IsSetUp(tv.Info.Serial) || (tvs == 1 && Setup.Settings.Tvs.Count == 0))) ticked.Add(tv.Info.Serial);
                searched = true;
            }
            ticked.RemoveWhere(serial => { FoundTv tv = Listed(serial); return tv == null || !tv.Info.IsTv; });

            hint.Visible = tvs == 0;
            bool setUpListed = false;
            foreach (FoundTv tv in list.Items) if (Setup.IsSetUp(tv.Info.Serial)) setUpListed = true;
            redo.Visible = setUpListed;
            list.Invalidate();
            if (tvs == 0)
                SetStatus(status, false, list.Items.Count == 0 ? "No TVs found." : "Found a Roku, but no TV with Roku built in.");
            else if (ticked.Count > 0)
            {
                Picked();
                return;
            }
            else
                SetStatus(status, null, tvs == 1 ? "Found your TV. Tick it to use it." : "Found " + tvs + " TVs. Tick every TV this PC is plugged into.");
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
            if (tv.Info.IsTv) ticked.Add(tv.Info.Serial); // typed in: that's the TV wanted
            manualRow.Visible = false;
            list.Invalidate();
            Picked(tv);
        }

        void Toggle(int index)
        {
            var tv = (FoundTv)list.Items[index];
            if (tv.Info.IsTv && !ticked.Remove(tv.Info.Serial)) ticked.Add(tv.Info.Serial);
            list.Invalidate(list.GetItemRectangle(index));
            Picked(tv);
        }

        // what the ticks add up to, after this TV was clicked (null: no click, just a new list)
        void Picked(FoundTv clicked = null)
        {
            List<FoundTv> tvs = Ticked;
            if (clicked != null && !clicked.Info.IsTv)
                SetStatus(status, false, "That's a Roku stick or box. It can't turn the TV on or off, so tick a TV with Roku built in.");
            else if (tvs.Count == 1)
                SetStatus(status, true, tvs[0].Title + " it is.");
            else if (tvs.Count > 1)
                SetStatus(status, true, tvs.Count + " TVs: " + string.Join(", ", tvs.ConvertAll(t => t.Title)) + ".");
            else if (list.Items.Count > 0)
                SetStatus(status, null, "Tick the TV this PC is plugged into.");
            NextChanged();
        }

        void DrawTv(object sender, DrawItemEventArgs e)
        {
            // no highlight for the item with focus: the tick is what counts
            using (var back = new SolidBrush(list.BackColor)) e.Graphics.FillRectangle(back, e.Bounds);
            if (e.Index < 0) return;
            var tv = (FoundTv)list.Items[e.Index];
            CheckBoxState box = !tv.Info.IsTv ? CheckBoxState.UncheckedDisabled
                : ticked.Contains(tv.Info.Serial) ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
            Size glyph = CheckBoxRenderer.GetGlyphSize(e.Graphics, box);
            CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(e.Bounds.X + 10, e.Bounds.Y + (e.Bounds.Height - glyph.Height) / 2), box);

            Color main = tv.Info.IsTv ? SystemColors.ControlText : Muted;
            int line = list.Font.Height;
            int left = 10 + glyph.Width + 10;
            var top = new Rectangle(e.Bounds.X + left, e.Bounds.Y + 6, e.Bounds.Width - left - 10, line + 2);
            var bottom = new Rectangle(top.X, top.Bottom + 1, top.Width, line + 2);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(e.Graphics, tv.Title, bold, top, main, flags);
            string details;
            if (!tv.Info.IsTv) details = "Roku stick or box (can't turn a TV on or off)  ·  " + tv.Address;
            else if (!tv.Answering) details = "Set up already  ·  not answering right now  ·  " + tv.Address;
            else details = (Setup.IsSetUp(tv.Info.Serial) ? "Set up already  ·  " : "") + tv.Details;
            TextRenderer.DrawText(e.Graphics, details, list.Font, bottom, Muted, flags);
            if ((e.State & DrawItemState.Focus) != 0 && list.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds);
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
        int visit; // answers from an earlier visit (maybe about another TV) are ignored

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
            visit++;
            checking = false;
            allowed = false;
            steps.Visible = after.Visible = false;
            SetStatus(status, null, "Checking the TV's settings…");
            Check();
            timer.Start();
        }

        public override void Leaving() { timer.Stop(); visit++; }

        async void Check()
        {
            if (checking) return;
            checking = true;
            int thisVisit = visit;
            RokuClient client = Setup.Client;
            DeviceInfo info = await Task.Run(() => client.GetDeviceInfo(3000));
            if (thisVisit != visit) return; // the page was left (and maybe another TV picked) meanwhile
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

            bool guessed = inputs == null || inputs.Count == 0;
            if (guessed)
            {
                inputs = new List<KeyValuePair<string, string>>();
                for (int i = 1; i <= 4; i++) inputs.Add(new KeyValuePair<string, string>("tvinput.hdmi" + i, ""));
            }

            // pick: the input this TV has (saved, or picked earlier), else the one it's showing, else the first
            string pick = Setup.Current.Tv.Input.Length > 0 ? "tvinput." + Setup.Current.Tv.Input : null;
            if (pick == null || !inputs.Exists(p => p.Key == pick)) pick = active != null && inputs.Exists(p => p.Key == active) ? active : inputs[0].Key;

            note.Text = "";
            foreach (KeyValuePair<string, string> input in inputs)
            {
                var radio = new RadioButton { Text = InputLabel(input.Key, input.Value), Tag = input, AutoSize = true, Margin = new Padding(0, 2, 0, 4), UseMnemonic = false };
                radio.CheckedChanged += delegate { if (radio.Checked) Chose(radio); };
                choices.Controls.Add(radio);
                if (input.Key == pick) radio.Checked = true;
                if (input.Key == active) note.Text = "Your TV is showing " + InputLabel(input.Key, input.Value) + " right now.";
            }
            if (guessed)
            {
                // ask again next time this page opens
                note.Text = "The TV didn't list its inputs, so these are the usual four. Use Show it on the TV to check.";
                loadedFor = null;
            }
            else loadedFor = Setup.Tv.Info.Serial;
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
            Setup.Current.Tv.Input = input.Key.Substring("tvinput.".Length);
            Setup.Current.InputName = InputLabel(input.Key, input.Value);
            shown.Text = "";
            NextChanged();
        }

        async void ShowOnTv()
        {
            RadioButton radio = Selected();
            if (radio == null) return;
            show.Enabled = false;
            RokuClient client = Setup.Client;
            string input = Setup.Current.Tv.Input;
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

    // Step 4: turn the TV off and back on, the way TVFollow will. Each TV is tested (or skipped)
    // on its own.
    class TestPage : SetupPage
    {
        readonly Button run;
        readonly FlowLayoutPanel results;
        readonly LinkLabel skip;
        bool running;
        string shownFor;  // the TV and input the results on the page are about

        public TestPage(SetupForm setup) : base(setup)
        {
            Para("TVFollow will turn the TV off, check it can still reach it, then turn it back on showing this PC. " +
                "It takes about 15 seconds.");
            FlowLayoutPanel row = Row();
            run = MakeButton("Run the test");
            run.Click += delegate { StartTest(); };
            skip = new LinkLabel { Text = "Skip the test", AutoSize = true, Margin = new Padding(4, 8, 0, 0) };
            skip.LinkClicked += delegate { Setup.Current.Tested = Key; NextChanged(); Setup.GoNext(); };
            row.Controls.Add(run);
            row.Controls.Add(skip);
            results = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Tag = "stretch", Margin = new Padding(0) };
            Body.Controls.Add(results);
        }

        public override string Title { get { return "Test it"; } }
        public override bool Busy { get { return running; } }
        public override bool CanGoNext { get { return Setup.Current != null && Setup.Current.Tested == Key; } }

        string Key { get { return Setup.Tv.Info.Serial + "|" + Setup.Current.Tv.Input; } }

        public override void Entered()
        {
            if (shownFor != Key)
            {
                results.Controls.Clear();
                shownFor = null;
            }
            run.Text = Setup.Current.Tested == Key && shownFor != null ? "Run it again" : "Run the test";
        }

        public async void StartTest()
        {
            if (running) return;
            running = true;
            SetupTv current = Setup.Current;
            current.Tested = null;
            results.Controls.Clear();
            shownFor = Key;
            run.Enabled = skip.Enabled = false;
            NextChanged();

            var progress = new Progress<Tuple<bool?, string>>(line => AddLine(line.Item1, line.Item2));
            IProgress<Tuple<bool?, string>> report = progress;
            RokuClient tv = Setup.Client;
            string input = current.Tv.Input;
            string mac = Setup.Tv.Info.WakeMac;
            string key = Key;
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
            if (ok) current.Tested = key;
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

    // Step 5: what TVFollow will do, how it runs (installed or portable), and Finish.
    class FinishPage : SetupPage
    {
        readonly Label summary, portableNote, note;
        readonly CheckBox off, on, updates, startup, shortcuts;
        readonly RadioButton install, portable;
        string blocker;

        public FinishPage(SetupForm setup) : base(setup)
        {
            summary = Para("");
            off = Option("Turn the TV off when this PC's monitors turn off", 4, 0);
            on = Option("Turn the TV on and switch it to this PC when you come back", 4, 0);
            updates = Option("Tell me when there's a new version (checks GitHub once a week)", 14, 0);

            Label how = Para("How should TVFollow run?");
            how.Font = new Font(setup.Font, FontStyle.Bold);
            how.Margin = new Padding(0, 0, 0, 4);
            install = Choice("Install it for this Windows account");
            startup = Option("Start TVFollow when you sign in to Windows", 2, 20);
            shortcuts = Option("Put TV on, TV off and TV to PC shortcuts on the desktop", 6, 20);
            portable = Choice("Portable: run from this folder and change nothing else on this PC");
            portableNote = Note("");
            portableNote.Margin = new Padding(20, 0, 0, 12);

            off.CheckedChanged += delegate { Setup.Settings.OffWithMonitors = off.Checked; };
            on.CheckedChanged += delegate { Setup.Settings.OnWithMonitors = on.Checked; };
            updates.CheckedChanged += delegate { Setup.Settings.CheckUpdates = updates.Checked; };
            startup.CheckedChanged += delegate { Setup.Settings.StartWithWindows = startup.Checked; };
            shortcuts.CheckedChanged += delegate { Setup.Settings.DesktopShortcuts = shortcuts.Checked; };
            install.CheckedChanged += delegate { ShowMode(); };
            portable.CheckedChanged += delegate { ShowMode(); };

            note = Note("");
        }

        CheckBox Option(string text, int gap, int indent)
        {
            var box = new CheckBox { Text = text, AutoSize = true, Margin = new Padding(indent, 0, 0, gap) };
            Body.Controls.Add(box);
            return box;
        }

        RadioButton Choice(string text)
        {
            var radio = new RadioButton { Text = text, AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
            Body.Controls.Add(radio);
            return radio;
        }

        public override string Title { get { return "All set"; } }

        public override void Entered()
        {
            List<SetupTv> tvs = Setup.Chosen;
            bool several = tvs.Count > 1;
            if (!several)
                summary.Text = "TV: " + Where(tvs[0]) + "\r\nInput: " + tvs[0].InputName;
            else
                summary.Text = "TVs:\r\n" + string.Join("\r\n", tvs.ConvertAll(t => "    " + Where(t) + ":  " + t.InputName));
            off.Text = several ? "Turn the TVs off when this PC's monitors turn off" : "Turn the TV off when this PC's monitors turn off";
            on.Text = several ? "Turn the TVs on and switch them to this PC when you come back" : "Turn the TV on and switch it to this PC when you come back";
            note.Text = (several
                ? "TVFollow only turns a TV off while it's showing this PC, so it won't interrupt anyone watching something else. "
                : "TVFollow only turns the TV off while it's showing this PC, so it won't interrupt anyone watching something else. ") +
                "It runs from the TV icon in the taskbar corner: right-click it for the remote, TV buttons, Pause and Settings.";
            off.Checked = Setup.Settings.OffWithMonitors;
            on.Checked = Setup.Settings.OnWithMonitors;
            updates.Checked = Setup.Settings.CheckUpdates;
            startup.Checked = Setup.Settings.StartWithWindows;
            shortcuts.Checked = Setup.Settings.DesktopShortcuts;

            blocker = Settings.PortableBlocker(Settings.ExeDir, InstallPaths.Installed().Dir, System.IO.Path.GetTempPath());
            portable.Enabled = blocker == null;
            if (portable.Enabled && Settings.IsPortable) portable.Checked = true; // already portable: stay that way
            else install.Checked = true;
            ShowMode();
        }

        // "Living Room TV (Living room)"
        static string Where(SetupTv tv)
        {
            string location = tv.Found.Info.Location;
            return tv.Found.Title + (location.Length > 0 ? " (" + location + ")" : "");
        }

        void ShowMode()
        {
            Setup.Portable = portable.Checked;
            startup.Enabled = shortcuts.Enabled = !portable.Checked;
            portableNote.Text = blocker ?? "Settings are kept in a \"" + Settings.PortableFolderName + "\" folder next to TVFollow.exe, " +
                "and it won't start by itself when you sign in. To remove it, quit it from the tray and delete the folder.";
        }
    }
}
