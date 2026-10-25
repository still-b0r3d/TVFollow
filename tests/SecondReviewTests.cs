using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow.Tests
{
    // Tests for the fixes from the 2026-10-24 review (the second one).
    static partial class TestMain
    {
        static void RunSecondReview()
        {
            Run("a redirect from the TV is not followed", RedirectIsNotFollowed);
            Run("the TV's network: the same numbers aren't the same network", NetworkGate);
            Run("the TV's router is remembered, kept, and never asked for at sleep", GatewayMacIsRemembered);
            Run("after a router swap a TV button finds the moved TV; the automatic following doesn't look", RouterSwapButtonFindsTv);
            Run("a pressed button's search behind another router stops after a while", PressedSearchIsCapped);
            Run("remote: an emoji is one letter, half of one throws", RemoteEmojiLetter);
            Run("remote: an emoji typed in halves is one press, a stray half is nothing", RemoteEmojiTyped);
            Run("remote: every typed character reaches the TV as typed, . / and \\ too", EveryCharacterArrivesAsTyped);
            Run("remote: pasted text becomes presses, first line only", LettersFromText);
            Run("remote: a paste reaches the TV in order, a busy clipboard sends nothing", RemotePasteIsSent);
            Run("settings: a held file is waited for, then reported, never a crash", SettingsFileHeld);
            Run("settings: an address that isn't one is dropped", SettingsBadAddress);
            Run("settings: a reply from the TV can't add lines to settings.ini", SettingsStayOneLinePerValue);
            Run("controller: two callers at once take turns", ControllerIsSerialised);
            Run("controller: the quick off at sleep doesn't wait its turn", QuickOffDoesNotWait);
            Run("on: a TV that's already on gets the input press at once", InputPressedAtOnceWhenOn);
        }

        // ---- the controller ----

        static void ControllerIsSerialised()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.PowerMode = "DisplayOff";
                tv.DelayMs = 200;
                Controller c = Fast(new Controller(SettingsFor(tv)));
                Exception failure = null;
                ThreadStart turnOn = () => { try { c.TurnOn(TurnOnMode.ToPc); } catch (Exception e) { failure = e; } };
                var first = new Thread(turnOn) { IsBackground = true };  // a stuck one mustn't keep the test run alive
                var second = new Thread(turnOn) { IsBackground = true };
                first.Start();
                second.Start();
                Check(first.Join(30000) && second.Join(30000), "a TurnOn got stuck");
                Check(failure == null, "TurnOn threw: " + failure);
                Check(tv.Count("POST /keypress/PowerOn") == 1, "power on pressed " + tv.Count("POST /keypress/PowerOn") + " times");
                Check(tv.Presses == 1 && tv.IsOn, "presses: " + tv.Presses);
                Check(LogText().Contains("already showing the PC"), "the second caller should have found the TV on and showing the PC");
            }
        }

        static void QuickOffDoesNotWait()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.App = "";           // the Roku home screen
                tv.IgnoreInput = true; // so the sequence keeps trying for a while
                tv.DelayMs = 100;
                Controller c = Fast(new Controller(SettingsFor(tv)));
                c.RetryMs = 300;
                var busy = new Thread(() => { try { c.TurnOn(TurnOnMode.ToPc); } catch { } }) { IsBackground = true };
                busy.Start();
                // the sequence asks the TV only from inside its turn, so its first request means it
                // holds the turn; a fixed sleep could let the quick off go first on a slow machine
                var started = Stopwatch.StartNew();
                while (tv.Requests().Count == 0 && started.ElapsedMilliseconds < 5000) Thread.Sleep(10);
                Check(tv.Requests().Count > 0, "the sequence never started");
                var watch = Stopwatch.StartNew();
                c.TurnOff(false, true);
                watch.Stop();
                Check(watch.ElapsedMilliseconds < 1500, "the quick off waited " + watch.ElapsedMilliseconds + " ms behind a running sequence");
                Check(LogText().Contains("leaving it on"), "the quick off should have looked at the TV");
                tv.IgnoreInput = false; // let the sequence finish at its next try
                Check(busy.Join(30000), "the sequence got stuck");
            }
        }

        static void InputPressedAtOnceWhenOn()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                tv.App = "12"; // on, watching some channel
                Controller c = Fast(new Controller(SettingsFor(tv)));
                c.FirstCheckMs = 5000; // would be waited out before the first press if the TV had been off
                var watch = Stopwatch.StartNew();
                c.TurnOn(TurnOnMode.ToPc);
                watch.Stop();
                Check(tv.App == "tvinput.hdmi1", "the input didn't switch");
                Check(watch.ElapsedMilliseconds < 2500, "waited " + watch.ElapsedMilliseconds + " ms to press the input on a TV that was already on");
                // a TV that was off still gets its moment after power on
                tv.PowerMode = "DisplayOff";
                tv.App = "12";
                c.FirstCheckMs = 400;
                watch = Stopwatch.StartNew();
                c.TurnOn(TurnOnMode.ToPc);
                watch.Stop();
                Check(tv.App == "tvinput.hdmi1" && watch.ElapsedMilliseconds >= 350, "a TV just turned on should get a moment before the input press: " + watch.ElapsedMilliseconds + " ms");
            }
        }

        // ---- settings ----

        static void SettingsFileHeld()
        {
            var s = new Settings();
            s.Ip = "192.168.1.50";
            s.Serial = "X000000001";
            s.Save();
            int retryMs = Settings.ReadRetryMs, retries = Settings.ReadRetries;
            try
            {
                Settings.ReadRetryMs = 50;
                Settings.ReadRetries = 60; // three seconds, plenty for the moment below
                // held for a moment, then let go: the settings come through
                var hold = new FileStream(Settings.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                var release = new Thread(() => { Thread.Sleep(300); hold.Dispose(); });
                release.Start();
                Settings loaded;
                try { loaded = Settings.Load(); }
                finally { release.Join(); } // let go of the file even if Load failed, or the next test can't use it
                Check(loaded.LoadError == null && loaded.Ip == "192.168.1.50", "a briefly held file should still load: " + loaded.LoadError);

                // held for good: no crash, and not "not set up" either
                Settings.ReadRetries = 4;
                using (new FileStream(Settings.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Settings held = Settings.Load();
                    Check(held.LoadError != null && !held.IsComplete, "a held file should read as unreadable");
                }
                Check(LogText().Contains("couldn't read settings.ini"), "not logged");
            }
            finally
            {
                Settings.ReadRetryMs = retryMs;
                Settings.ReadRetries = retries;
                File.Delete(Settings.FilePath);
            }
        }

        static void SettingsBadAddress()
        {
            Directory.CreateDirectory(Settings.Dir);
            // an empty line is what an unfinished setup leaves, not junk: no log line for it
            File.WriteAllText(Settings.FilePath, "ip=\r\n");
            Check(Settings.Load().Ip == "" && !LogText().Contains("isn't an IPv4"), "an empty address isn't junk");
            foreach (string bad in new[] { "example.com/x?", "fe80::1", "192.168.1.50:8060", "my.tv.home" })
            {
                File.WriteAllText(Settings.FilePath, "ip=" + bad + "\r\nserial=X000000001\r\n");
                Settings s = Settings.Load();
                Check(s.Ip == "" && !s.IsComplete, "a bad address was kept: [" + s.Ip + "]");
                Check(LogText().Contains("ip=" + bad), "the bad address wasn't logged: " + bad);
            }
            foreach (string good in new[] { "192.168.1.50", "10.0.0.7", "0x7F.0.0.1" })
            {
                File.WriteAllText(Settings.FilePath, "ip=" + good + "   # the TV\r\nserial=X000000001\r\n");
                Settings s = Settings.Load();
                Check(s.Ip == good && s.IsComplete, "a good address was dropped: " + good + " -> [" + s.Ip + "]");
            }
            File.Delete(Settings.FilePath);
        }

        static void SettingsStayOneLinePerValue()
        {
            // a value with line breaks in it (here a wake address) is saved as one line, so it
            // can't set other keys when the file is read back
            var s = new Settings();
            s.Ip = "192.168.1.50";
            s.Serial = "X000000001";
            s.WakeMac = "aa:bb:cc:dd:ee:ff\r\nip=203.0.113.9\nserial=EVIL\rport=9\npaused=yes";
            s.Save();
            Settings back = Settings.Load();
            Check(back.Ip == "192.168.1.50" && back.Serial == "X000000001", "a line break in a value rewrote the TV: " + back.Ip + " " + back.Serial);
            Check(back.Port == Settings.DefaultPort && !back.Paused, "a line break in a value set port or paused");

            // and a TV whose answer carries one never gets it saved in the first place
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings t = SettingsFor(tv);
                t.Save();
                tv.RawDeviceInfo = "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n<device-info>\n" +
                    "\t<serial-number>" + tv.Serial + "</serial-number>\n\t<network-type>wifi</network-type>\n" +
                    "\t<wifi-mac>00:11:22:33:44:77&#10;ip=203.0.113.9&#10;serial=EVIL&#10;paused=yes</wifi-mac>\n" +
                    "\t<power-mode>PowerOn</power-mode>\n</device-info>";
                Fast(new Controller(t)).TurnOff(false, false);
                Check(t.WakeMac == "00:11:22:33:44:55", "a wake address that isn't one was taken from the TV: " + t.WakeMac);
                Settings loaded = Settings.Load();
                Check(loaded.Ip == tv.Ip && loaded.Serial == tv.Serial && !loaded.Paused, "the TV's answer changed the saved settings");
                Check(tv.Count("POST /keypress/PowerOff") == 1, "the TV should still have been turned off");
            }
            File.Delete(Settings.FilePath);
        }

        // ---- the remote's typing box ----

        static void RemoteEmojiLetter()
        {
            Check(RemoteKeys.Letter("\U0001F600") == "Lit_%F0%9F%98%80", "an emoji (a surrogate pair) should be one letter: " + RemoteKeys.Letter("\U0001F600"));
            Check(RemoteKeys.Letter("a") == "Lit_a" && RemoteKeys.Letter('a') == "Lit_a", "plain letters");
            bool threw = false;
            try { RemoteKeys.Letter("\uD83D"); }
            catch (UriFormatException) { threw = true; }
            Check(threw, "half a pair should throw in the escaping; that is why the typing box pairs them up");
        }

        static void RemoteEmojiTyped()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                RemoteForm form = OffScreenRemote(SettingsFor(tv), RemoteForm.Classic);
                try
                {
                    foreach (char c in new[] { '\uD83D', '\uDE00', '\uDE00', 'a' }) TypeInto(form.Typing, c);
                    Pump(() => tv.Count("POST /keypress/Lit_a") == 1, 10);
                    Check(tv.Count("POST /keypress/Lit_%F0%9F%98%80") == 1, "the emoji should be one press");
                    Check(tv.Presses == 2, "presses: " + tv.Presses + " (a stray half got sent?)");
                }
                finally { form.Close(); form.Dispose(); }
            }
        }

        // .NET's 4.0 compatibility mode "cleaned up" the address on the way out: Lit_. lost its dot
        // and Lit_%2F / Lit_%5C became Lit_/. Every printable character goes through the real client
        // here and has to arrive exactly as escaped.
        static void EveryCharacterArrivesAsTyped()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                var client = new RokuClient(tv.Ip, tv.Port);
                var typed = new List<string>();
                for (char c = ' '; c <= '~'; c++) typed.Add(RemoteKeys.Letter(c));
                typed.Add(RemoteKeys.Letter("\U0001F600"));
                var wrong = new List<string>();
                foreach (string key in typed)
                {
                    int code = client.Keypress(key, 3000);
                    if (code != 200 || tv.Count("POST /keypress/" + key) != 1) wrong.Add(key + " (" + code + ")");
                }
                List<string> expected = typed.ConvertAll(key => "POST /keypress/" + key);
                Check(wrong.Count == 0, "not sent as typed: " + string.Join(" ", wrong) +
                    "; the TV saw instead: " + string.Join(" ", tv.Requests().FindAll(r => !expected.Contains(r))));
            }
        }

        static void LettersFromText()
        {
            List<string> presses = RemoteKeys.Letters("hi");
            Check(presses.Count == 2 && presses[0] == "Lit_h" && presses[1] == "Lit_i", "hi: " + string.Join(",", presses));
            presses = RemoteKeys.Letters("a\U0001F600b");
            Check(presses.Count == 3 && presses[1] == "Lit_%F0%9F%98%80", "an emoji in the middle stays one press: " + string.Join(",", presses));
            Check(RemoteKeys.Letters("\uD83Dx\uDE00").Count == 1, "stray halves of a pair are dropped");
            Check(RemoteKeys.Letters("a\tb\u0001c\u007F").Count == 3, "control characters are dropped");
            Check(RemoteKeys.Letters("one\r\ntwo").Count == 3 && RemoteKeys.Letters("\nx").Count == 0, "only the first line is sent");
            Check(RemoteKeys.Letters("").Count == 0, "nothing from nothing");
        }

        static void RemotePasteIsSent()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                RemoteForm form = OffScreenRemote(SettingsFor(tv), RemoteForm.Classic);
                try
                {
                    var box = (PasteBox)form.Typing;
                    box.ReadClipboard = () => "hi";  // never the real clipboard: it belongs to whoever runs the tests
                    box.Paste();
                    Pump(() => tv.Count("POST /keypress/Lit_i") == 1, 10);
                    List<string> presses = tv.Requests().FindAll(r => r.StartsWith("POST "));
                    Check(presses.Count == 2 && presses[0] == "POST /keypress/Lit_h" && presses[1] == "POST /keypress/Lit_i", "presses: " + string.Join(", ", presses));

                    box.ReadClipboard = () => { throw new System.Runtime.InteropServices.ExternalException("the clipboard is busy"); };
                    box.Paste(); // must not throw (Run fails the test if the window did) and must not send
                    // presses go out in order, so once a letter typed afterwards has arrived, anything
                    // the busy paste had sent would have arrived before it
                    TypeInto(box, 'z');
                    Pump(() => tv.Count("POST /keypress/Lit_z") == 1, 10);
                    Check(tv.Presses == 3, "a busy clipboard should send nothing: " + tv.Presses + " presses");
                }
                finally { form.Close(); form.Dispose(); }
            }
        }

        // a key press the way Windows delivers one, without a keyboard
        static void TypeInto(TextBox box, char c)
        {
            try
            {
                typeof(Control).GetMethod("OnKeyPress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(box, new object[] { new KeyPressEventArgs(c) });
            }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }

        // ---- the TV's network ----

        static void NetworkGate()
        {
            Check(Discovery.OnNetworkOf(true, "aa:bb:cc:dd:ee:ff", "AA:BB:CC:DD:EE:FF"), "same numbers, same router");
            Check(!Discovery.OnNetworkOf(true, "aa:bb:cc:dd:ee:ff", "11:22:33:44:55:66"), "same numbers, another router");
            Check(Discovery.OnNetworkOf(true, "", "11:22:33:44:55:66"), "no router remembered yet: the numbers decide");
            Check(!Discovery.OnNetworkOf(true, "aa:bb:cc:dd:ee:ff", ""), "a router remembered, none found");
            Check(!Discovery.OnNetworkOf(false, "", ""), "other numbers");
            Check(!Discovery.OnNetworkOf(false, "aa:bb:cc:dd:ee:ff", "aa:bb:cc:dd:ee:ff"), "other numbers, even behind the same router");
            Check(Discovery.GatewayMacFor("127.0.0.1") == "", "loopback has no router");

            // the controller's own check: an address on none of the PC's networks is answered
            // without looking up any router (the other tests replace this check)
            var s = new Settings();
            s.Ip = "127.0.0.1";
            s.Serial = "X000000001";
            s.GatewayMac = "AA:BB:CC:DD:EE:FF";
            var c = new Controller(s);
            c.GatewayMacFor = ip => { throw new Exception("looked up a router for an address on none of the PC's networks"); };
            Check(!c.OnTvNetwork(), "loopback isn't the TV's network");
        }

        static void GatewayMacIsRemembered()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings s = SettingsFor(tv);
                s.Save();
                Controller c = Fast(new Controller(s));
                c.GatewayMacFor = ip => "AA:BB:CC:DD:EE:FF";
                c.TurnOff(false, false);
                Check(s.GatewayMac == "AA:BB:CC:DD:EE:FF", "router not remembered: [" + s.GatewayMac + "]");
                Check(Settings.Load().GatewayMac == "AA:BB:CC:DD:EE:FF", "router not saved");
                Check(LogText().Contains("remembered the TV's network") && !LogText().Contains("AA:BB"), "no log line, or the log shows the address");

                // a router that doesn't answer for a moment doesn't erase the one remembered
                c.GatewayMacFor = ip => "";
                tv.PowerMode = "PowerOn";
                c.TurnOff(false, false);
                Check(s.GatewayMac == "AA:BB:CC:DD:EE:FF", "an unanswered router lookup erased the remembered one");

                // sleep and shutdown have no time for the network: the quick path never asks
                c.GatewayMacFor = ip => { throw new Exception("asked for the router on the quick path"); };
                tv.PowerMode = "PowerOn";
                c.TurnOff(false, true);
                Check(tv.Count("POST /keypress/PowerOff") == 3, "power off pressed " + tv.Count("POST /keypress/PowerOff") + " times, expected 3");
                File.Delete(Settings.FilePath);
            }
        }

        // The router was replaced and the new one gave the TV a new address: the old address has
        // nothing on it, the TV answers at another one, and the router isn't the remembered one.
        static void RouterSwapButtonFindsTv()
        {
            using (var tv = new FakeTv("127.0.0.2", 0))
            {
                tv.App = "12"; // on, showing a channel
                int searches = 0;
                Func<Settings, Controller> afterSwap = s =>
                {
                    var c = new Controller(s);
                    Func<bool> realCheck = c.OnTvNetwork;   // home's numbers and home's router
                    Fast(c);                                // Fast: a wake packet would throw
                    c.OnTvNetwork = realCheck;
                    c.SameNumbers = () => true;             // home's address range...
                    c.GatewayMacFor = ip => "BB:BB:BB:BB:BB:BB"; // ...but not the router the TV was last seen behind
                    c.Candidates = () => { searches++; return new List<string> { "127.0.0.2" }; };
                    return c;
                };
                Func<Settings> moved = () =>
                {
                    Settings s = SettingsFor(tv);
                    s.Ip = "127.0.0.3"; // where the TV used to be
                    s.GatewayMac = "AA:AA:AA:AA:AA:AA";
                    s.Save();
                    return s;
                };

                // the automatic following leaves a network with another router alone
                Settings home = moved();
                Controller follow = afterSwap(home);
                follow.TurnOn(TurnOnMode.Follow);
                follow.TurnOff(false, false);
                Check(searches == 0 && tv.Presses == 0, "the automatic following looked for the TV behind another router");

                // a person pressing TV to PC gets the TV found, switched, and the new router learned
                Controller button = afterSwap(home);
                button.TurnOn(TurnOnMode.ToPc);
                Check(searches == 1, "TV to PC didn't look for the TV: " + searches + " searches");
                Check(home.Ip == "127.0.0.2" && Settings.Load().Ip == "127.0.0.2", "the TV's new address wasn't saved: " + home.Ip);
                Check(home.GatewayMac == "BB:BB:BB:BB:BB:BB" && Settings.Load().GatewayMac == "BB:BB:BB:BB:BB:BB", "the new router wasn't learned");
                Check(tv.App == "tvinput.hdmi1", "the TV wasn't switched to the PC");
                Check(LogText().Contains("looking for the TV by its serial number"), "the log doesn't say why it looked");

                // the tray's TV off does the same
                tv.App = "tvinput.hdmi1";
                Controller off = afterSwap(moved());
                off.TurnOff(true, false);
                Check(searches == 2 && tv.Count("POST /keypress/PowerOff") == 1, "TV off didn't find the moved TV");

                // a network with other numbers is left alone even for a button
                tv.PowerMode = "PowerOn";
                Controller away = afterSwap(moved());
                away.SameNumbers = () => false;
                away.TurnOn(TurnOnMode.ToPc);
                away.TurnOff(true, false);
                Check(searches == 2 && tv.Count("POST /keypress/PowerOff") == 1, "a button looked for the TV on a network with other numbers");

                // the tray's TV on (and the "on" desktop shortcut) does the same
                tv.PowerMode = "DisplayOff";
                Settings onPress = moved();
                afterSwap(onPress).TurnOn(TurnOnMode.PowerOnly);
                Check(searches == 3 && tv.IsOn, "TV on didn't find and turn on the moved TV: " + searches + " searches");
                Check(onPress.GatewayMac == "BB:BB:BB:BB:BB:BB", "TV on didn't learn the new router");

                // a press while the TV is too deeply asleep to answer finds nothing (there's no wake
                // packet behind a router TVFollow doesn't know); the next press, once the TV is on,
                // searches again rather than waiting ten minutes
                Settings asleep = moved();
                Controller one = afterSwap(asleep);
                one.SearchEvery = TimeSpan.FromMinutes(10);
                tv.Down = true;
                one.TurnOn(TurnOnMode.PowerOnly);
                Check(searches == 4 && asleep.Ip == "127.0.0.3", "a press should have searched and found nothing");
                tv.Down = false; // turned on with its own remote
                tv.App = "12";
                one.TurnOn(TurnOnMode.ToPc);
                Check(searches == 5, "the second press didn't search: " + searches + " searches");
                Check(asleep.Ip == "127.0.0.2" && asleep.GatewayMac == "BB:BB:BB:BB:BB:BB" && tv.App == "tvinput.hdmi1", "the second press didn't find the TV");
                File.Delete(Settings.FilePath);
            }
        }

        // a pressed button's search on a network that isn't home stops after a while, even when
        // the addresses there take the connection and answer slowly
        static void PressedSearchIsCapped()
        {
            using (var tv = new FakeTv("127.0.0.2", 0))
            using (var slow = new FakeTv("127.0.0.4", tv.Port))
            {
                slow.Serial = "SOMEONE-ELSE";
                slow.DelayMs = 800;
                var watch = Stopwatch.StartNew();
                string found = Discovery.FindBySerial(new[] { "127.0.0.4", "127.0.0.4", "127.0.0.2" }, tv.Port, tv.Serial, 300);
                watch.Stop();
                Check(found == null && tv.Count("GET /query/device-info") == 0, "the search went on past its limit");
                Check(watch.ElapsedMilliseconds < 2500, "the capped search took " + watch.ElapsedMilliseconds + " ms");
                Check(Discovery.FindBySerial(new[] { "127.0.0.2" }, tv.Port, tv.Serial) == "127.0.0.2", "without a limit the TV is found");
            }
        }

        // ---- the TV client ----

        static void RedirectIsNotFollowed()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            using (var decoy = new Decoy())
            {
                tv.RedirectTo = "http://127.0.0.1:" + decoy.Port + "/decoy";
                var client = new RokuClient(tv.Ip, tv.Port);
                DeviceInfo info = client.GetDeviceInfo(3000);
                Check(info == null, "a redirect was followed" + (info == null ? "" : " (got serial " + info.Serial + ")"));
                int code = client.Keypress("PowerOn", 3000);
                Check(code < 200 || code > 299, "a redirected press counted as sent: " + code);
                Check(decoy.Connections == 0, "the redirect target was connected to " + decoy.Connections + " time(s)");
                Check(tv.Count("GET /query/device-info") == 1 && tv.Count("POST /keypress/PowerOn") == 1, "the TV should see one request per call");
            }
        }
    }

    // Where a redirect from the pretend TV points: counts the connections it gets, and claims to
    // be a TV with another serial, so a followed redirect shows up as the wrong TV.
    class Decoy : IDisposable
    {
        readonly TcpListener listener;
        int connections;

        public readonly int Port;

        public Decoy()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var thread = new Thread(Serve);
            thread.IsBackground = true;
            thread.Start();
        }

        public int Connections { get { return Thread.VolatileRead(ref connections); } }

        public void Dispose() { listener.Stop(); }

        void Serve()
        {
            while (true)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch { return; }
                Interlocked.Increment(ref connections);
                try
                {
                    using (client)
                    {
                        NetworkStream stream = client.GetStream();
                        var reader = new StreamReader(stream, Encoding.ASCII);
                        string line;
                        while (!string.IsNullOrEmpty(line = reader.ReadLine())) { }
                        byte[] body = Encoding.UTF8.GetBytes("<device-info><serial-number>DECOY</serial-number><power-mode>PowerOn</power-mode></device-info>");
                        byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/xml\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                        stream.Write(head, 0, head.Length);
                        stream.Write(body, 0, body.Length);
                    }
                }
                catch { }
            }
        }
    }
}
