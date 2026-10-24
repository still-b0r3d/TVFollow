using System;
using System.Threading;

namespace TVFollow
{
    // The setup window's test: turn the TV off, check this PC can still reach it, then turn it
    // back on showing this PC. The same steps TVFollow takes every day, done once while you watch.
    class PowerTest
    {
        // waits in milliseconds; the tests shrink them
        public int OffWaitMs = 10000;
        public int StandbyCheckMs = 5000;
        public int WakeWaitMs = 25000;
        public int OnWaitMs = 15000;
        public int PollMs = 1000;

        public Action<string> SendWake = Wake.Send;

        // report(ok, text): ok is null while working on something, true when a step worked,
        // false for the problem that stopped the test.
        public bool Run(RokuClient tv, string input, string wakeMac, Action<bool?, string> report)
        {
            string pcApp = "tvinput." + input;

            report(null, "Turning the TV off…");
            int code = tv.Keypress("PowerOff", 4000);
            if (code == 403) { report(false, "The TV said no. Its Network access setting is still on Limited."); return false; }
            if (code < 200 || code > 299) { report(false, "The TV didn't answer."); return false; }
            // off, or gone quiet: some TVs drop off the network the moment they're off
            if (!WaitFor(() => { DeviceInfo i = tv.GetDeviceInfo(2000); return i == null || !i.IsOn; }, OffWaitMs))
            {
                report(false, "The TV didn't turn off.");
                return false;
            }
            report(true, "The TV turned off.");

            Thread.Sleep(StandbyCheckMs);
            if (tv.GetDeviceInfo(3000) != null)
            {
                report(true, "It still answers while it's off, so this PC can turn it back on.");
            }
            else
            {
                report(null, "It stopped answering while it's off. Trying the network wake signal…");
                SendWake(wakeMac);
                if (!WaitFor(() => tv.GetDeviceInfo(2000) != null, WakeWaitMs))
                {
                    report(false, "This PC can't reach the TV while it's off. On the TV, turn on Fast TV start " +
                        "(Settings → System → Power → Fast TV start), turn the TV back on with the remote, and test again.");
                    return false;
                }
                report(true, "The wake signal reached it.");
            }

            DeviceInfo info = tv.GetDeviceInfo(3000);
            if (info == null || !info.IsOn)
            {
                report(null, "Turning it back on…");
                tv.Keypress("PowerOn", 4000);
                if (!WaitFor(() => { DeviceInfo i = tv.GetDeviceInfo(2000); return i != null && i.IsOn; }, OnWaitMs))
                {
                    report(false, "The TV didn't turn back on. Turn it on with the remote and test again.");
                    return false;
                }
            }

            report(null, "Switching it to this PC…");
            bool shown = WaitFor(() =>
            {
                if (tv.GetActiveApp(2000) == pcApp) return true;
                int c = tv.Keypress("Input" + input.ToUpperInvariant(), 4000);
                if (c < 200 || c > 299) tv.Launch(pcApp, 4000);
                return false;
            }, OnWaitMs);
            if (!shown)
            {
                report(false, "It came back on but didn't switch to the input you picked.");
                return false;
            }
            report(true, "Back on and showing this PC.");
            return true;
        }

        bool WaitFor(Func<bool> done, int ms)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(ms);
            while (true)
            {
                if (done()) return true;
                if (DateTime.UtcNow > until) return false;
                Thread.Sleep(PollMs);
            }
        }
    }
}
