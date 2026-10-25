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
