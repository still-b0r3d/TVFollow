using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TVFollow
{
    // Something that reads a TV's app console: told each time it connects, each piece of text,
    // and trouble in words. These run under the console's lock (on its own thread, or on the
    // caller's when Listen catches a listener up), one at a time, and must be quick.
    class ConsoleListener
    {
        public Action Connected = delegate { };
        public Action<string> Text = delegate { };
        public Action<string> Trouble = delegate { };
    }

    // The pushed app's console on one TV (port 8085 while developer mode is on): what the app
    // prints, and the error and the line it stopped at when it crashes. The TV lets one program
    // read it at a time, so each TV has one connection, shared by the app log window and the
    // app's asks for inputs. It's open while anything listens, connects again a few seconds
    // after it drops, and keeps the recent text for a listener that comes later.
    class DevConsole
    {
        public static int RetryMs = 3000;
        public const int KeepChars = 32000;

        readonly Func<string> address;   // the TV's address as it is now (it can change while TVFollow runs)
        readonly object gate = new object();
        readonly List<ConsoleListener> listeners = new List<ConsoleListener>();
        readonly StringBuilder recent = new StringBuilder();
        TcpClient client;   // the open connection; closing it stops the reading
        Thread reader;
        bool connected;
        int telnet;         // where a telnet command the console sends stands (they aren't text); the reader's own

        public DevConsole(Func<string> address)
        {
            this.address = address;
        }

        // Starts reading if nothing was. A listener that comes while the console is connected
        // is told so at once, and gets the recent text.
        public void Listen(ConsoleListener listener)
        {
            lock (gate)
            {
                if (listeners.Contains(listener)) return;
                listeners.Add(listener);
                if (reader == null)
                {
                    reader = new Thread(Read) { IsBackground = true, Name = "app console" };
                    reader.Start();
                }
                else if (connected)
                {
                    listener.Connected();
                    if (recent.Length > 0) listener.Text(recent.ToString());
                }
            }
        }

        // The last listener gone, the connection closes.
        public void Unlisten(ConsoleListener listener)
        {
            lock (gate)
            {
                listeners.Remove(listener);
                if (listeners.Count == 0 && client != null) client.Close();
            }
        }

        public bool Connected
        {
            get { lock (gate) return connected; }
        }

        void Read()
        {
            Decoder decoder = Encoding.UTF8.GetDecoder();
            var bytes = new byte[4096];
            var kept = new byte[4096];
            var chars = new char[Encoding.UTF8.GetMaxCharCount(kept.Length)];
            while (true)
            {
                if (Idle()) return;
                bool wasConnected = false;
                TcpClient c = null;
                try
                {
                    c = DevClient.OpenConsole(address(), 3000);
                    lock (gate)
                    {
                        if (listeners.Count == 0)
                        {
                            // nobody listens any more; a new reader may already be on its way
                            c.Close();
                            reader = null;
                            return;
                        }
                        client = c;
                        connected = true;
                        recent.Length = 0;
                        telnet = 0;
                        decoder.Reset();
                        foreach (ConsoleListener l in listeners.ToArray()) l.Connected();
                    }
                    wasConnected = true;
                    NetworkStream stream = c.GetStream();
                    int n;
                    while ((n = stream.Read(bytes, 0, bytes.Length)) > 0)
                    {
                        int count = decoder.GetChars(kept, 0, Plain(bytes, n, kept), chars, 0);
                        if (count == 0) continue;
                        string chunk = new string(chars, 0, count);
                        lock (gate)
                        {
                            recent.Append(chunk);
                            if (recent.Length > KeepChars) recent.Remove(0, recent.Length - KeepChars / 2);
                            foreach (ConsoleListener l in listeners.ToArray()) l.Text(chunk);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!wasConnected) Tell("Can't reach the app log (" + Why(e) + "). It's there while developer mode is on. Trying again…");
                }
                finally
                {
                    // only this pass's own connection: after the early return above, a new
                    // reader's may be the one there now
                    lock (gate)
                    {
                        if (c != null && client == c)
                        {
                            client = null;
                            connected = false;
                        }
                    }
                    if (c != null) c.Close();
                }
                if (wasConnected) Tell("The TV closed the app log. Trying again…");
                // a while before the next try, cut short once nobody listens
                for (int waited = 0; waited < RetryMs; waited += 100)
                {
                    lock (gate) if (listeners.Count == 0) break;
                    Thread.Sleep(Math.Min(100, RetryMs));
                }
            }
        }

        // nobody listens any more: this reader stops (a new one starts with the next listener)
        bool Idle()
        {
            lock (gate)
            {
                if (listeners.Count > 0) return false;
                reader = null;
                return true;
            }
        }

        void Tell(string words)
        {
            lock (gate) foreach (ConsoleListener l in listeners.ToArray()) l.Trouble(words);
        }

        // The text, without the telnet commands a console can send: IAC (255), then a command, an
        // option after WILL, WONT, DO and DONT (251-254), and after SB (250) everything up to IAC
        // SE (255 240). IAC IAC is a 255 in the text.
        int Plain(byte[] input, int n, byte[] output)
        {
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                byte b = input[i];
                switch (telnet)
                {
                    case 1:
                        if (b == 255) { output[k++] = b; telnet = 0; }
                        else telnet = b == 250 ? 3 : b >= 251 && b <= 254 ? 2 : 0;
                        continue;
                    case 2: telnet = 0; continue;
                    case 3: if (b == 255) telnet = 4; continue;
                    case 4: telnet = b == 240 ? 0 : 3; continue;
                }
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
    }
}
