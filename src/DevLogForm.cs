using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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
                MaxLength = 0,
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
        internal TextBox LogBox { get { return text; } }    // for the tests

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
            Post(() => AppendChunk(chunk));
        }

        const int SB_VERT = 1, SB_BOTTOM = 7;
        const int SIF_RANGE = 0x1, SIF_PAGE = 0x2, SIF_POS = 0x4;
        const int WM_SETREDRAW = 0x000B, WM_VSCROLL = 0x0115;
        const int EM_SETSEL = 0x00B1, EM_LINESCROLL = 0x00B6, EM_REPLACESEL = 0x00C2, EM_GETFIRSTVISIBLELINE = 0x00CE;
        const int RDW_INVALIDATE = 0x0001, RDW_ERASE = 0x0004, RDW_ALLCHILDREN = 0x0080, RDW_FRAME = 0x0400;

        [StructLayout(LayoutKind.Sequential)]
        struct SCROLLINFO { public int cbSize, fMask, nMin, nMax; public uint nPage; public int nPos, nTrackPos; }

        [DllImport("user32.dll")]
        static extern bool GetScrollInfo(IntPtr hwnd, int fnBar, ref SCROLLINFO lpsi);

        [DllImport("user32.dll")]
        static extern bool RedrawWindow(IntPtr hwnd, IntPtr rcUpdate, IntPtr hrgnUpdate, int flags);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, string lParam);

        bool AtBottom()
        {
            var si = new SCROLLINFO { cbSize = Marshal.SizeOf(typeof(SCROLLINFO)), fMask = SIF_RANGE | SIF_PAGE | SIF_POS };
            if (!GetScrollInfo(text.Handle, SB_VERT, ref si)) return true;
            if (si.nPage == 0 || si.nPage > (uint)(si.nMax - si.nMin)) return true;
            return si.nPos + (int)si.nPage > si.nMax;
        }

        void ScrollToBottom()
        {
            SendMessage(text.Handle, WM_VSCROLL, (IntPtr)SB_BOTTOM, IntPtr.Zero);
        }

        internal void AppendChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk) || text.IsDisposed || !text.IsHandleCreated) return;
            bool atBottom = AtBottom();
            int selStart = text.SelectionStart, selLength = text.SelectionLength;
            bool keepView = !atBottom || selLength > 0;
            int firstLine = (int)SendMessage(text.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);

            if (chunk.Length >= MaxChars)
            {
                text.Text = chunk.Substring(chunk.Length - MaxChars / 2);
                if (!keepView)
                {
                    int newEnd = text.TextLength;
                    SendMessage(text.Handle, EM_SETSEL, (IntPtr)newEnd, (IntPtr)newEnd);
                    ScrollToBottom();
                }
                return;
            }

            SendMessage(text.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                int cut = 0, cutLines = 0;
                if (text.TextLength + chunk.Length > MaxChars)
                {
                    cut = Math.Min(text.TextLength, text.TextLength + chunk.Length - MaxChars / 2);
                    int nl = text.Text.IndexOf('\n', cut, Math.Min(4096, text.TextLength - cut));
                    if (nl >= 0) cut = nl + 1;
                    else if (cut > 0 && cut < text.TextLength && text.Text[cut - 1] == '\r' && text.Text[cut] == '\n') cut++;
                    cutLines = text.GetLineFromCharIndex(cut);
                    SendMessage(text.Handle, EM_SETSEL, IntPtr.Zero, (IntPtr)cut);
                    SendMessage(text.Handle, EM_REPLACESEL, IntPtr.Zero, "");
                }

                int end = text.TextLength;
                SendMessage(text.Handle, EM_SETSEL, (IntPtr)end, (IntPtr)end);
                SendMessage(text.Handle, EM_REPLACESEL, IntPtr.Zero, chunk);

                if (keepView)
                {
                    int newStart = Math.Max(0, selStart - cut);
                    int newLength = selStart < cut ? Math.Max(0, selLength - (cut - selStart)) : selLength;
                    newStart = Math.Min(newStart, text.TextLength);
                    newLength = Math.Min(newLength, text.TextLength - newStart);
                    SendMessage(text.Handle, EM_SETSEL, (IntPtr)newStart, (IntPtr)(newStart + newLength));
                    int targetFirst = Math.Max(0, firstLine - cutLines);
                    int nowFirst = (int)SendMessage(text.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
                    if (targetFirst != nowFirst)
                        SendMessage(text.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)(targetFirst - nowFirst));
                }
                else
                {
                    int newEnd = text.TextLength;
                    SendMessage(text.Handle, EM_SETSEL, (IntPtr)newEnd, (IntPtr)newEnd);
                    ScrollToBottom();
                }
            }
            finally
            {
                SendMessage(text.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                RedrawWindow(text.Handle, IntPtr.Zero, IntPtr.Zero, RDW_ERASE | RDW_FRAME | RDW_INVALIDATE | RDW_ALLCHILDREN);
            }
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
