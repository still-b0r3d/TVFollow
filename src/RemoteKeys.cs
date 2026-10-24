using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow
{
    // The remote's buttons and what they send. Key names are Roku's (External Control Protocol).
    static class RemoteKeys
    {
        public const string Up = "Up", Down = "Down", Left = "Left", Right = "Right", Ok = "Select";
        public const string Back = "Back", Home = "Home", Options = "Info", Replay = "InstantReplay", Search = "Search";
        public const string Rewind = "Rev", Play = "Play", Forward = "Fwd";
        public const string VolumeUp = "VolumeUp", VolumeDown = "VolumeDown", Mute = "VolumeMute";

        // Keyboard keys while the remote has focus (not the typing box): the arrows, Enter for OK,
        // Backspace for Back, Space for play/pause, Home, and + / - for volume. Null if the key
        // isn't one of them.
        public static string ForKey(Keys key)
        {
            switch (key)
            {
                case Keys.Up: return Up;
                case Keys.Down: return Down;
                case Keys.Left: return Left;
                case Keys.Right: return Right;
                case Keys.Enter: return Ok;
                case Keys.Back: return Back;
                case Keys.Space: return Play;
                case Keys.Home: return Home;
                case Keys.Add:
                case Keys.Oemplus: return VolumeUp;
                case Keys.Subtract:
                case Keys.OemMinus: return VolumeDown;
                default: return null;
            }
        }

        // a typed character for the TV's on-screen keyboard
        public static string Letter(char c)
        {
            return "Lit_" + Uri.EscapeDataString(c.ToString());
        }
    }

    // Sends the remote's presses one at a time, in order (typing must arrive in order), on a
    // background thread so the window never waits on the network.
    class RemoteSender : IDisposable
    {
        readonly Func<RokuClient> client;
        readonly BlockingCollection<Func<RokuClient, int>> queue = new BlockingCollection<Func<RokuClient, int>>();
        readonly Thread thread;

        // what the TV said to the last press: 2xx fine, 403 blocked by Limited, 0 no answer
        public event Action<int> Answered = delegate { };

        public RemoteSender(Func<RokuClient> client)
        {
            this.client = client;
            thread = new Thread(Run) { IsBackground = true, Name = "remote" };
            thread.Start();
        }

        public void Press(string key) { Send(tv => tv.Keypress(key, 2500)); }

        public void Launch(string appId) { Send(tv => tv.Launch(appId, 2500)); }

        public void Send(Func<RokuClient, int> action)
        {
            if (!queue.IsAddingCompleted) queue.Add(action);
        }

        void Run()
        {
            foreach (Func<RokuClient, int> action in queue.GetConsumingEnumerable())
            {
                int code;
                try { code = action(client()); }
                catch { code = 0; }
                Answered(code);
            }
        }

        public void Dispose() { queue.CompleteAdding(); }
    }
}
