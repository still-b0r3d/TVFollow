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
            Run("remote: an emoji is one letter, half of one throws", RemoteEmojiLetter);
            Run("remote: an emoji typed in halves is one press, a stray half is nothing", RemoteEmojiTyped);
            Run("remote: pasted text becomes presses, first line only", LettersFromText);
            Run("remote: a paste reaches the TV in order, a busy clipboard sends nothing", RemotePasteIsSent);
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
                    box.Paste(); // must not throw, must not send
                    Pump(() => true, 1);
                    Check(tv.Presses == 2, "a busy clipboard should send nothing: " + tv.Presses);
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
