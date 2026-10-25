using System;
using System.IO;
using System.Threading;

namespace TVFollow
{
    // Settings live in settings.ini as key=value lines, in one of three places:
    //   %APPDATA%\TVFollow         installed (the usual)
    //   "TVFollow data" next to the exe   portable: present only when set up that way
    //   TVFOLLOW_HOME              a trial copy, for trying things out next to a running one
    class Settings
    {
        public const string PortableFolderName = "TVFollow data";

        public static readonly string DefaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TVFollow");
        public static readonly string ExeDir = Path.GetDirectoryName(
            (System.Reflection.Assembly.GetEntryAssembly() ?? typeof(Settings).Assembly).Location);
        public static string PortableDir { get { return Path.Combine(ExeDir, PortableFolderName); } }
        public static string Dir = PickDir(Environment.GetEnvironmentVariable("TVFOLLOW_HOME"), PortableDir, Directory.Exists(PortableDir));

        public static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }
        public static bool IsDefaultDir { get { return SameDir(Dir, DefaultDir); } }
        public static bool IsPortable { get { return SameDir(Dir, PortableDir); } }
        public static bool IsTrial { get { return !IsDefaultDir && !IsPortable; } }

        public static string PickDir(string trialHome, string portableDir, bool portableExists)
        {
            if (!string.IsNullOrEmpty(trialHome)) return trialHome;
            return portableExists ? portableDir : DefaultDir;
        }

        public static bool SameDir(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        // Why portable can't be used from this folder, or null if it can: not from the installed
        // copy's folder, not from Temp (where Windows puts an exe opened straight from a zip),
        // and only where TVFollow can write.
        public static string PortableBlocker(string exeDir, string installDir, string tempDir)
        {
            if (SameDir(exeDir, installDir)) return "This is the installed copy. To use TVFollow portable, open a TVFollow.exe kept in a folder of your own.";
            if (Path.GetFullPath(exeDir).StartsWith(Path.GetFullPath(tempDir), StringComparison.OrdinalIgnoreCase))
                return "TVFollow is running from a temporary folder (opened straight from a zip?). Unzip it into a folder of your own first.";
            try
            {
                string probe = Path.Combine(exeDir, "TVFollow-write-check.tmp");
                File.WriteAllText(probe, "");
                File.Delete(probe);
            }
            catch { return "TVFollow can't save anything in this folder. Move TVFollow.exe to a folder of your own first."; }
            return null;
        }

        public const int DefaultPort = 8060;

        public string Ip = "";
        public string Serial = "";
        public string Input = "hdmi1";
        public string WakeMac = "";
        public string GatewayMac = "";  // the home router the TV was last seen behind, to tell its network from a look-alike
        public int Port = DefaultPort;
        public bool OffWithMonitors = true;
        public bool OnWithMonitors = true;
        public bool StartWithWindows = true;
        public bool DesktopShortcuts;
        public bool CheckUpdates = true;
        public bool Paused;  // set from the tray; kept across restarts until resumed
        public string RemoteLayout = "classic";  // classic, compact or strip

        public bool IsComplete { get { return Ip.Length > 0 && Serial.Length > 0; } }

        // the app id the TV reports while it shows this PC
        public string PcApp { get { return "tvinput." + Input; } }

        // A file another program is holding (an editor, a sync or backup tool) is tried again for
        // a few seconds; the tests shorten the waits.
        public static int ReadRetryMs = 250;
        public static int ReadRetries = 12;

        // why settings.ini couldn't be read, or null. The settings are then the defaults, and
        // Program tells that apart from "not set up yet".
        public string LoadError;

        public static Settings Load()
        {
            var s = new Settings();
            string[] lines = null;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (!File.Exists(FilePath)) return s;
                    lines = File.ReadAllLines(FilePath);
                    break;
                }
                catch (Exception e)
                {
                    if (attempt < ReadRetries) { Thread.Sleep(ReadRetryMs); continue; }
                    Log.Write("couldn't read settings.ini: " + e.Message);
                    s.LoadError = e.Message;
                    return s;
                }
            }
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (line.StartsWith("#") || eq < 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1);
                int comment = value.IndexOf(" #");   // "input=hdmi1   # a note" still reads as hdmi1
                if (comment < 0) comment = value.IndexOf("\t#");
                if (comment >= 0) value = value.Substring(0, comment);
                value = value.Trim();
                switch (key)
                {
                    case "ip": s.Ip = value; break;
                    case "serial": s.Serial = value; break;
                    case "input": s.Input = value.ToLowerInvariant(); break;
                    case "wake_mac":
                    case "mac": s.WakeMac = value; break;
                    case "gateway_mac": s.GatewayMac = value; break;
                    case "port":
                        int port;
                        if (int.TryParse(value, out port) && port > 0 && port < 65536) s.Port = port;
                        break;
                    case "off_with_monitors": s.OffWithMonitors = !IsNo(value); break;
                    case "on_with_monitors": s.OnWithMonitors = !IsNo(value); break;
                    case "start_with_windows": s.StartWithWindows = !IsNo(value); break;
                    case "desktop_shortcuts": s.DesktopShortcuts = !IsNo(value); break;
                    case "check_updates": s.CheckUpdates = !IsNo(value); break;
                    case "paused": s.Paused = !IsNo(value); break;
                    case "remote_layout":
                        value = value.ToLowerInvariant();
                        if (value == "classic" || value == "compact" || value == "strip") s.RemoteLayout = value;
                        break;
                }
            }
            return s;
        }

        static readonly object SaveLock = new object();

        // several threads can save (a moved TV, the tray's Pause), so one at a time
        public void Save()
        {
            lock (SaveLock) SaveNow();
        }

        void SaveNow()
        {
            Directory.CreateDirectory(Dir);
            string text =
                "# TVFollow settings. The setup window writes this file.\r\n" +
                "ip=" + Ip + "\r\n" +
                "serial=" + Serial + "\r\n" +
                "input=" + Input + "\r\n" +
                "wake_mac=" + WakeMac + "\r\n" +
                "gateway_mac=" + GatewayMac + "\r\n" +
                "off_with_monitors=" + (OffWithMonitors ? "yes" : "no") + "\r\n" +
                "on_with_monitors=" + (OnWithMonitors ? "yes" : "no") + "\r\n" +
                "start_with_windows=" + (StartWithWindows ? "yes" : "no") + "\r\n" +
                "desktop_shortcuts=" + (DesktopShortcuts ? "yes" : "no") + "\r\n" +
                "check_updates=" + (CheckUpdates ? "yes" : "no") + "\r\n" +
                "remote_layout=" + RemoteLayout + "\r\n" +
                (Paused ? "paused=yes\r\n" : "") +
                (Port != DefaultPort ? "port=" + Port + "\r\n" : "");
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }

        static bool IsNo(string value)
        {
            value = value.ToLowerInvariant();
            return value == "no" || value == "false" || value == "0" || value == "off";
        }
    }
}
