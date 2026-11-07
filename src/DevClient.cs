using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace TVFollow
{
    // The app pushed to a TV in developer mode, as the TV lists it (its id is always "dev").
    class DevApp
    {
        public string Name = "";
        public string Version = "";

        public override string ToString()
        {
            return (Name.Length > 0 ? Name : "An app") + (Version.Length > 0 ? " " + Version : "");
        }
    }

    enum DevOutcome
    {
        Done,           // pushed, removed, pictured, or the password is right
        Same,           // the TV has these very files already
        NoPassword,     // no password typed in yet
        WrongPassword,
        DevModeOff,     // the TV answers, but developer mode is off
        OtherRoku,      // something else answers at the TV's address right now
        NoAnswer,       // the TV didn't answer
        InstallerSilent,// the TV answers, but its app installer didn't
        TooSlow,        // the TV took too long to answer
        UpdateCheck,    // the TV wants to look for a Roku update before it takes apps
        Refused,        // the TV answered with an error of its own; Message has its words
        Other           // something else went wrong; Message says what, in TVFollow's words
    }

    class DevResult
    {
        public const int MaxWords = 300;

        public readonly DevOutcome Outcome;
        public readonly string Message;  // the TV's own words (or, for Other, TVFollow's), if any

        public DevResult(DevOutcome outcome, string message)
        {
            Outcome = outcome;
            Message = OneLine(message);
        }

        public bool Ok { get { return Outcome == DevOutcome.Done || Outcome == DevOutcome.Same; } }

        // the TV turned the app away because of its code: pushing again won't help
        public bool CompileError { get { return Message.IndexOf("compil", StringComparison.OrdinalIgnoreCase) >= 0; } }

        // The TV's words end up in the window, a notification and the log, so they're made one
        // line (a reply can't write lines of its own into the log) and kept short.
        static string OneLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var line = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsControl(c)) { if (line.Length > 0 && line[line.Length - 1] != ' ') line.Append(' '); }
                else line.Append(c);
                if (line.Length >= MaxWords) { line.Append("…"); break; }
            }
            return line.ToString().Trim();
        }

        // why it didn't work, in words for the window; null when it did
        public string Problem
        {
            get
            {
                switch (Outcome)
                {
                    case DevOutcome.NoPassword: return "Type the password you chose on the TV first.";
                    case DevOutcome.WrongPassword: return "The TV says the password is wrong. It's the one you chose when you turned developer mode on.";
                    case DevOutcome.DevModeOff: return "Developer mode is off on this TV. Turn it on first.";
                    case DevOutcome.OtherRoku: return "Another Roku answers at this TV's address right now. TVFollow finds your TV again the next time it turns it on or off.";
                    case DevOutcome.NoAnswer: return "The TV didn't answer. Is it on, and on this network?";
                    case DevOutcome.InstallerSilent: return "The TV's app installer didn't answer. Try again in a moment.";
                    case DevOutcome.TooSlow: return "The TV took too long to answer. Try again.";
                    case DevOutcome.UpdateCheck: return "The TV wants to check for a Roku update before it takes apps. On the TV: Settings → System → System update → Check now. Then try again.";
                    case DevOutcome.Refused:
                        return "The TV said: " + Message.TrimEnd('.') + "." + (CompileError ? " Open App log…, then push it again to see why." : "");
                    case DevOutcome.Other: return Message;
                    default: return null;
                }
            }
        }
    }

    // A TV's developer mode: its app installer, a web page on port 80 behind the password chosen
    // on the TV (user rokudev, HTTP digest), and the pushed app's console on port 8085.
    class DevClient
    {
        public const string User = "rokudev";
        public static int InstallerPort = 80;   // the tests' pretend TVs use ports of their own
        public static int ConsolePort = 8085;
        public const int MaxPage = 1024 * 1024;           // the installer's pages are a few kilobytes
        public const int MaxPicture = 32 * 1024 * 1024;

        readonly string ip;
        readonly string password;

        public DevClient(string ip, string password)
        {
            this.ip = ip;
            this.password = password ?? "";
        }

        // The app (a zip with its manifest at the top) goes on the TV in place of the one there,
        // and the TV opens it. If the TV refuses Replace for anything but the app's code, it's
        // tried once more as Install, as Roku's own tools do.
        public DevResult Push(byte[] zip, int timeoutMs)
        {
            DevResult r = Submit("plugin_install", "Replace", zip, timeoutMs).Result;
            if ((r.Outcome == DevOutcome.Refused || r.Outcome == DevOutcome.Other) && !r.CompileError)
            {
                DevResult again = Submit("plugin_install", "Install", zip, timeoutMs).Result;
                if (again.Ok) return again;
            }
            return r;
        }

        public DevResult Remove(int timeoutMs) { return Submit("plugin_install", "Delete", null, timeoutMs).Result; }

        // asks the installer whether the password is right, the way Roku's own tools do: HEAD on
        // its page is 200 with the right password, 401 with a wrong one
        public DevResult CheckPassword(int timeoutMs)
        {
            if (password.Length == 0) return new DevResult(DevOutcome.NoPassword, "");
            try
            {
                HttpWebRequest req = Request("plugin_install", timeoutMs);
                req.Method = "HEAD";
                using (RokuClient.Deadline(req, timeoutMs))
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    int code = (int)resp.StatusCode;
                    return code == 200 ? new DevResult(DevOutcome.Done, "") : Unexpected(code);
                }
            }
            catch (WebException e) { return Failed(e); }
            catch (Exception e) { return new DevResult(DevOutcome.NoAnswer, e.Message); }
        }

        static DevResult Unexpected(int code)
        {
            return new DevResult(DevOutcome.Other, "The TV's app installer answered in a way TVFollow doesn't know (" + code + ").");
        }

        // A picture of the pushed app's screen (Roku only takes one of that app): its bytes and
        // "png" or "jpg", or null with the reason in result.
        public byte[] Screenshot(int timeoutMs, out string extension, out DevResult result)
        {
            extension = "";
            Submitted page = Submit("plugin_inspect", "Screenshot", null, timeoutMs);
            result = page.Result;
            if (!result.Ok) return null;
            Match link = Regex.Match(page.Body, "[\"'](pkgs/dev\\.(png|jpg)(\\?[^\"'<>]*)?)[\"']", RegexOptions.IgnoreCase);
            if (!link.Success)
            {
                result = new DevResult(DevOutcome.Other, "No picture came back. Is the app open on the TV?");
                return null;
            }
            extension = link.Groups[2].Value.ToLowerInvariant();
            try
            {
                HttpWebRequest req = Request(link.Groups[1].Value, timeoutMs);
                using (RokuClient.Deadline(req, timeoutMs))
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    int code = (int)resp.StatusCode;
                    if (code != 200) { result = Unexpected(code); return null; }
                    using (Stream picture = resp.GetResponseStream()) return ReadAll(picture, MaxPicture);
                }
            }
            catch (WebException e) { result = Failed(e); }
            catch (InvalidDataException) { result = new DevResult(DevOutcome.Other, "The picture the TV sent was far too big."); }
            catch (Exception e) { result = new DevResult(DevOutcome.NoAnswer, e.Message); }
            return null;
        }

        // the pushed app's console: what it prints, and where it stopped when it crashed
        public static TcpClient OpenConsole(string ip, int timeoutMs)
        {
            var client = new TcpClient();
            try
            {
                IAsyncResult connecting = client.BeginConnect(ip, ConsolePort, null, null);
                if (!connecting.AsyncWaitHandle.WaitOne(timeoutMs)) throw new TimeoutException("no answer");
                client.EndConnect(connecting);
                // A TV that loses power or its Wi-Fi never says goodbye, and the console only ever
                // sends: keep-alive notices it's gone within about half a minute (10 s quiet, then
                // Windows' 10 tries 2 s apart), so the window can reconnect.
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                var keepAlive = new byte[12];
                BitConverter.GetBytes(1u).CopyTo(keepAlive, 0);
                BitConverter.GetBytes(10000u).CopyTo(keepAlive, 4);
                BitConverter.GetBytes(2000u).CopyTo(keepAlive, 8);
                try { client.Client.IOControl(IOControlCode.KeepAliveValues, keepAlive, null); }
                catch { }  // the plain keep-alive above still works, only slower
                return client;
            }
            catch
            {
                client.Close();
                throw;
            }
        }

        class Submitted
        {
            public DevResult Result;
            public string Body = "";
        }

        // the installer's form, sent the way its web page sends it
        Submitted Submit(string path, string action, byte[] archive, int timeoutMs)
        {
            var done = new Submitted();
            if (password.Length == 0)
            {
                done.Result = new DevResult(DevOutcome.NoPassword, "");
                return done;
            }
            string boundary = "----TVFollow" + Guid.NewGuid().ToString("N");
            byte[] body = Form(boundary, action, archive);
            try
            {
                HttpWebRequest req = Request(path, timeoutMs);
                req.Method = "POST";
                req.ContentType = "multipart/form-data; boundary=" + boundary;
                req.ContentLength = body.Length;
                int code;
                using (RokuClient.Deadline(req, timeoutMs))
                {
                    // kept in memory (the default), so it can go again once the TV asks for the password
                    using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (Stream page = resp.GetResponseStream())
                    {
                        code = (int)resp.StatusCode;
                        done.Body = Encoding.UTF8.GetString(ReadAll(page, MaxPage));
                    }
                }
                // only a plain 200 is the installer's own page (a redirect, say, is not)
                done.Result = code == 200 ? Judge(done.Body) : Unexpected(code);
            }
            catch (WebException e) { done.Result = Failed(e); }
            catch (InvalidDataException) { done.Result = new DevResult(DevOutcome.Other, "The TV's app installer sent back far too much."); }
            catch (Exception e) { done.Result = new DevResult(DevOutcome.NoAnswer, e.Message); }
            return done;
        }

        public static byte[] Form(string boundary, string action, byte[] archive)
        {
            var body = new MemoryStream();
            Action<string> write = text =>
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                body.Write(bytes, 0, bytes.Length);
            };
            write("--" + boundary + "\r\nContent-Disposition: form-data; name=\"mysubmit\"\r\n\r\n" + action + "\r\n");
            if (archive == null) write("--" + boundary + "\r\nContent-Disposition: form-data; name=\"archive\"\r\n\r\n\r\n");
            else
            {
                write("--" + boundary + "\r\nContent-Disposition: form-data; name=\"archive\"; filename=\"app.zip\"\r\nContent-Type: application/zip\r\n\r\n");
                body.Write(archive, 0, archive.Length);
                write("\r\n");
            }
            write("--" + boundary + "--\r\n");
            return body.ToArray();
        }

        HttpWebRequest Request(string path, int timeoutMs)
        {
            string root = "http://" + ip + ":" + InstallerPort + "/";
            var req = (HttpWebRequest)WebRequest.Create(root + path);
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.Proxy = null;
            req.KeepAlive = false;
            req.AllowAutoRedirect = false;   // like every request to the TV: an answer, not somewhere to go
            req.UserAgent = "TVFollow/" + Program.Version;
            req.ServicePoint.Expect100Continue = false;
            // digest only: the password itself never goes over the network, as it would with Basic
            var credentials = new CredentialCache();
            credentials.Add(new Uri(root), "Digest", new NetworkCredential(User, password));
            req.Credentials = credentials;
            return req;
        }

        static DevResult Failed(WebException e)
        {
            var resp = e.Response as HttpWebResponse;
            if (resp == null)
            {
                // a time-out, or the deadline cutting a slow answer short
                bool slow = e.Status == WebExceptionStatus.Timeout || e.Status == WebExceptionStatus.RequestCanceled;
                return new DevResult(slow ? DevOutcome.TooSlow : DevOutcome.NoAnswer, e.Message);
            }
            int code = (int)resp.StatusCode;
            resp.Close();
            if (code == 401) return new DevResult(DevOutcome.WrongPassword, "");
            // newer Roku OS turns apps away like this until the TV has looked for an update
            if (code == 577) return new DevResult(DevOutcome.UpdateCheck, "");
            return new DevResult(DevOutcome.Other, "The TV's app installer turned it away (error " + code + ").");
        }

        static byte[] ReadAll(Stream s, int max)
        {
            var all = new MemoryStream();
            var buffer = new byte[16384];
            int n;
            while ((n = s.Read(buffer, 0, buffer.Length)) > 0)
            {
                all.Write(buffer, 0, n);
                if (all.Length > max) throw new InvalidDataException("the TV's answer is too big");
            }
            return all.ToArray();
        }

        // How it went, from the installer's page. The page says so in a script, one of two ways
        // depending on the Roku OS:
        //   Shell.create('Roku.Message').trigger('Set message type', 'error')
        //       .trigger('Set message content', 'Install Failure: ...')
        //   JSON.parse('{"messages":[{"text":"Install Success.","type":"success","text_type":"text"}]}')
        // and older ones just write "Install Failure: ..." into the page.
        public static DevResult Judge(string page)
        {
            if (page.IndexOf("Identical to previous version", StringComparison.OrdinalIgnoreCase) >= 0)
                return new DevResult(DevOutcome.Same, "");
            if (Regex.IsMatch(page, "[\"']\\s*Failed\\s*to\\s*check\\s*for\\s*software\\s*update\\s*[\"']", RegexOptions.IgnoreCase))
                return new DevResult(DevOutcome.UpdateCheck, "");
            string error = null, other = null;
            foreach (KeyValuePair<string, string> message in Messages(page))
            {
                if (message.Key == "error") { if (error == null) error = message.Value; }
                else if (other == null) other = message.Value;
            }
            if (error == null)
            {
                Match plain = Regex.Match(page, "(Install Failure|Delete Failure)[^<\\r\\n'\"]*");
                if (plain.Success && other == null) error = plain.Value.Trim();
            }
            if (error != null) return new DevResult(DevOutcome.Refused, error);
            return new DevResult(DevOutcome.Done, other ?? "");
        }

        // the page's messages as (type, text): "success", "info" or "error"
        public static List<KeyValuePair<string, string>> Messages(string page)
        {
            var found = new List<KeyValuePair<string, string>>();
            const string quoted = "'((?:[^'\\\\]|\\\\.)*)'";
            foreach (Match m in Regex.Matches(page, "Shell\\.create\\('Roku\\.Message'\\)\\.trigger\\('[\\w\\s]+',\\s*'(\\w+)'\\)\\.trigger\\('[\\w\\s]+',\\s*" + quoted + "\\)", RegexOptions.IgnoreCase))
                found.Add(new KeyValuePair<string, string>(m.Groups[1].Value.ToLowerInvariant(), Unescape(m.Groups[2].Value)));
            foreach (Match m in Regex.Matches(page, "JSON\\.parse\\(" + quoted + "\\)"))
            {
                InstallerPage parsed;
                try
                {
                    using (var json = new MemoryStream(Encoding.UTF8.GetBytes(Unescape(m.Groups[1].Value))))
                        parsed = (InstallerPage)new DataContractJsonSerializer(typeof(InstallerPage)).ReadObject(json);
                }
                catch { continue; }
                if (parsed == null || parsed.Messages == null) continue;
                foreach (InstallerMessage message in parsed.Messages)
                    if (message != null && message.Type != null && message.Text != null)
                        found.Add(new KeyValuePair<string, string>(message.Type.ToLowerInvariant(), message.Text));
            }
            return found;
        }

        // a JavaScript string's \' \" \\ \/ \n and \t; \u stays for the JSON reader
        static string Unescape(string js)
        {
            var text = new StringBuilder();
            for (int i = 0; i < js.Length; i++)
            {
                char c = js[i];
                if (c != '\\' || i + 1 == js.Length) { text.Append(c); continue; }
                char next = js[++i];
                if (next == 'u') text.Append("\\u");
                else text.Append(next == 'n' ? '\n' : next == 't' ? '\t' : next);
            }
            return text.ToString();
        }
    }

#pragma warning disable 0649 // filled in by the JSON reader
    [DataContract]
    class InstallerPage
    {
        [DataMember(Name = "messages")] public InstallerMessage[] Messages;
    }

    [DataContract]
    class InstallerMessage
    {
        [DataMember(Name = "text")] public string Text;
        [DataMember(Name = "type")] public string Type;
    }
#pragma warning restore 0649
}
