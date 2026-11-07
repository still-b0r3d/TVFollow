using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow
{
    // The pushed app's console (port 8085 while developer mode is on): what the app prints, and
    // the error and the line it stopped at when it crashes. Read only; it keeps reconnecting while
    // the window is open.
    class DevLogForm : Form
    {
        public const int MaxChars = 400000;  // older lines go once the log gets this long

        public readonly string Ip;
        readonly TextBox text;
        readonly Label state;
        readonly object clientLock = new object();
        TcpClient client;   // the open connection; closing it stops the reading
        volatile bool closing;
        int telnet;         // where a telnet command the console sends stands (they aren't text)

        public DevLogForm(string ip, string label)
        {
            Ip = ip;
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

            Shown += delegate
            {
                var reader = new Thread(Read) { IsBackground = true, Name = "app log" };
                reader.Start();
            };
            FormClosing += delegate
            {
                closing = true;
                lock (clientLock) if (client != null) client.Close();
            };
        }

        public string LogText { get { return text.Text; } }  // for the tests

        static Button MakeButton(string label, EventHandler click)
        {
            var b = new Button { Text = label, AutoSize = true, MinimumSize = new Size(84, 30), UseVisualStyleBackColor = true, Margin = new Padding(8, 0, 0, 0) };
            b.Click += click;
            return b;
        }

        void Read()
        {
            Decoder decoder = Encoding.UTF8.GetDecoder();
            var bytes = new byte[4096];
            var kept = new byte[4096];
            var chars = new char[Encoding.UTF8.GetMaxCharCount(kept.Length)];
            while (!closing)
            {
                bool connected = false;
                try
                {
                    TcpClient c = DevClient.OpenConsole(Ip, 3000);
                    lock (clientLock)
                    {
                        if (closing) { c.Close(); return; }
                        client = c;
                    }
                    connected = true;
                    telnet = 0;
                    Status("Connected. What the app prints shows up here.");
                    NetworkStream stream = c.GetStream();
                    int n;
                    while (!closing && (n = stream.Read(bytes, 0, bytes.Length)) > 0)
                    {
                        int count = decoder.GetChars(kept, 0, Plain(bytes, n, kept), chars, 0);
                        if (count > 0) Append(new string(chars, 0, count).Replace("\r", "").Replace("\n", "\r\n"));
                    }
                }
                catch (Exception e)
                {
                    if (closing) return;
                    if (!connected) Status("Can't reach the app log (" + Why(e) + "). It's there while developer mode is on. Trying again…");
                }
                finally
                {
                    lock (clientLock)
                    {
                        if (client != null) client.Close();
                        client = null;
                    }
                }
                if (connected && !closing) Status("The TV closed the app log. Trying again…");
                for (int i = 0; i < 30 && !closing; i++) Thread.Sleep(100);
            }
        }

        // the text, without the telnet commands a console can send (IAC = 255, then a command,
        // and for WILL, WONT, DO and DONT an option)
        int Plain(byte[] input, int n, byte[] output)
        {
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                byte b = input[i];
                if (telnet == 1) { telnet = b >= 251 && b <= 254 ? 2 : 0; if (b == 255) output[k++] = b; continue; }
                if (telnet == 2) { telnet = 0; continue; }
                if (b == 255) { telnet = 1; continue; }
                output[k++] = b;
            }
            return k;
        }

        static string Why(Exception e)
        {
            var socket = e as SocketException;
            if (socket != null && socket.SocketErrorCode == SocketError.ConnectionRefused) return "the TV refused";
            if (e is TimeoutException) return "no answer";
            return e.Message.TrimEnd('.');
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
