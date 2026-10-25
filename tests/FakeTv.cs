using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TVFollow.Tests
{
    // A pretend TV with Roku built in. It answers the few requests TVFollow makes and
    // remembers the button presses, so the tests can run without a real TV.
    class FakeTv : IDisposable
    {
        public string Serial = "X000000001";
        public string PowerMode = "PowerOn";      // "PowerOn", or "DisplayOff" when off
        public string App = "tvinput.hdmi1";      // "" = the Roku home screen
        public string ControlMode = "enabled";    // "limited" refuses button presses
        public string NetworkType = "wifi";
        public string Name = "Test TV";
        public string Location = "Test room";
        public bool IsTv = true;
        public string[] InputNames = { "HDMI 1", "HDMI 2", "HDMI 3" };
        public bool Down;                         // stop answering, like a TV in deep standby
        public bool DownWhenOff;                  // go into deep standby as soon as it's turned off
        public int DelayMs;                       // wait this long before answering anything
        public int TrickleMs;                     // send the headers, then wait this long before the body
        public int PadBytes;                      // make device-info this much bigger
        public string RawDeviceInfo;              // answer device-info with exactly this
        public bool RefusePowerOn;                // say OK to PowerOn but stay off
        public bool IgnoreInput;                  // say OK to input changes but don't switch
        public string RedirectTo;                 // answer everything with "302 Found, go here" (no TV does this)

        public readonly string Ip;
        public readonly int Port;
        readonly List<string> requests = new List<string>();
        readonly TcpListener listener;

        public FakeTv(string ip, int port)
        {
            listener = new TcpListener(IPAddress.Parse(ip), port);
            listener.Start();
            Ip = ip;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var thread = new Thread(Serve);
            thread.IsBackground = true;
            thread.Start();
        }

        public bool IsOn { get { return PowerMode == "PowerOn"; } }

        // how many times a request like "POST /keypress/PowerOff" came in
        public int Count(string request)
        {
            lock (requests) return requests.FindAll(r => r == request).Count;
        }

        public int Presses
        {
            get { lock (requests) return requests.FindAll(r => r.StartsWith("POST ")).Count; }
        }

        // every request so far, in the order they came
        public List<string> Requests()
        {
            lock (requests) return new List<string>(requests);
        }

        public void Dispose() { listener.Stop(); }

        void Serve()
        {
            while (true)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch { return; }
                try { using (client) Handle(client); }
                catch { }
            }
        }

        void Handle(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.ASCII);
            string requestLine = reader.ReadLine();
            if (requestLine == null) return;
            string header;
            while (!string.IsNullOrEmpty(header = reader.ReadLine())) { }
            if (Down) return; // hang up without answering
            if (DelayMs > 0) Thread.Sleep(DelayMs);

            string[] parts = requestLine.Split(' ');
            string method = parts[0], path = parts[1];
            lock (requests) requests.Add(method + " " + path);

            int status;
            string body;
            string extraHeaders = "";
            if (RedirectTo != null)
            {
                status = 302;
                body = "";
                extraHeaders = "Location: " + RedirectTo + "\r\n";
            }
            else lock (this) Respond(method, path, out status, out body);

            byte[] content = Encoding.UTF8.GetBytes(body);
            string reason = status == 200 ? " OK" : status == 302 ? " Found" : status == 403 ? " Forbidden" : " Not Found";
            string head = "HTTP/1.1 " + status + reason + "\r\n" + extraHeaders +
                "Content-Type: text/xml; charset=utf-8\r\nContent-Length: " + content.Length + "\r\nConnection: close\r\n\r\n";
            byte[] headBytes = Encoding.ASCII.GetBytes(head);
            stream.Write(headBytes, 0, headBytes.Length);
            if (TrickleMs > 0)
            {
                stream.Flush();
                Thread.Sleep(TrickleMs);
            }
            stream.Write(content, 0, content.Length);
        }

        void Respond(string method, string path, out int status, out string body)
        {
            status = 200;
            body = "";
            if (method == "GET" && path == "/query/device-info")
            {
                body = RawDeviceInfo ?? DeviceInfoXml();
                if (PadBytes > 0) body += "<!--" + new string('x', PadBytes) + "-->";
                return;
            }
            if (method == "GET" && path == "/query/active-app")
            {
                body = App.Length == 0
                    ? "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n<active-app>\n\t<app>Roku</app>\n</active-app>"
                    : "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n<active-app>\n\t<app id=\"" + App + "\" type=\"tvin\">PC</app>\n</active-app>";
                return;
            }
            if (method == "GET" && path == "/query/apps")
            {
                if (ControlMode == "limited") { status = 403; return; }
                var apps = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n<apps>\n");
                for (int i = InputNames.Length; i >= 1; i--)
                    apps.Append("\t<app id=\"tvinput.hdmi" + i + "\" type=\"tvin\" version=\"1.0.0\">" + InputNames[i - 1] + "</app>\n");
                apps.Append("\t<app id=\"tvinput.cvbs\" type=\"tvin\" version=\"1.0.0\">AV</app>\n");
                apps.Append("\t<app id=\"12\" type=\"appl\" version=\"5.2.0\">Some channel</app>\n</apps>");
                body = apps.ToString();
                return;
            }
            if (method != "POST") { status = 404; return; }
            if (ControlMode == "limited" && path.StartsWith("/keypress/")) { status = 403; return; }
            if (path == "/keypress/PowerOff") { PowerMode = "DisplayOff"; if (DownWhenOff) Down = true; }
            else if (path == "/keypress/PowerOn") { if (!RefusePowerOn) PowerMode = "PowerOn"; }
            else if (path.StartsWith("/keypress/InputHDMI")) { if (IsOn && !IgnoreInput) App = "tvinput.hdmi" + path.Substring("/keypress/InputHDMI".Length); }
            else if (path.StartsWith("/launch/")) { if (IsOn && !IgnoreInput) App = path.Substring("/launch/".Length); }
            else if (!path.StartsWith("/keypress/")) status = 404; // any other remote key is fine, like a real TV
        }

        string DeviceInfoXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n<device-info>\n" +
                "\t<serial-number>" + Serial + "</serial-number>\n" +
                "\t<vendor-name>Example</vendor-name>\n" +
                "\t<model-name>EX43</model-name>\n" +
                "\t<is-tv>" + (IsTv ? "true" : "false") + "</is-tv>\n" +
                "\t<wifi-mac>00:11:22:33:44:55</wifi-mac>\n" +
                "\t<ethernet-mac>00:11:22:33:44:66</ethernet-mac>\n" +
                "\t<network-type>" + NetworkType + "</network-type>\n" +
                "\t<friendly-device-name>" + Name + "</friendly-device-name>\n" +
                "\t<user-device-location>" + Location + "</user-device-location>\n" +
                "\t<power-mode>" + PowerMode + "</power-mode>\n" +
                "\t<ecp-setting-mode>" + ControlMode + "</ecp-setting-mode>\n" +
                "</device-info>";
        }
    }
}
