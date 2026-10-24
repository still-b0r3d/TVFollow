using System;
using System.Collections.Generic;
using System.Threading;

namespace TVFollow
{
    enum TurnOnMode
    {
        Follow,    // the monitors came on: switch to the PC unless the TV is busy showing something else
        ToPc,      // a button: turn on and switch to the PC whatever it shows
        PowerOnly  // a button: just turn it on
    }

    class TvState
    {
        public bool Reachable;
        public string PowerMode = "";
        public string App = "";
        public bool IsOn { get { return Reachable && PowerMode == "PowerOn"; } }
    }

    // Turning the TV on and off, with the retries, wake-ups and searching that takes.
    class Controller
    {
        readonly Settings settings;
        readonly RokuClient tv;

        // waits in milliseconds; the tests shrink them
        public int FirstCheckMs = 1500;
        public int RetryMs = 2500;
        public int PollMs = 1500;
        public int ReachMs = 8000;       // after sleep the PC's own network takes a few seconds
        public int WakeReachMs = 20000;  // a TV woken from deep standby takes a while to answer
        public int SweepWaitMs = 2000;

        public Action<string> SendWake = Wake.Send;
        public Func<List<string>> Candidates; // where to look if the TV moved

        public Controller(Settings settings)
        {
            this.settings = settings;
            tv = new RokuClient(settings.Ip, settings.Port);
            Candidates = () => Discovery.Sweep(this.settings.Port, SweepWaitMs);
        }

        public void TurnOn(TurnOnMode mode)
        {
            string tag = mode == TurnOnMode.Follow ? "on" : mode == TurnOnMode.ToPc ? "pc" : "on (button)";
            TvState s = WaitReachable(ReachMs);
            if (!s.Reachable)
            {
                Log.Write(tag + ": TV not answering, sending a wake packet");
                SendWake(settings.WakeMac);
                s = WaitReachable(WakeReachMs);
                if (s.Reachable) Log.Write(tag + ": TV woke up (" + s.PowerMode + ")");
            }
            if (!s.Reachable && Rediscover()) s = Query();
            if (!s.Reachable) { Log.Write(tag + ": TV not found"); return; }

            if (s.IsOn)
            {
                if (mode == TurnOnMode.PowerOnly) { Log.Write(tag + ": already on"); return; }
                if (s.App == settings.PcApp) { Log.Write(tag + ": already showing the PC"); return; }
                if (mode == TurnOnMode.Follow) { Log.Write(tag + ": TV is on and showing " + Describe(s.App) + ", leaving it alone"); return; }
            }
            else
            {
                int code = tv.Keypress("PowerOn", 4000);
                Log.Write(tag + ": power on (was " + s.PowerMode + ") -> " + code);
                if (mode == TurnOnMode.PowerOnly) return;
            }

            for (int i = 0; i < 8; i++)
            {
                Thread.Sleep(i == 0 ? FirstCheckMs : RetryMs);
                TvState now = Query();
                if (now.IsOn && now.App == settings.PcApp) { Log.Write(tag + ": showing the PC"); return; }
                if (!now.IsOn) continue;
                int code = tv.Keypress("Input" + settings.Input.ToUpperInvariant(), 4000);
                if (code < 200 || code > 299) code = tv.Launch(settings.PcApp, 4000);
                if (i == 0) Log.Write(tag + ": switching to the PC -> " + code);
            }
            Log.Write(tag + ": gave up waiting for the PC input");
        }

        // force: turn it off whatever it shows (a button), else only if it shows the PC.
        // quick: sleep and shutdown only give us a moment, so short waits and no searching.
        public void TurnOff(bool force, bool quick)
        {
            string tag = force ? "off (button)" : "off";
            TvState s = Query(quick ? 800 : 3000);
            if (!s.Reachable && !quick && Rediscover()) s = Query();
            if (!s.Reachable) { Log.Write(tag + ": TV not answering"); return; }
            if (!s.IsOn) { Log.Write(tag + ": already off"); return; }
            if (!force && s.App != settings.PcApp) { Log.Write(tag + ": TV is showing " + Describe(s.App) + ", leaving it on"); return; }
            Log.Write(tag + ": sending power off"); // logged first: at shutdown the PC may stop before the reply
            Log.Write(tag + ": power off -> " + tv.Keypress("PowerOff", quick ? 800 : 4000));
        }

        TvState Query() { return Query(3000); }

        TvState Query(int timeoutMs)
        {
            var s = new TvState();
            DeviceInfo info = tv.GetDeviceInfo(timeoutMs);
            // the serial makes sure it's this TV and not some other Roku that took its address
            if (info == null || (settings.Serial.Length > 0 && info.Serial != settings.Serial)) return s;
            s.Reachable = true;
            s.PowerMode = info.PowerMode;
            s.App = tv.GetActiveApp(timeoutMs) ?? "";
            return s;
        }

        TvState WaitReachable(int ms)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(ms);
            while (true)
            {
                TvState s = Query();
                if (s.Reachable || DateTime.UtcNow > until) return s;
                Thread.Sleep(PollMs);
            }
        }

        // If the router handed the TV a new address, find it again by its serial number.
        bool Rediscover()
        {
            Log.Write("looking for the TV on the network");
            string ip = Discovery.FindBySerial(Candidates(), settings.Port, settings.Serial);
            if (ip == null) { Log.Write("TV not found on the network"); return false; }
            if (ip != settings.Ip)
            {
                Log.Write("TV found at " + ip);
                settings.Ip = ip;
                tv.Ip = ip;
                try { settings.Save(); }
                catch (Exception e) { Log.Write("couldn't save the new address: " + e.Message); }
            }
            return true;
        }

        static string Describe(string app)
        {
            return app.Length == 0 ? "the Roku home screen" : app;
        }
    }
}
