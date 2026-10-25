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
        DateTime lastSearch = DateTime.MinValue;
        readonly object gate = new object();  // one sequence at a time, see TurnOn

        // waits in milliseconds; the tests shrink them
        public int FirstCheckMs = 1500;
        public int RetryMs = 2500;
        public int PollMs = 1500;
        public int ReachMs = 8000;        // after sleep the PC's own network takes a few seconds
        public int WakeReachMs = 20000;   // a TV woken from deep standby takes a while to answer
        public int WakeRepeatMs = 3000;   // the wake packet is sent again this often while waiting
        public int SweepWaitMs = 2000;
        public TimeSpan SearchEvery = TimeSpan.FromMinutes(10);

        public Func<string, int> SendWake = Wake.Send;  // returns how many packets went out
        public Func<bool> OnTvNetwork;                  // is the PC on the network the TV was last seen on?
        public Func<List<string>> Candidates;           // where to look if the TV moved
        public Func<string, string> GatewayMacFor = Discovery.GatewayMacFor;  // the router of the network an address is on

        public Controller(Settings settings)
        {
            this.settings = settings;
            tv = new RokuClient(settings.Ip, settings.Port);
            // the same address range isn't the same network: the router has to match too, once known
            OnTvNetwork = () => Discovery.IsLocal(this.settings.Ip) &&
                Discovery.OnNetworkOf(true, this.settings.GatewayMac, GatewayMacFor(this.settings.Ip));
            Candidates = () => Discovery.SweepAround(this.settings.Ip, this.settings.Port, SweepWaitMs);
        }

        // The follower's worker, the tray's TV buttons and the remote's TV to PC button share this
        // controller from their own threads. Two sequences at once would press buttons in each
        // other's way (and both search for the TV at once), so they take turns: a button pressed
        // during a running sequence waits for it to finish. Only these sequences, in this process,
        // take turns: the remote's other buttons (power, arrows, inputs, typing) and the Settings
        // window's "Show it on the TV" talk to the TV directly, and the desktop shortcuts run their
        // own TVFollow.exe.
        public void TurnOn(TurnOnMode mode)
        {
            lock (gate) TurnOnNow(mode);
        }

        void TurnOnNow(TurnOnMode mode)
        {
            string tag = mode == TurnOnMode.Follow ? "on" : mode == TurnOnMode.ToPc ? "pc" : "on (button)";
            TvState s = WaitReachable(ReachMs);
            if (!s.Reachable)
            {
                if (!OnTvNetwork())
                {
                    // a laptop away from home, say: don't wake or search anything on this network
                    Log.Write(tag + ": this PC isn't on the TV's network, leaving it alone");
                    return;
                }
                s = WakeUp(tag);
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
                // a TV that was already on can take the input press at once; one just turned on
                // needs a moment first
                if (i > 0 || !s.IsOn) Thread.Sleep(i == 0 ? FirstCheckMs : RetryMs);
                TvState now = Query();
                if (now.IsOn && now.App == settings.PcApp) { Log.Write(tag + ": showing the PC"); return; }
                if (!now.IsOn) continue;
                int code = tv.Keypress("Input" + settings.Input.ToUpperInvariant(), 4000);
                if (code < 200 || code > 299) code = tv.Launch(settings.PcApp, 4000);
                if (i == 0) Log.Write(tag + ": switching to the PC -> " + code);
            }
            Log.Write(tag + ": gave up waiting for the PC input");
        }

        // The wake packet, sent again every few seconds while waiting for the TV to answer.
        TvState WakeUp(string tag)
        {
            int sent = SendWake(settings.WakeMac);
            Log.Write(sent > 0
                ? tag + ": TV not answering, sent a wake packet (" + sent + " addresses)"
                : tag + ": TV not answering, and there was no network to send a wake packet on");
            DateTime until = DateTime.UtcNow.AddMilliseconds(WakeReachMs);
            DateTime nextSend = DateTime.UtcNow.AddMilliseconds(WakeRepeatMs);
            while (true)
            {
                TvState s = Query();
                if (s.Reachable) { Log.Write(tag + ": TV woke up (" + s.PowerMode + ")"); return s; }
                if (DateTime.UtcNow > until) return s;
                if (DateTime.UtcNow >= nextSend)
                {
                    SendWake(settings.WakeMac);
                    nextSend = DateTime.UtcNow.AddMilliseconds(WakeRepeatMs);
                }
                Thread.Sleep(PollMs);
            }
        }

        // force: turn it off whatever it shows (a button), else only if it shows the PC.
        // quick: sleep and shutdown only give us a moment, so short waits and no searching.
        public void TurnOff(bool force, bool quick)
        {
            // quick runs at sleep and shutdown, with a moment to act in, so it doesn't wait behind
            // a running sequence (1.0 didn't either): waiting could outlast the moment Windows
            // gives. The two can cross either way. It can press power off in the middle of a
            // sequence, or a sequence that had already looked can press power on or the input just
            // after it, leaving the TV on as the PC sleeps. What they share is safe to share: every
            // request stands alone, and saving the settings takes turns.
            if (quick) { TurnOffNow(force, true); return; }
            lock (gate) TurnOffNow(force, false);
        }

        void TurnOffNow(bool force, bool quick)
        {
            string tag = force ? "off (button)" : "off";
            TvState s = quick ? Query(800) : Query();
            if (!s.Reachable && !quick)
            {
                if (!OnTvNetwork()) { Log.Write(tag + ": this PC isn't on the TV's network"); return; }
                if (Rediscover()) s = Query();
            }
            if (!s.Reachable) { Log.Write(tag + ": TV not answering"); return; }
            if (!s.IsOn) { Log.Write(tag + ": already off"); return; }
            if (!force && s.App != settings.PcApp) { Log.Write(tag + ": TV is showing " + Describe(s.App) + ", leaving it on"); return; }
            Log.Write(tag + ": sending power off"); // logged first: at shutdown the PC may stop before the reply
            Log.Write(tag + ": power off -> " + tv.Keypress("PowerOff", quick ? 800 : 4000));
        }

        // the usual query; the quick path at sleep and shutdown calls Query(800) directly, so it
        // never stops to look at the network
        TvState Query()
        {
            TvState s = Query(3000);
            if (s.Reachable) RememberNetwork();
            return s;
        }

        // The TV answered, so this is its network: remember the router, so that a laptop on
        // another network with the same addresses can be told apart later. A router that
        // doesn't answer right now doesn't make TVFollow forget the one it knows.
        void RememberNetwork()
        {
            string gateway = GatewayMacFor(settings.Ip);
            if (gateway.Length == 0 || string.Equals(gateway, settings.GatewayMac, StringComparison.OrdinalIgnoreCase)) return;
            settings.GatewayMac = gateway;
            Log.Write("remembered the TV's network");
            Save();
        }

        TvState Query(int timeoutMs)
        {
            var s = new TvState();
            DeviceInfo info = tv.GetDeviceInfo(timeoutMs);
            // the serial makes sure it's this TV and not some other Roku that took its address
            if (info == null || (settings.Serial.Length > 0 && info.Serial != settings.Serial)) return s;
            s.Reachable = true;
            s.PowerMode = info.PowerMode;
            s.App = tv.GetActiveApp(timeoutMs) ?? "";
            // only an address that reads as one replaces the saved one: the reply is the TV's word
            if (Wake.ParseMac(info.WakeMac) != null && !string.Equals(info.WakeMac, settings.WakeMac, StringComparison.OrdinalIgnoreCase))
            {
                // moved from Wi-Fi to a cable, or the other way round
                settings.WakeMac = info.WakeMac;
                Log.Write("the TV's network connection changed; its wake address is updated");
                Save();
            }
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

        // If the router handed the TV a new address, find it again by its serial number: only
        // on the TV's own network, and at most once every ten minutes.
        bool Rediscover()
        {
            if (DateTime.UtcNow - lastSearch < SearchEvery)
            {
                Log.Write("looked for the TV a few minutes ago; not searching again yet");
                return false;
            }
            lastSearch = DateTime.UtcNow;
            Log.Write("looking for the TV on its network");
            string ip = Discovery.FindBySerial(Candidates(), settings.Port, settings.Serial);
            if (ip == null) { Log.Write("TV not found on the network"); return false; }
            if (ip != settings.Ip)
            {
                Log.Write("TV found at " + ip);
                settings.Ip = ip;
                tv.Ip = ip;
                Save();
            }
            return true;
        }

        void Save()
        {
            try { settings.Save(); }
            catch (Exception e) { Log.Write("couldn't save the settings: " + e.Message); }
        }

        static string Describe(string app)
        {
            return app.Length == 0 ? "the Roku home screen" : app;
        }
    }
}
