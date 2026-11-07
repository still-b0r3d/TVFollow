using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace TVFollow.Tests
{
    // A pretend TV's developer-mode app installer (port 80 on a real TV): it asks for the password
    // with HTTP digest as a Roku does, takes apps, removes them and takes pictures of them, and
    // tells its FakeTv which app it has, so the TV's list of apps shows it.
    class FakeInstaller : IDisposable
    {
        public const string Realm = "rokudev";

        public string Password = "test-pass-123";
        public bool JsonMessages;       // newer Roku OS: the page's messages as JSON.parse('...')
        public string Refuse;           // answer pushes with this error, like "Install Failure: Compilation Failed."
        public bool RefuseReplace;      // turn away mysubmit=Replace (but take Install), as some TVs might
        public bool OfferBasic;         // offer Basic (the password as it is) beside digest
        public int ForceStatus;         // answer every request that has the password with this status
        public bool UpdateCheckPage;    // answer pushes with newer Roku OS's "check for an update first" page
        public int Installs;            // apps taken (not counting the same files again)
        public int Asked;               // requests that came with the right password
        public bool SawBasic;           // a request offered the password with Basic, as it is
        public byte[] Last;             // the last app taken
        public int Hits;                // requests of any kind, with a password or not
        public int Submits;             // pushes with the right password, the same files again too

        public readonly int Port;
        readonly FakeTv tv;
        readonly TcpListener listener;
        readonly string nonce = Guid.NewGuid().ToString("N");
        readonly byte[] picture;

        public FakeInstaller(FakeTv tv, int port)
        {
            this.tv = tv;
            listener = new TcpListener(IPAddress.Parse(tv.Ip), port);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using (var bmp = new Bitmap(64, 36))
            using (var ms = new MemoryStream())
            {
                using (Graphics g = Graphics.FromImage(bmp)) g.Clear(Color.FromArgb(20, 33, 61));
                bmp.Save(ms, ImageFormat.Jpeg);
                picture = ms.ToArray();
            }
            var thread = new Thread(Serve) { IsBackground = true };
            thread.Start();
        }

        public void Dispose() { listener.Stop(); }

        void Serve()
        {
            while (true)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch { return; }
                try { using (client) Handle(client.GetStream()); }
                catch { }
            }
        }

        static string ReadLine(Stream s)
        {
            var line = new StringBuilder();
            int b;
            while ((b = s.ReadByte()) >= 0)
            {
                if (b == '\n') break;
                if (b != '\r') line.Append((char)b);
            }
            return b < 0 && line.Length == 0 ? null : line.ToString();
        }

        void Handle(NetworkStream stream)
        {
            string requestLine = ReadLine(stream);
            if (requestLine == null) return;
            Hits++;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string header;
            while (!string.IsNullOrEmpty(header = ReadLine(stream)))
            {
                int colon = header.IndexOf(':');
                if (colon > 0) headers[header.Substring(0, colon).Trim()] = header.Substring(colon + 1).Trim();
            }
            string[] parts = requestLine.Split(' ');
            string method = parts[0], path = parts[1];
            int length = 0;
            string value;
            if (headers.TryGetValue("Content-Length", out value)) int.TryParse(value, out length);
            var body = new byte[length];
            for (int read = 0; read < length; )
            {
                int n = stream.Read(body, read, length - read);
                if (n <= 0) break;
                read += n;
            }

            string auth;
            headers.TryGetValue("Authorization", out auth);
            if (auth != null && auth.StartsWith("Basic", StringComparison.OrdinalIgnoreCase)) SawBasic = true;
            if (auth == null || !DigestOk(auth, method))
            {
                Send(stream, 401, "Unauthorized", "text/html", Encoding.ASCII.GetBytes("<html>401 Unauthorized</html>"),
                    (OfferBasic ? "WWW-Authenticate: Basic realm=\"" + Realm + "\"\r\n" : "") +
                    "WWW-Authenticate: Digest qop=\"auth\", realm=\"" + Realm + "\", nonce=\"" + nonce + "\"\r\n");
                return;
            }
            Asked++;
            tv.Record("INSTALLER " + method + " " + path);

            if (ForceStatus != 0)
            {
                Send(stream, ForceStatus, "Forced", "text/html", new byte[0], ForceStatus / 100 == 3 ? "Location: http://192.0.2.1/elsewhere\r\n" : "");
                return;
            }
            if (method == "HEAD" && path == "/plugin_install")
            {
                Send(stream, 200, "OK", "text/html", new byte[0], "");
                return;
            }
            if (method == "GET" && (path == "/" || path == "/plugin_install"))
            {
                Page(stream, "<html><title>Roku Development Kit</title>Development Application Installer</html>");
                return;
            }
            if (method == "GET" && path.StartsWith("/pkgs/dev.jpg"))
            {
                Send(stream, 200, "OK", "image/jpeg", picture, "");
                return;
            }
            if (method != "POST") { Send(stream, 404, "Not Found", "text/html", new byte[0], ""); return; }

            Dictionary<string, byte[]> form = Multipart(headers["Content-Type"], body);
            string submit = form.ContainsKey("mysubmit") ? Encoding.UTF8.GetString(form["mysubmit"]) : "";
            tv.Record("INSTALLER " + submit);
            if (path == "/plugin_install" && (submit == "Replace" || submit == "Install"))
            {
                byte[] zip = form.ContainsKey("archive") ? form["archive"] : new byte[0];
                Submits++;
                if (UpdateCheckPage) { Page(stream, Message("error", "Failed to check for software update")); return; }
                if (Refuse != null) { Page(stream, Message("error", Refuse)); return; }
                if (RefuseReplace && submit == "Replace") { Page(stream, Message("error", "Install Failure: Nothing to replace.")); return; }
                if (Last != null && tv.DevAppName != null && Same(zip, Last)) { Page(stream, Message("info", "Identical to previous version -- not replacing.")); return; }
                string title, version;
                if (!ReadApp(zip, out title, out version)) { Page(stream, Message("error", "Install Failure: No manifest. Invalid package.")); return; }
                lock (tv)
                {
                    tv.DevAppName = title;
                    tv.DevAppVersion = version;
                    if (tv.IsOn) tv.App = "dev";   // the TV opens what it just took
                }
                Last = zip;
                Installs++;
                Page(stream, Message("success", "Install Success."));
                return;
            }
            if (path == "/plugin_install" && submit == "Delete")
            {
                lock (tv)
                {
                    tv.DevAppName = null;
                    if (tv.App == "dev") tv.App = "";
                }
                Last = null;
                Page(stream, Message("success", "Delete Succeeded."));
                return;
            }
            if (path == "/plugin_inspect" && submit == "Screenshot")
            {
                if (tv.App != "dev") { Page(stream, Message("error", "Screenshot failed. No dev app running.")); return; }
                Page(stream, Message("success", "Screenshot ok") + "<img src=\"pkgs/dev.jpg?time=1762430400\">");
                return;
            }
            Send(stream, 404, "Not Found", "text/html", new byte[0], "");
        }

        // a message as the installer's page puts it, in the older or the newer way
        string Message(string type, string text)
        {
            string escaped = text.Replace("\\", "\\\\").Replace("'", "\\'");
            if (JsonMessages)
                return "<script>var params = JSON.parse('{\"messages\":[{\"text\":\"" + escaped.Replace("\"", "\\\\\"") +
                    "\",\"text_type\":\"text\",\"type\":\"" + type + "\"}],\"metadata\":{}}');</script>";
            return "<script>Shell.create('Roku.Message').trigger('Set message type', '" + type + "').trigger('Set message content', '" +
                escaped + "').trigger('Render', node);</script>";
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static bool ReadApp(byte[] zip, out string title, out string version)
        {
            title = version = "";
            try
            {
                using (var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
                {
                    ZipArchiveEntry manifest = archive.GetEntry("manifest");
                    if (manifest == null) return false;
                    string major = "", minor = "", build = "";
                    using (var reader = new StreamReader(manifest.Open()))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            int eq = line.IndexOf('=');
                            if (line.StartsWith("#") || eq < 0) continue;
                            string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
                            if (key == "title") title = value;
                            else if (key == "major_version") major = value;
                            else if (key == "minor_version") minor = value;
                            else if (key == "build_version") build = value;
                        }
                    }
                    version = major + "." + minor + "." + build;
                    return true;
                }
            }
            catch { return false; }
        }

        // the form's fields by name: "mysubmit", and "archive" (the app's zip)
        static Dictionary<string, byte[]> Multipart(string contentType, byte[] body)
        {
            var fields = new Dictionary<string, byte[]>();
            int at = contentType.IndexOf("boundary=");
            if (at < 0) return fields;
            byte[] boundary = Encoding.ASCII.GetBytes("--" + contentType.Substring(at + 9).Trim('"'));
            int start = Find(body, boundary, 0);
            while (start >= 0)
            {
                int headersAt = start + boundary.Length + 2;   // past the boundary's \r\n
                int next = Find(body, boundary, headersAt);
                if (next < 0 || headersAt >= body.Length) break;
                int blank = Find(body, Encoding.ASCII.GetBytes("\r\n\r\n"), headersAt);
                if (blank < 0 || blank > next) break;
                string partHeaders = Encoding.UTF8.GetString(body, headersAt, blank - headersAt);
                int nameAt = partHeaders.IndexOf("name=\"");
                if (nameAt >= 0)
                {
                    string name = partHeaders.Substring(nameAt + 6, partHeaders.IndexOf('"', nameAt + 6) - nameAt - 6);
                    int contentAt = blank + 4, contentEnd = next - 2;   // the content ends with \r\n
                    var content = new byte[Math.Max(0, contentEnd - contentAt)];
                    Array.Copy(body, contentAt, content, 0, content.Length);
                    fields[name] = content;
                }
                start = next;
            }
            return fields;
        }

        static int Find(byte[] haystack, byte[] needle, int from)
        {
            for (int i = from; i <= haystack.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }

        // HTTP digest as RFC 2617 has it with qop=auth, the way a Roku checks it
        bool DigestOk(string auth, string method)
        {
            if (!auth.StartsWith("Digest ", StringComparison.OrdinalIgnoreCase)) return false;
            var p = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string rest = auth.Substring(7);
            int i = 0;
            while (i < rest.Length)
            {
                while (i < rest.Length && (rest[i] == ' ' || rest[i] == ',')) i++;
                int eq = rest.IndexOf('=', i);
                if (eq < 0) break;
                string key = rest.Substring(i, eq - i).Trim();
                i = eq + 1;
                string val;
                if (i < rest.Length && rest[i] == '"')
                {
                    int close = rest.IndexOf('"', i + 1);
                    val = rest.Substring(i + 1, close - i - 1);
                    i = close + 1;
                }
                else
                {
                    int comma = rest.IndexOf(',', i);
                    if (comma < 0) comma = rest.Length;
                    val = rest.Substring(i, comma - i).Trim();
                    i = comma;
                }
                p[key] = val;
            }
            string user, realm, gotNonce, uri, cnonce, nc, qop, response;
            if (!p.TryGetValue("username", out user) || !p.TryGetValue("realm", out realm) || !p.TryGetValue("nonce", out gotNonce) ||
                !p.TryGetValue("uri", out uri) || !p.TryGetValue("cnonce", out cnonce) || !p.TryGetValue("nc", out nc) ||
                !p.TryGetValue("qop", out qop) || !p.TryGetValue("response", out response)) return false;
            if (user != "rokudev" || realm != Realm || gotNonce != nonce || qop != "auth") return false;
            string ha1 = Md5(user + ":" + realm + ":" + Password), ha2 = Md5(method + ":" + uri);
            return string.Equals(response, Md5(ha1 + ":" + gotNonce + ":" + nc + ":" + cnonce + ":" + qop + ":" + ha2), StringComparison.OrdinalIgnoreCase);
        }

        static string Md5(string text)
        {
            using (MD5 md5 = MD5.Create())
            {
                var hex = new StringBuilder();
                foreach (byte b in md5.ComputeHash(Encoding.UTF8.GetBytes(text))) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        static void Page(Stream stream, string html)
        {
            Send(stream, 200, "OK", "text/html", Encoding.UTF8.GetBytes("<html><body>" + html + "</body></html>"), "");
        }

        static void Send(Stream stream, int status, string reason, string type, byte[] content, string extra)
        {
            byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " " + reason + "\r\n" + extra +
                "Content-Type: " + type + "\r\nContent-Length: " + content.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(head, 0, head.Length);
            stream.Write(content, 0, content.Length);
        }
    }

    // A pretend TV's app console (port 8085 in developer mode): says hello, sends a telnet
    // command first like some consoles do, and passes on whatever the test says.
    class FakeConsole : IDisposable
    {
        public readonly int Port;
        public string Backlog = "";   // sent to each new reader after the welcome, as a TV repeats its recent lines
        public bool Busy;             // says another program reads it and hangs up, as a TV does to a second reader
        public int BusyHangUpMs;      // how long a busy console waits before it hangs up
        int accepted;
        readonly TcpListener listener;
        readonly List<NetworkStream> open = new List<NetworkStream>();

        public FakeConsole(string ip, int port)
        {
            listener = new TcpListener(IPAddress.Parse(ip), port);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var thread = new Thread(Serve) { IsBackground = true };
            thread.Start();
        }

        public int Connections { get { lock (open) return open.Count; } }

        // every connection made, a turned-away one too
        public int Accepted { get { lock (open) return accepted; } }

        void Serve()
        {
            while (true)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch { return; }
                NetworkStream s = client.GetStream();
                lock (open) accepted++;
                if (Busy)
                {
                    byte[] busy = Encoding.ASCII.GetBytes("Console connection is already in use.\r\n");
                    try { s.Write(busy, 0, busy.Length); } catch { }
                    int wait = BusyHangUpMs;
                    if (wait <= 0) client.Close();
                    else new Thread(delegate () { Thread.Sleep(wait); client.Close(); }) { IsBackground = true }.Start();
                    continue;
                }
                byte[] hello = Encoding.ASCII.GetBytes("Welcome to the pretend app console.\r\n" + Backlog);
                s.Write(new byte[] { 255, 251, 1 }, 0, 3);                // IAC WILL ECHO: not text
                s.Write(new byte[] { 255, 250, 24, 1, 255, 240 }, 0, 6);  // IAC SB ... IAC SE: not text either
                s.Write(hello, 0, hello.Length);
                lock (open) open.Add(s);
            }
        }

        public void Say(string line)
        {
            Send(line + "\n");
        }

        // without a line break: a line can come in pieces
        public void Send(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            lock (open) foreach (NetworkStream s in open) { try { s.Write(bytes, 0, bytes.Length); } catch { } }
        }

        public void Dispose()
        {
            listener.Stop();
            lock (open) foreach (NetworkStream s in open) { try { s.Close(); } catch { } }
        }
    }
}
