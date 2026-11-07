using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TVFollow
{
    // The pushed app's console (port 8085 while developer mode is on): what the app prints, and
    // the error and the line it stopped at when it crashes. Read only, through the TV's one
    // shared console connection, which keeps reconnecting while the window is open.
    class DevLogForm : Form
    {
        public const int MaxChars = 400000;  // older lines go once the log gets this long

        public readonly DevConsole Console;
        readonly TextBox text;
        readonly Label state;
        readonly ConsoleListener listener = new ConsoleListener();
        volatile bool closing;

        public DevLogForm(DevConsole console, string label)
        {
            Console = console;
            SuspendLayout();
            Text = "App log · " + label;
            Font = SystemFonts.MessageBoxFont;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 480);
            MinimumSize = new Size(480, 260);
            try { Icon = AppIcons.Load("tv.ico", SystemInformation.IconSize); } catch { }

            text = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9.75f),
                BackColor = SystemColors.Window,
                HideSelection = false
            };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Color.FromArgb(243, 243, 243) };
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 9, 12, 0),
                BackColor = bottom.BackColor
            };
            var close = MakeButton("Close", delegate { Close(); });
            CancelButton = close;
            buttons.Controls.Add(close);
            buttons.Controls.Add(MakeButton("Save…", delegate { SaveLog(); }));
            buttons.Controls.Add(MakeButton("Copy all", delegate { CopyAll(); }));
            buttons.Controls.Add(MakeButton("Clear", delegate { text.Clear(); }));
            state = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                ForeColor = SetupPage.Muted,
                Text = "Connecting to the app log…",
                UseMnemonic = false
            };
            bottom.Controls.Add(state);
            bottom.Controls.Add(buttons);
            Controls.Add(text);
            Controls.Add(bottom);
            text.BringToFront();   // laid out last, so it fills what the bottom row leaves
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();

            listener.Connected = () => Status("Connected. What the app prints shows up here.");
            listener.Text = chunk => Append(chunk.Replace("\r", "").Replace("\n", "\r\n"));
            listener.Trouble = Status;
            Shown += delegate { Console.Listen(listener); };
            FormClosing += delegate
            {
                closing = true;
                Console.Unlisten(listener);
            };
        }

        public string LogText { get { return text.Text; } }  // for the tests

        static Button MakeButton(string label, EventHandler click)
        {
            var b = new Button { Text = label, AutoSize = true, MinimumSize = new Size(84, 30), UseVisualStyleBackColor = true, Margin = new Padding(8, 0, 0, 0) };
            b.Click += click;
            return b;
        }

        void Status(string words)
        {
            Post(() => state.Text = words);
        }

        void Append(string chunk)
        {
            Post(() =>
            {
                if (text.TextLength + chunk.Length > MaxChars) text.Text = text.Text.Substring(Math.Min(text.TextLength, text.TextLength + chunk.Length - MaxChars / 2));
                text.AppendText(chunk);
            });
        }

        void Post(Action action)
        {
            if (closing || IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)(() => { if (!IsDisposed) action(); })); }
            catch (InvalidOperationException) { }  // closing
        }

        void CopyAll()
        {
            if (text.TextLength == 0) return;
            try { Clipboard.SetText(text.Text); }
            catch (Exception e) { state.Text = "Couldn't copy: " + e.Message; }
        }

        void SaveLog()
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Save the app log",
                FileName = "App log " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ".txt",
                Filter = "Text (*.txt)|*.txt"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllText(dialog.FileName, text.Text); }
                catch (Exception e) { state.Text = "Couldn't save it: " + e.Message; }
            }
        }
    }
}
