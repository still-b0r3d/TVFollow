using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

    // Turning one TV on and off, with the retries, wake-ups and searching that takes. Each TV
    // that follows the monitors has a controller of its own.
    class Controller
    {
        readonly Settings settings;
        readonly TvSettings tv;      // this TV's part of the settings
        readonly RokuClient client;
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
        public int PressSearchMs = 20000;  // how long a pressed button may search behind a router TVFollow doesn't know

        public Func<string, int> SendWake = Wake.Send;  // returns how many packets went out
        public Func<bool> OnTvNetwork;                  // is the PC on the network the TV was last seen on?
        public Func<List<string>> Candidates;           // where to look if the TV moved
        public Func<string, string> GatewayMacFor = Discovery.GatewayMacFor;  // the router of the network an address is on
        public Func<bool> SameNumbers;                  // is the TV's last address in one of the PC's address ranges?

        public Controller(Settings settings, TvSettings tv)
        {
            this.settings = settings;
            this.tv = tv;
            client = new RokuClient(tv.Ip, settings.Port);
            SameNumbers = () => Discovery.IsLocal(this.tv.Ip);
            // the same address range isn't the same network: the router has to match too, once known
            OnTvNetwork = () => SameNumbers() &&
                Discovery.OnNetworkOf(true, this.tv.GatewayMac, GatewayMacFor(this.tv.Ip));
            Candidates = () => Discovery.SweepAround(this.tv.Ip, this.settings.Port, SweepWaitMs);
        }

        // One controller for each TV in the settings, in their order.
        public static List<Controller> For(Settings settings)
        {
            return settings.Tvs.ConvertAll(t => new Controller(settings, t));
        }

        public TvSettings Tv { get { return tv; } }

        // what the menus and the log call this TV
        public string Label { get { return settings.LabelOf(tv); } }

        // A log line; with several TVs it starts with the TV's name, so each line says which TV.
        public void Say(string text)
        {
            Log.Write(settings.SeveralTvs ? Label + ": " + text : text);
        }

        // Does the same to each TV at once, each on a thread of its own, and waits for them all,
        // so one slow TV (waking from deep standby, say) doesn't hold up the others. False if
        // any of them threw; that's in the log.
        public static bool Each(List<Controller> tvs, string what, Action<Controller> action)
        {
            bool ok = true;
            Task[] runs = tvs.ConvertAll(c => Task.Run(() =>
            {
                try { action(c); }
                catch (Exception e) { ok = false; c.Say(what + ": error: " + e.Message); }
            })).ToArray();
            Task.WaitAll(runs);
            return ok;
        }

        // The follower's worker, the tray's TV buttons and the remote's TV to PC button share this
        // controller from their own threads. Two sequences at once would press buttons in each
        // other's way (and both search for the TV at once), so they take turns: a button pressed
        // during a running sequence waits for it to finish. Only these sequences, in this process,
        // take turns: the remote's other buttons (power, arrows, inputs, typing) and the Settings
        // window's "Show it on the TV" talk to the TV directly, and the desktop shortcuts run their
        // own TVFollow.exe. Each TV takes turns on its own; different TVs never wait for each other.
        public void TurnOn(TurnOnMode mode)
        {
            lock (gate) TurnOnNow(mode);
        }

        void TurnOnNow(TurnOnMode mode)
        {
            string tag = mode == TurnOnMode.Follow ? "on" : mode == TurnOnMode.ToPc ? "pc" : "on (button)";
            TvState s = WaitReachable(ReachMs);
            bool pressed = false;  // a pressed button's search behind a router TVFollow doesn't know
            if (!s.Reachable)
            {
                if (OnTvNetwork()) s = WakeUp(tag);
                else if (mode != TurnOnMode.Follow && SameNumbers()) { LookingBehindOtherRouter(tag); pressed = true; }
                else
                {
                    // a laptop away from home, say: don't wake or search anything on this network
                    Say(tag + ": this PC isn't on the TV's network, leaving it alone");
                    return;
                }
            }
            if (!s.Reachable && Rediscover(pressed)) s = Query();
            if (!s.Reachable) { Say(tag + ": TV not found"); return; }

            if (s.IsOn)
            {
                if (mode == TurnOnMode.PowerOnly) { Say(tag + ": already on"); return; }
                if (s.App == tv.PcApp) { Say(tag + ": already showing the PC"); return; }
                if (mode == TurnOnMode.Follow) { Say(tag + ": TV is on and showing " + Describe(s.App) + ", leaving it alone"); return; }
            }
            else
            {
                int code = client.Keypress("PowerOn", 4000);
                Say(tag + ": power on (was " + s.PowerMode + ") -> " + code);
                if (mode == TurnOnMode.PowerOnly) return;
            }

            for (int i = 0; i < 8; i++)
            {
                // a TV that was already on can take the input press at once; one just turned on
                // needs a moment first
                if (i > 0 || !s.IsOn) Thread.Sleep(i == 0 ? FirstCheckMs : RetryMs);
                TvState now = Query();
                if (now.IsOn && now.App == tv.PcApp) { Say(tag + ": showing the PC"); return; }
                if (!now.IsOn) continue;
                int code = client.Keypress("Input" + tv.Input.ToUpperInvariant(), 4000);
                if (code < 200 || code > 299) code = client.Launch(tv.PcApp, 4000);
                if (i == 0) Say(tag + ": switching to the PC -> " + code);
            }
            Say(tag + ": gave up waiting for the PC input");
        }

        // Someone pressed a TV button (not the automatic following) on a network that has the TV's
        // addresses, but the router the TV was last seen behind didn't match or didn't answer.
        // After a router swap the TV may have a new address, so the search by serial number goes
        // ahead; no wake packet, since this may not be home. Finding the TV teaches TVFollow the
        // new router.
        void LookingBehindOtherRouter(string tag)
        {
            Say(tag + ": the home router didn't match or didn't answer; looking for the TV by its serial number");
        }

        // The wake packet, sent again every few seconds while waiting for the TV to answer.
        TvState WakeUp(string tag)
        {
            int sent = SendWake(tv.WakeMac);
            Say(sent > 0
                ? tag + ": TV not answering, sent a wake packet (" + sent + " addresses)"
                : tag + ": TV not answering, and there was no network to send a wake packet on");
            DateTime until = DateTime.UtcNow.AddMilliseconds(WakeReachMs);
            DateTime nextSend = DateTime.UtcNow.AddMilliseconds(WakeRepeatMs);
            while (true)
            {
                TvState s = Query();
                if (s.Reachable) { Say(tag + ": TV woke up (" + s.PowerMode + ")"); return s; }
                if (DateTime.UtcNow > until) return s;
                if (DateTime.UtcNow >= nextSend)
                {
                    SendWake(tv.WakeMac);
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
                bool pressed = false;
                if (!OnTvNetwork())
                {
                    if (!force || !SameNumbers()) { Say(tag + ": this PC isn't on the TV's network"); return; }
                    LookingBehindOtherRouter(tag);
                    pressed = true;
                }
                if (Rediscover(pressed)) s = Query();
            }
            if (!s.Reachable) { Say(tag + ": TV not answering"); return; }
            if (!s.IsOn) { Say(tag + ": already off"); return; }
            if (!force && s.App != tv.PcApp) { Say(tag + ": TV is showing " + Describe(s.App) + ", leaving it on"); return; }
            Say(tag + ": sending power off"); // logged first: at shutdown the PC may stop before the reply
            Say(tag + ": power off -> " + client.Keypress("PowerOff", quick ? 800 : 4000));
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
            string gateway = GatewayMacFor(tv.Ip);
            if (gateway.Length == 0 || string.Equals(gateway, tv.GatewayMac, StringComparison.OrdinalIgnoreCase)) return;
            tv.GatewayMac = gateway;
            Say("remembered the TV's network");
            Save();
        }

        TvState Query(int timeoutMs)
        {
            var s = new TvState();
            DeviceInfo info = client.GetDeviceInfo(timeoutMs);
            // the serial makes sure it's this TV and not some other Roku that took its address
            if (info == null || (tv.Serial.Length > 0 && info.Serial != tv.Serial)) return s;
            s.Reachable = true;
            s.PowerMode = info.PowerMode;
            s.App = client.GetActiveApp(timeoutMs) ?? "";
            // only an address that reads as one replaces the saved one: the reply is the TV's word
            if (Wake.ParseMac(info.WakeMac) != null && !string.Equals(info.WakeMac, tv.WakeMac, StringComparison.OrdinalIgnoreCase))
            {
                // moved from Wi-Fi to a cable, or the other way round
                tv.WakeMac = info.WakeMac;
                Say("the TV's network connection changed; its wake address is updated");
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

        // If the router handed the TV a new address, find it again by its serial number, at most
        // once every ten minutes. pressed: a button pressed behind a router TVFollow doesn't know
        // (after a router swap, say). Each such press searches once, so a press that came while
        // the TV was off doesn't hold up the next one, but only for PressSearchMs, since this may
        // be a stranger's network.
        bool Rediscover(bool pressed)
        {
            if (!pressed && DateTime.UtcNow - lastSearch < SearchEvery)
            {
                Say("looked for the TV a few minutes ago; not searching again yet");
                return false;
            }
            lastSearch = DateTime.UtcNow;
            Say("looking for the TV on its network");
            string ip = Discovery.FindBySerial(Candidates(), settings.Port, tv.Serial, pressed ? PressSearchMs : Timeout.Infinite);
            if (ip == null) { Say("TV not found on the network"); return false; }
            if (ip != tv.Ip)
            {
                Say("TV found at " + ip);
                tv.Ip = ip;
                client.Ip = ip;
                Save();
            }
            return true;
        }

        void Save()
        {
            try { settings.Save(); }
            catch (Exception e) { Say("couldn't save the settings: " + e.Message); }
        }

        static string Describe(string app)
        {
            return app.Length == 0 ? "the Roku home screen" : app;
        }
    }
}
