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
        public bool Down;                         // stop answering, like a TV in deep standby

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

            string[] parts = requestLine.Split(' ');
            string method = parts[0], path = parts[1];
            lock (requests) requests.Add(method + " " + path);

            int status;
            string body;
            lock (this) Respond(method, path, out status, out body);

            byte[] content = Encoding.UTF8.GetBytes(body);
            string head = "HTTP/1.1 " + status + (status == 200 ? " OK" : status == 403 ? " Forbidden" : " Not Found") + "\r\n" +
                "Content-Type: text/xml; charset=utf-8\r\nContent-Length: " + content.Length + "\r\nConnection: close\r\n\r\n";
            byte[] headBytes = Encoding.ASCII.GetBytes(head);
            stream.Write(headBytes, 0, headBytes.Length);
            stream.Write(content, 0, content.Length);
        }

        void Respond(string method, string path, out int status, out string body)
        {
            status = 200;
            body = "";
            if (method == "GET" && path == "/query/device-info") { body = DeviceInfoXml(); return; }
            if (method == "GET" && path == "/query/active-app")
            {
                body = App.Length == 0
                    ? "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n<active-app>\n\t<app>Roku</app>\n</active-app>"
                    : "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n<active-app>\n\t<app id=\"" + App + "\" type=\"tvin\">PC</app>\n</active-app>";
                return;
            }
            if (method != "POST") { status = 404; return; }
            if (ControlMode == "limited" && path.StartsWith("/keypress/")) { status = 403; return; }
            if (path == "/keypress/PowerOff") PowerMode = "DisplayOff";
            else if (path == "/keypress/PowerOn") PowerMode = "PowerOn";
            else if (path.StartsWith("/keypress/InputHDMI")) { if (IsOn) App = "tvinput.hdmi" + path.Substring("/keypress/InputHDMI".Length); }
            else if (path.StartsWith("/launch/")) { if (IsOn) App = path.Substring("/launch/".Length); }
            else status = 404;
        }

        string DeviceInfoXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n<device-info>\n" +
                "\t<serial-number>" + Serial + "</serial-number>\n" +
                "\t<vendor-name>Example</vendor-name>\n" +
                "\t<model-name>EX43</model-name>\n" +
                "\t<is-tv>true</is-tv>\n" +
                "\t<wifi-mac>00:11:22:33:44:55</wifi-mac>\n" +
                "\t<ethernet-mac>00:11:22:33:44:66</ethernet-mac>\n" +
                "\t<network-type>" + NetworkType + "</network-type>\n" +
                "\t<friendly-device-name>Test TV</friendly-device-name>\n" +
                "\t<user-device-location>Test room</user-device-location>\n" +
                "\t<power-mode>" + PowerMode + "</power-mode>\n" +
                "\t<ecp-setting-mode>" + ControlMode + "</ecp-setting-mode>\n" +
                "</device-info>";
        }
    }
}
