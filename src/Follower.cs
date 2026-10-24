using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow
{
    // Decides what the TV should do when the monitors change. Kept apart from Windows
    // messages so the tests can drive it.
    class FollowRules
    {
        // The monitors can come on with nobody there (a screen being plugged in, a notification),
        // so the TV only follows them on once there's mouse or keyboard input.
        public const double FreshInputSec = 5;

        public Func<double> SecondsSinceInput = UserInput.SecondsSinceInput;
        public Func<uint> LastInputTick = UserInput.LastInputTick;
        public Action<bool> Want = delegate { };  // true: TV on, false: TV off
        public bool OnWithMonitors = true;
        public bool OffWithMonitors = true;
        public bool Paused;

        bool waitingForInput;
        uint inputTickAtWake;
        int lastDisplay = -1;

        // Windows' display state: 0 off, 1 on, 2 dimmed. Coming back from dimmed isn't the
        // monitors coming back on (they never went off), so it's ignored; on after on still
        // counts, since sleep doesn't always send "off" first.
        public void Display(int state)
        {
            int before = lastDisplay;
            lastDisplay = state;
            if (state == 0) MonitorsOff();
            else if (state == 1 && before != 2) MonitorsOn();
        }

        // after sleep, the next "on" always counts
        public void Resumed() { lastDisplay = -1; }

        public void MonitorsOn()
        {
            double idle = SecondsSinceInput();
            Log.Write("monitors on (last input " + idle.ToString("0.0") + "s ago)");
            waitingForInput = false;
            if (Paused || !OnWithMonitors) return;
            if (idle <= FreshInputSec) { Want(true); return; }
            waitingForInput = true;
            inputTickAtWake = LastInputTick();
            Log.Write("waiting for the mouse or keyboard before turning the TV on");
        }

        public void MonitorsOff()
        {
            Log.Write("monitors off");
            waitingForInput = false;
            if (Paused || !OffWithMonitors) return;
            Want(false);
        }

        // called about twice a second
        public void CheckForInput()
        {
            if (!waitingForInput || LastInputTick() == inputTickAtWake) return;
            waitingForInput = false;
            if (Paused) return;
            Log.Write("input seen");
            Want(true);
        }
    }

    static class UserInput
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

        public static uint LastInputTick()
        {
            var info = new LASTINPUTINFO();
            info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            GetLastInputInfo(ref info);
            return info.dwTime;
        }

        public static double SecondsSinceInput()
        {
            return unchecked((uint)Environment.TickCount - LastInputTick()) / 1000.0;
        }
    }

    // A hidden window that hears the monitors turn on and off, sleep and shutdown, and a worker
    // thread that does what the rules want so the window never waits on the TV.
    class Follower : NativeWindow
    {
        const int WM_QUERYENDSESSION = 0x11, WM_ENDSESSION = 0x16, WM_POWERBROADCAST = 0x218;
        const int PBT_APMSUSPEND = 0x4, PBT_POWERSETTINGCHANGE = 0x8013;
        static Guid GUID_CONSOLE_DISPLAY_STATE = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

        public readonly FollowRules Rules = new FollowRules();

        public Settings Settings { get { return settings; } }

        readonly Settings settings;
        readonly Controller controller;
        readonly AutoResetEvent kick = new AutoResetEvent(false);
        int wanted = -1;
        System.Windows.Forms.Timer tick;
        EventWaitHandle quit;

        public Follower(Settings settings, Controller controller)
        {
            this.settings = settings;
            this.controller = controller;
            Rules.OnWithMonitors = settings.OnWithMonitors;
            Rules.OffWithMonitors = settings.OffWithMonitors;
            Rules.Paused = settings.Paused;
            Rules.Want = Want;
        }

        public void Start()
        {
            var worker = new Thread(Work);
            worker.IsBackground = true;
            worker.Start();

            var cp = new CreateParams();
            cp.Caption = "TVFollow";
            CreateHandle(cp); // hidden top-level window, so it also hears sleep and shutdown
            // Windows answers right away with the current state, which turns the TV on at sign-in
            RegisterPowerSettingNotification(Handle, ref GUID_CONSOLE_DISPLAY_STATE, 0);

            quit = new EventWaitHandle(false, EventResetMode.AutoReset, Instance.QuitName);
            showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, Instance.SettingsName);
            tick = new System.Windows.Forms.Timer();
            tick.Interval = 500;
            tick.Tick += delegate
            {
                if (quit.WaitOne(0))
                {
                    Log.Write("stopping: a new copy with new settings is taking over");
                    tick.Stop();
                    Application.ExitThread();
                    return;
                }
                if (showSettings.WaitOne(0)) SettingsRequested();
                Rules.CheckForInput();
            };
            tick.Start();
        }

        // TVFollow was opened again while this copy runs
        public event Action SettingsRequested = delegate { };
        EventWaitHandle showSettings;

        void Want(bool on)
        {
            Interlocked.Exchange(ref wanted, on ? 1 : 0);
            kick.Set();
        }

        void Work()
        {
            while (true)
            {
                kick.WaitOne();
                Thread.Sleep(1000); // let quick off/on flickers settle
                try
                {
                    int state = Thread.VolatileRead(ref wanted);
                    if (state == 1) controller.TurnOn(TurnOnMode.Follow);
                    else if (state == 0) controller.TurnOff(false, false);
                }
                catch (Exception e) { Log.Write("error: " + e.Message); }
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_POWERBROADCAST)
            {
                int ev = m.WParam.ToInt32();
                if (ev == PBT_POWERSETTINGCHANGE)
                {
                    // POWERBROADCAST_SETTING: GUID, DWORD length, then the data (0 off, 1 on, 2 dimmed)
                    Rules.Display(Marshal.ReadInt32(m.LParam, 20));
                }
                else if (ev == PBT_APMSUSPEND)
                {
                    Log.Write("PC going to sleep");
                    Rules.Resumed(); // whatever comes after waking counts as the monitors coming on
                    QuickOff();
                }
                m.Result = (IntPtr)1;
                return;
            }
            if (m.Msg == WM_QUERYENDSESSION) { m.Result = (IntPtr)1; return; }
            if (m.Msg == WM_ENDSESSION && m.WParam != IntPtr.Zero)
            {
                Log.Write("PC shutting down");
                QuickOff();
            }
            base.WndProc(ref m);
        }

        void QuickOff()
        {
            if (Rules.Paused || !settings.OffWithMonitors) return;
            try { controller.TurnOff(false, true); }
            catch (Exception e) { Log.Write("error: " + e.Message); }
        }
    }
}
