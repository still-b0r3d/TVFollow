using System;
using System.IO;

namespace TVFollow
{
    // Settings live in %APPDATA%\TVFollow\settings.ini as key=value lines.
    // TVFOLLOW_HOME points them somewhere else, for trying things out next to a running copy.
    class Settings
    {
        public static readonly string DefaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TVFollow");
        public static string Dir = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TVFOLLOW_HOME"))
            ? DefaultDir : Environment.GetEnvironmentVariable("TVFOLLOW_HOME");
        public static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }
        public static bool IsDefaultDir { get { return string.Equals(Path.GetFullPath(Dir).TrimEnd('\\'), DefaultDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); } }

        public const int DefaultPort = 8060;

        public string Ip = "";
        public string Serial = "";
        public string Input = "hdmi1";
        public string WakeMac = "";
        public int Port = DefaultPort;
        public bool OffWithMonitors = true;
        public bool OnWithMonitors = true;
        public bool StartWithWindows = true;
        public bool DesktopShortcuts;
        public bool CheckUpdates = true;
        public bool Paused;  // set from the tray; kept across restarts until resumed

        public bool IsComplete { get { return Ip.Length > 0 && Serial.Length > 0; } }

        // the app id the TV reports while it shows this PC
        public string PcApp { get { return "tvinput." + Input; } }

        public static Settings Load()
        {
            var s = new Settings();
            if (!File.Exists(FilePath)) return s;
            foreach (string raw in File.ReadAllLines(FilePath))
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
                "off_with_monitors=" + (OffWithMonitors ? "yes" : "no") + "\r\n" +
                "on_with_monitors=" + (OnWithMonitors ? "yes" : "no") + "\r\n" +
                "start_with_windows=" + (StartWithWindows ? "yes" : "no") + "\r\n" +
                "desktop_shortcuts=" + (DesktopShortcuts ? "yes" : "no") + "\r\n" +
                "check_updates=" + (CheckUpdates ? "yes" : "no") + "\r\n" +
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
