using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

        // Windows dims the screens about 15 s before it switches them off. A screen saver can make
        // the dim last the whole screen timeout instead, with the screens already dark, so a dim
        // this long counts as the monitors going off, and the end of it as them coming back.
        public const double LongDimSec = 30;

        public Func<double> SecondsSinceInput = UserInput.SecondsSinceInput;
        public Func<uint> LastInputTick = UserInput.LastInputTick;
        public Func<uint> Clock = () => unchecked((uint)Environment.TickCount);  // ms, like the input ticks
        public Action<bool> Want = delegate { };  // true: TV on, false: TV off
        public bool OnWithMonitors = true;
        public bool OffWithMonitors = true;
        public bool Paused;

        bool waitingForInput;
        uint inputTickAtWake;
        int lastDisplay = -1;
        uint dimmedAt;
        bool longDim;  // the dim went on long enough to count as the monitors going off

        // Are the monitors dark: off, or dimmed long enough to count as off? The main window
        // doesn't ask the TVs how they are meanwhile. (Sleep leaves it as it was, unlike the
        // rules' own state: the screens say again when the PC wakes.)
        public bool MonitorsDark { get; private set; }
        public Action DarkChanged = delegate { };

        void Dark(bool value)
        {
            if (MonitorsDark == value) return;
            MonitorsDark = value;
            DarkChanged();
        }

        // Windows' display state: 0 off, 1 on, 2 dimmed. Coming back from a short dim isn't the
        // monitors coming back on (they never went off), so it's ignored; on after on still
        // counts, since sleep doesn't always send "off" first.
        public void Display(int state)
        {
            Decide(state);
            if (state == 0) Dark(true);
            else if (state == 1) Dark(false);
        }

        void Decide(int state)
        {
            int before = lastDisplay;
            lastDisplay = state;
            if (state == 2)
            {
                if (before == 2) return;
                dimmedAt = Clock();
                Log.Write("monitors dimmed");
                return;
            }
            bool wasLongDim = longDim;
            longDim = false;
            if (state == 0) MonitorsOff();
            else if (state == 1 && before == 2 && !wasLongDim) Log.Write("monitors back from dimmed");
            else if (state == 1) MonitorsOn();
        }

        // after sleep, the next "on" always counts
        public void Resumed() { lastDisplay = -1; longDim = false; }

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
            Off();
        }

        void Off()
        {
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

        // called about twice a second too
        public void CheckDim()
        {
            if (lastDisplay != 2 || longDim || unchecked(Clock() - dimmedAt) < LongDimSec * 1000) return;
            longDim = true;
            Log.Write("monitors dimmed for " + LongDimSec + "s, so they count as off");
            Off();
            Dark(true);
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
    // thread for each TV that does what the rules want, so the window never waits on a TV and
    // the TVs never wait on each other.
    class Follower : NativeWindow
    {
        const int WM_QUERYENDSESSION = 0x11, WM_ENDSESSION = 0x16, WM_POWERBROADCAST = 0x218, WM_WTSSESSION_CHANGE = 0x2B1;
        const int PBT_APMSUSPEND = 0x4, PBT_POWERSETTINGCHANGE = 0x8013;
        const int WTS_SESSION_LOCK = 0x7, WTS_SESSION_UNLOCK = 0x8, NOTIFY_FOR_THIS_SESSION = 0;
        static Guid GUID_CONSOLE_DISPLAY_STATE = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int flags);

        public readonly FollowRules Rules = new FollowRules();

        public Settings Settings { get { return settings; } }

        // the TVs, in the settings' order
        public readonly List<Controller> Controllers;

        // how long sleep and shutdown wait for the TVs to be told, at most
        public int QuickOffWaitMs = 5000;

        readonly Settings settings;
        readonly List<Worker> workers;
        System.Windows.Forms.Timer tick;
        EventWaitHandle quit;

        public Follower(Settings settings, List<Controller> controllers)
        {
            this.settings = settings;
            Controllers = controllers;
            workers = controllers.ConvertAll(c => new Worker(c));
            Rules.OnWithMonitors = settings.OnWithMonitors;
            Rules.OffWithMonitors = settings.OffWithMonitors;
            Rules.Paused = settings.Paused;
            Rules.Want = Want;
            Rules.DarkChanged = () => ScreensChanged();
        }

        // ---- can anyone see the screens? Only then does the main window ask the TVs how they are ----

        bool locked;

        // the monitors are off (or dimmed long enough to count as off), or the PC is locked
        public bool NobodyLooking { get { return locked || Rules.MonitorsDark; } }

        // NobodyLooking may have changed, on the window's thread
        public event Action ScreensChanged = delegate { };

        // the PC locked or unlocked (Windows tells the hidden window; the tests call it)
        public void SessionLocked(bool value)
        {
            if (locked == value) return;
            locked = value;
            ScreensChanged();
        }

        public void Start()
        {
            StartWorkers();

            var cp = new CreateParams();
            cp.Caption = "TVFollow";
            CreateHandle(cp); // hidden top-level window, so it also hears sleep and shutdown
            // Windows answers right away with the current state, which turns the TV on at sign-in
            RegisterPowerSettingNotification(Handle, ref GUID_CONSOLE_DISPLAY_STATE, 0);
            // the PC being locked and unlocked: the following ignores it, the main window doesn't
            if (!WTSRegisterSessionNotification(Handle, NOTIFY_FOR_THIS_SESSION))
                Log.Write("couldn't listen for the PC being locked (error " + Marshal.GetLastWin32Error() + ")");
            Listen();
        }

        // Twice a second: the other copies' signals (a new copy taking over asks this one to
        // stop; TVFollow opened again asks it to show its window), and the input and the dim the
        // rules wait for. Start does this; the tests listen alone, and stop again.
        public void Listen()
        {
            quit = new EventWaitHandle(false, EventResetMode.AutoReset, Instance.QuitName);
            showWindow = new EventWaitHandle(false, EventResetMode.AutoReset, Instance.ShowName);
            tick = new System.Windows.Forms.Timer();
            tick.Interval = 500;
            tick.Tick += delegate
            {
                if (quit.WaitOne(0))
                {
                    Log.Write("stopping: a new copy with new settings is taking over");
                    // it has saved them: presses not written yet aren't written over them
                    SavedPresses.Forget();
                    tick.Stop();
                    Application.ExitThread();
                    return;
                }
                if (showWindow.WaitOne(0)) ShowRequested();
                Rules.CheckForInput();
                Rules.CheckDim();
            };
            tick.Start();
        }

        public void StopListening()
        {
            tick.Dispose();
            quit.Dispose();
            showWindow.Dispose();
        }

        // TVFollow was opened again while this copy runs (the tray shows the main window)
        public event Action ShowRequested = delegate { };
        EventWaitHandle showWindow;

        // the TVs' threads; Start starts them, and the tests start them alone to drive the rules
        // without the window
        public void StartWorkers()
        {
            foreach (Worker w in workers) w.Start();
        }

        void Want(bool on)
        {
            foreach (Worker w in workers) w.Want(on);
        }

        // One TV's thread: it does the latest thing the rules want for that TV, once the TV is
        // done with whatever it was doing.
        class Worker
        {
            readonly Controller controller;
            readonly AutoResetEvent kick = new AutoResetEvent(false);
            int wanted = -1;

            public Worker(Controller controller)
            {
                this.controller = controller;
                // the developer tools may take a while before an off: by then the monitors may want on
                controller.OffStillWanted = () => Thread.VolatileRead(ref wanted) == 0;
            }

            public void Start()
            {
                var thread = new Thread(Work);
                thread.IsBackground = true;
                thread.Start();
            }

            public void Want(bool on)
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
                    catch (Exception e) { controller.Say("error: " + e.Message); }
                }
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
                // the presses saved so far, the off just now included, before Windows ends TVFollow
                SavedPresses.Flush();
            }
            if (m.Msg == WM_WTSSESSION_CHANGE)
            {
                int ev = m.WParam.ToInt32();
                if (ev == WTS_SESSION_LOCK) SessionLocked(true);
                else if (ev == WTS_SESSION_UNLOCK) SessionLocked(false);
            }
            base.WndProc(ref m);
        }

        // Every TV at once: sleep and shutdown only give a moment, and each TV's quick off has
        // short waits of its own. The window waits for them (up to QuickOffWaitMs), or Windows
        // could stop the PC before the requests go out.
        public void QuickOff()
        {
            if (Rules.Paused || !settings.OffWithMonitors) return;
            Task[] offs = Controllers.ConvertAll(c => Task.Run(() =>
            {
                try { c.TurnOff(false, true); }
                catch (Exception e) { c.Say("error: " + e.Message); }
            })).ToArray();
            Task.WaitAll(offs, QuickOffWaitMs);
        }
    }
}
