using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TVFollow
{
    // The TV remote, from the tray's Remote… item. Three layouts (Settings → Remote):
    //   classic  tall, like the real remote (the default)
    //   compact  square, volume beside the arrow pad, inputs in a drop-down
    //   strip    one wide row
    // While it has focus the keyboard works too: arrows, Enter (OK), Backspace (Back), Space
    // (play/pause), Home, + and -. The box at the bottom types into the TV's search.
    class RemoteForm : Form
    {
        public const string Classic = "classic", Compact = "compact", Strip = "strip";

        readonly Settings settings;
        readonly Controller controller;
        readonly RemoteSender sender;
        readonly ToolTip tips = new ToolTip();
        readonly Panel board = new Panel();
        readonly Timer repeat = new Timer();
        readonly string layout;
        FlowLayoutPanel chips;          // inputs as buttons (classic, strip)
        PadButton inputMenu;            // inputs as a drop-down (compact)
        TextBox typing;
        Label status;
        PadButton pin;
        string repeatKey;
        List<KeyValuePair<string, string>> inputs = new List<KeyValuePair<string, string>>();
        string showing = "";

        public RemoteForm(Settings settings, Controller controller, string layout)
        {
            this.settings = settings;
            this.controller = controller;
            this.layout = layout == Compact || layout == Strip ? layout : Classic;
            sender = new RemoteSender(() => new RokuClient(this.settings.Ip, this.settings.Port));
            sender.Answered += code => { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(() => ShowAnswer(code))); };

            SuspendLayout();
            Text = "TV remote";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            try { Icon = AppIcons.Load("tv.ico", SystemInformation.SmallIconSize); } catch { }

            board.BackColor = Theme.Window;
            board.Click += delegate { ActiveControl = null; }; // clicking the remote leaves the typing box
            if (this.layout == Compact) BuildCompact();
            else if (this.layout == Strip) BuildStrip();
            else BuildClassic();

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();

            repeat.Tick += delegate { repeat.Interval = 120; if (repeatKey != null) sender.Press(repeatKey); };
            Load += delegate { PlaceNearTray(); LoadInputs(); };
            FormClosed += delegate { repeat.Stop(); sender.Dispose(); tips.Dispose(); };
        }

        public string LayoutName { get { return layout; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.TitleBar(this);
        }

        // above the taskbar corner of the screen the pointer is on (where the tray icon was clicked)
        Point placedAt;
        Rectangle placedArea;

        void PlaceNearTray()
        {
            placedArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = placedAt = new Point(placedArea.Right - Width - 12, placedArea.Bottom - Height - 12);
        }

        // the window grows once the inputs arrive: keep it above the taskbar, unless it was moved
        void KeepNearTray()
        {
            if (Location != placedAt) return;
            Location = placedAt = new Point(placedArea.Right - Width - 12, placedArea.Bottom - Height - 12);
        }

        // ---- layouts (positions in 96-dpi pixels; the window scales them) ----

        void BuildClassic()
        {
            int w = 232;
            board.Size = new Size(w, 368);
            pin = Add(Glyphs.Pin, "", "Keep the remote on top", 12, 16, 28, 28, null);
            pin.Selected = true;
            pin.Click += delegate { TogglePin(); };
            ToPcButton(46, 12, 116);
            PowerButton(184, 12);
            Add(Glyphs.Back, "", "Back", 12, 56, 36, 36, RemoteKeys.Back);
            Add(Glyphs.Home, "", "Home", 184, 56, 36, 36, RemoteKeys.Home);
            Pad(116, 160);
            Row(238, w, new[] { Glyphs.Replay, "*", Glyphs.Search }, new[] { "Instant replay", "Options", "Search" },
                new[] { RemoteKeys.Replay, RemoteKeys.Options, RemoteKeys.Search });
            Row(280, w, new[] { Glyphs.Rewind, Glyphs.Play, Glyphs.Forward }, new[] { "Rewind", "Play or pause", "Fast forward" },
                new[] { RemoteKeys.Rewind, RemoteKeys.Play, RemoteKeys.Forward });
            Row(322, w, new[] { Glyphs.VolumeDown, Glyphs.Mute, Glyphs.VolumeUp }, new[] { "Volume down", "Mute", "Volume up" },
                new[] { RemoteKeys.VolumeDown, RemoteKeys.Mute, RemoteKeys.VolumeUp });
            Stack(w, true);
        }

        void BuildCompact()
        {
            int w = 268;
            board.Size = new Size(w, 248);
            PowerButton(12, 12);
            inputMenu = Add(Glyphs.Down, "Inputs", "Pick an input", 54, 12, 160, 36, null);
            inputMenu.GlyphPoints = 9f;
            inputMenu.Click += delegate { ShowInputMenu(); };
            pin = Add(Glyphs.Pin, "", "Keep the remote on top", 220, 12, 36, 36, null);
            pin.Selected = true;
            pin.Click += delegate { TogglePin(); };
            Add(Glyphs.Back, "", "Back", 12, 58, 36, 36, RemoteKeys.Back);
            Add(Glyphs.Home, "", "Home", 12, 106, 36, 36, RemoteKeys.Home);
            Add("*", "", "Options", 12, 154, 36, 36, RemoteKeys.Options);
            Pad(134, 126);
            Add(Glyphs.Plus, "", "Volume up", 220, 58, 36, 36, RemoteKeys.VolumeUp);
            Add(Glyphs.Mute, "", "Mute", 220, 106, 36, 36, RemoteKeys.Mute);
            Add(Glyphs.Minus, "", "Volume down", 220, 154, 36, 36, RemoteKeys.VolumeDown);
            Row(202, w, new[] { Glyphs.Replay, Glyphs.Rewind, Glyphs.Play, Glyphs.Forward, Glyphs.Search },
                new[] { "Instant replay", "Rewind", "Play or pause", "Fast forward", "Search" },
                new[] { RemoteKeys.Replay, RemoteKeys.Rewind, RemoteKeys.Play, RemoteKeys.Forward, RemoteKeys.Search });
            Stack(w, false);
        }

        void BuildStrip()
        {
            int w = 604;
            board.Size = new Size(w, 148);
            PowerButton(12, 12);
            pin = Add(Glyphs.Pin, "", "Keep the remote on top", 56, 12, 36, 36, null);
            pin.Selected = true;
            pin.Click += delegate { TogglePin(); };
            ToPcButton(12, 56, 80);
            Pad(176, 76);
            string[] keys = { RemoteKeys.Back, RemoteKeys.Home, RemoteKeys.Options, RemoteKeys.Rewind, RemoteKeys.Play, RemoteKeys.Forward,
                RemoteKeys.VolumeDown, RemoteKeys.Mute, RemoteKeys.VolumeUp };
            string[] glyphs = { Glyphs.Back, Glyphs.Home, "*", Glyphs.Rewind, Glyphs.Play, Glyphs.Forward, Glyphs.Minus, Glyphs.Mute, Glyphs.Plus };
            string[] names = { "Back", "Home", "Options", "Rewind", "Play or pause", "Fast forward", "Volume down", "Mute", "Volume up" };
            for (int i = 0; i < 9; i++) Add(glyphs[i], "", names[i], 262 + (i % 3) * 42, 12 + (i / 3) * 44, 36, 36, keys[i]);
            Add(Glyphs.Replay, "", "Instant replay", 394, 12, 36, 36, RemoteKeys.Replay);
            Add(Glyphs.Search, "", "Search", 394, 56, 36, 36, RemoteKeys.Search);
            var kb = Add(Glyphs.Keyboard, "", "Type on the TV", 394, 100, 36, 36, null);
            kb.Click += delegate { typing.Focus(); };
            // inputs as a drop-down and the typing box on the right, so it stays one row
            inputMenu = Add(Glyphs.Down, "Inputs", "Pick an input", 442, 12, 150, 36, null);
            inputMenu.GlyphPoints = 9f;
            inputMenu.Click += delegate { ShowInputMenu(); };
            board.Controls.Add(new Label { Text = "Type on the TV:", AutoSize = true, Location = new Point(442, 62), ForeColor = Theme.Muted });
            typing = TypingBox(150);
            typing.Location = new Point(442, 84);
            board.Controls.Add(typing);
            Stack(w, false);
        }

        // the arrow pad around OK, centred on (cx, cy)
        void Pad(int cx, int cy)
        {
            Add(Glyphs.Up, "", "Up", cx - 18, cy - 66, 36, 36, RemoteKeys.Up);
            Add(Glyphs.Left, "", "Left", cx - 64, cy - 18, 36, 36, RemoteKeys.Left);
            PadButton ok = Add("", "OK", "OK", cx - 24, cy - 24, 48, 48, RemoteKeys.Ok);
            ok.Accent = ok.Round = true;
            Add(Glyphs.Right, "", "Right", cx + 28, cy - 18, 36, 36, RemoteKeys.Right);
            Add(Glyphs.Down, "", "Down", cx - 18, cy + 30, 36, 36, RemoteKeys.Down);
        }

        // buttons in a centred row
        void Row(int y, int width, string[] glyphs, string[] names, string[] keys)
        {
            int total = glyphs.Length * 36 + (glyphs.Length - 1) * 6, x = (width - total) / 2;
            for (int i = 0; i < glyphs.Length; i++) Add(glyphs[i], "", names[i], x + i * 42, y, 36, 36, keys[i]);
        }

        void PowerButton(int x, int y)
        {
            PadButton power = Add(Glyphs.Power, "", "Power", x, y, 36, 36, null);
            power.Danger = true;
            power.Click += delegate
            {
                sender.Send(tv =>
                {
                    DeviceInfo info = tv.GetDeviceInfo(2000);
                    return tv.Keypress(info != null && info.IsOn ? "PowerOff" : "PowerOn", 2500);
                });
            };
        }

        void ToPcButton(int x, int y, int width)
        {
            PadButton toPc = Add(Glyphs.Tv, "TV to PC", "Turn the TV on and switch it to this PC", x, y, width, 36, null);
            toPc.GlyphPoints = 11f;
            toPc.Click += delegate
            {
                Controller c = controller;
                Task.Run(() =>
                {
                    try { c.TurnOn(TurnOnMode.ToPc); }
                    catch (Exception e) { Log.Write("pc: error: " + e.Message); }
                });
                SetStatus(null, "Switching the TV to this PC…");
            };
        }

        PadButton Add(string glyph, string label, string name, int x, int y, int w, int h, string key)
        {
            var b = new PadButton { Glyph = glyph == "*" ? "" : glyph, Label = glyph == "*" ? "✱" : label, Location = new Point(x, y), Size = new Size(w, h) };
            b.AccessibleName = name;
            b.Tag = key;
            tips.SetToolTip(b, name);
            if (key == RemoteKeys.VolumeUp || key == RemoteKeys.VolumeDown)
            {
                // held down, the volume keeps going
                b.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { sender.Press(key); repeatKey = key; repeat.Interval = 400; repeat.Start(); } };
                b.MouseUp += delegate { repeat.Stop(); repeatKey = null; };
                b.MouseLeave += delegate { repeat.Stop(); repeatKey = null; };
            }
            else if (key != null) b.Click += delegate { sender.Press(key); };
            board.Controls.Add(b);
            return b;
        }

        // the board, then the input buttons (classic), the typing box and the status line, stacked
        void Stack(int width, bool inputButtons)
        {
            var column = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = Point.Empty,
                BackColor = Theme.Window,
                Padding = new Padding(0, 0, 0, 10)
            };
            board.Margin = Padding.Empty;
            column.Controls.Add(board);
            if (inputButtons)
            {
                chips = new FlowLayoutPanel
                {
                    AutoSize = true,
                    MaximumSize = new Size(width - 24, 0),
                    MinimumSize = new Size(width - 24, 0),
                    Margin = new Padding(12, 0, 12, 8),
                    BackColor = Theme.Window
                };
                column.Controls.Add(chips);
            }

            if (typing == null) // the strip has its typing box on the board already
            {
                var row = new Panel { Size = new Size(width - 24, 30), Margin = new Padding(12, 0, 12, 4), BackColor = Theme.Window };
                var key = new Label { Text = Glyphs.Keyboard, Font = Theme.Icons(12f), ForeColor = Theme.Muted, AutoSize = false, Size = new Size(24, 30), TextAlign = ContentAlignment.MiddleLeft };
                typing = TypingBox(width - 24 - 26);
                typing.Location = new Point(26, 3);
                row.Controls.Add(key);
                row.Controls.Add(typing);
                column.Controls.Add(row);
            }

            status = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(width - 24, 0),
                Margin = new Padding(12, 2, 12, 0),
                ForeColor = Theme.Muted,
                Text = ""
            };
            column.Controls.Add(status);

            Controls.Add(column);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        // the box that types into the TV's search: each letter is sent as it's typed
        TextBox TypingBox(int width)
        {
            var box = new TextBox
            {
                Width = width,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Button,
                ForeColor = Theme.Text
            };
            box.HandleCreated += delegate { SendMessage(box.Handle, 0x1501, (IntPtr)1, "Type to search on the TV"); }; // EM_SETCUEBANNER
            box.KeyPress += (s, e) =>
            {
                if (e.KeyChar >= ' ') sender.Press(RemoteKeys.Letter(e.KeyChar));
            };
            box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Back) sender.Press("Backspace");
                else if (e.KeyCode == Keys.Enter) { sender.Press("Enter"); e.SuppressKeyPress = true; }
            };
            tips.SetToolTip(box, "What you type here goes into the TV's search boxes");
            return box;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        // ---- inputs ----

        async void LoadInputs()
        {
            var client = new RokuClient(settings.Ip, settings.Port);
            List<KeyValuePair<string, string>> found = null;
            string active = null;
            await Task.Run(() =>
            {
                found = client.GetTvInputs(2500);
                active = client.GetActiveApp(2500);
            });
            if (IsDisposed) return;
            if (found == null || found.Count == 0)
            {
                // a TV on Limited doesn't list them; these are the usual ones
                found = new List<KeyValuePair<string, string>>();
                for (int i = 1; i <= 4; i++) found.Add(new KeyValuePair<string, string>("tvinput.hdmi" + i, ""));
            }
            inputs = found;
            showing = active ?? "";
            ShowInputs();
            if (active == null) SetStatus(false, "The TV isn't answering. Is it on, and on this network?");
            PerformLayout();
            KeepNearTray();
        }

        static string InputName(KeyValuePair<string, string> input)
        {
            string id = input.Key;
            if (id.StartsWith("tvinput.hdmi"))
            {
                string hdmi = "HDMI " + id.Substring("tvinput.hdmi".Length);
                return input.Value.Length == 0 || input.Value == hdmi ? hdmi : hdmi + " · " + input.Value;
            }
            return input.Value.Length > 0 ? input.Value : id.Substring("tvinput.".Length);
        }

        void ShowInputs()
        {
            if (inputMenu != null)
            {
                KeyValuePair<string, string> current = inputs.Find(p => p.Key == showing);
                inputMenu.Label = current.Key != null ? InputName(current) : "Inputs";
                inputMenu.Invalidate();
                return;
            }
            if (chips == null) return;
            chips.SuspendLayout();
            chips.Controls.Clear();
            foreach (KeyValuePair<string, string> input in inputs)
            {
                string label = InputName(input), id = input.Key;
                var chip = new PadButton { Label = label, Selected = id == showing, Margin = new Padding(0, 0, 6, 6) };
                chip.Size = new Size(TextRenderer.MeasureText(label, Font).Width + 22, 28);
                if (layout == Strip) chip.Width = Math.Max(chip.Width, 126);
                chip.AccessibleName = "Switch the TV to " + label;
                tips.SetToolTip(chip, chip.AccessibleName);
                chip.Click += delegate { SwitchTo(id); };
                chips.Controls.Add(chip);
            }
            chips.ResumeLayout();
        }

        void ShowInputMenu()
        {
            var menu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };
            foreach (KeyValuePair<string, string> input in inputs)
            {
                string id = input.Key;
                var item = new ToolStripMenuItem(InputName(input)) { Checked = id == showing };
                item.Click += delegate { SwitchTo(id); };
                menu.Items.Add(item);
            }
            menu.Show(inputMenu, new Point(0, inputMenu.Height));
        }

        void SwitchTo(string id)
        {
            sender.Launch(id);
            showing = id;
            ShowInputs();
        }

        // ---- keyboard, pin, status ----

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (typing == null || !typing.Focused)
            {
                string key = RemoteKeys.ForKey(keyData);
                if (key != null) { sender.Press(key); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void TogglePin()
        {
            TopMost = !TopMost;
            pin.Selected = TopMost;
            pin.Invalidate();
            tips.SetToolTip(pin, TopMost ? "Kept on top (click to let other windows cover it)" : "Keep the remote on top");
        }

        void ShowAnswer(int code)
        {
            if (code == 403)
                SetStatus(false, "Your TV's Network access is on Limited, so it ignores these buttons. On the TV: Settings → System → " +
                    "Advanced system settings → Control by mobile apps → Network access → Enabled.");
            else if (code == 0)
                SetStatus(false, "The TV isn't answering. Is it on, and on this network?");
            else
                SetStatus(null, "");
        }

        void SetStatus(bool? ok, string text)
        {
            if (status == null) return;
            status.Text = text;
            status.ForeColor = ok == false ? Theme.Warn : Theme.Muted;
        }

        // for the tests and the screenshot tool
        public List<PadButton> Buttons()
        {
            var list = new List<PadButton>();
            foreach (Control c in board.Controls) if (c is PadButton) list.Add((PadButton)c);
            return list;
        }

        public Size BoardSize { get { return board.Size; } }
    }
}
