using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace TVFollow
{
    // One TV that follows the monitors: where it is, how to tell it from other Rokus, and the
    // input this PC is plugged into.
    class TvSettings
    {
        public string Ip = "";
        public string Serial = "";
        public string Input = "hdmi1";
        public string WakeMac = "";
        public string GatewayMac = "";  // the home router the TV was last seen behind, to tell its network from a look-alike
        public string Name = "";        // what the TV calls itself, for the menus and the log

        public bool IsComplete { get { return Ip.Length > 0 && Serial.Length > 0; } }

        // the app id the TV reports while it shows this PC
        public string PcApp { get { return "tvinput." + Input; } }

        public TvSettings Copy() { return (TvSettings)MemberwiseClone(); }
    }

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
        public const int MaxTvs = 99;

        // The TVs that follow the monitors, in the order the Settings window numbers them. The
        // first keeps 1.1's keys (ip=, serial=, ...), so a 1.1 copy reading this file still finds
        // it; the others are tv2.ip=, tv2.serial= and so on, which 1.1 passes over.
        public readonly List<TvSettings> Tvs = new List<TvSettings>();
        public int Port = DefaultPort;  // the same for every TV (8060 on any real Roku)
        public bool OffWithMonitors = true;
        public bool OnWithMonitors = true;
        public bool StartWithWindows = true;
        public bool DesktopShortcuts;
        public bool CheckUpdates = true;
        public bool Paused;  // set from the tray; kept across restarts until resumed
        public string RemoteLayout = "classic";  // classic, compact or strip

        public bool IsComplete { get { return Tvs.Count > 0 && Tvs.TrueForAll(t => t.IsComplete); } }

        public bool SeveralTvs { get { return Tvs.Count > 1; } }

        // What a TV is called in the menus and the log: its own name, or "TV 2" without one. Two
        // TVs that go by the same name get their numbers added, "Roku TV (1)" and "Roku TV (2)".
        public string LabelOf(TvSettings tv)
        {
            int number = Tvs.IndexOf(tv) + 1;
            string name = BaseLabel(tv, number);
            for (int i = 0; i < Tvs.Count; i++)
                if (Tvs[i] != tv && string.Equals(BaseLabel(Tvs[i], i + 1), name, StringComparison.OrdinalIgnoreCase)) return name + " (" + number + ")";
            return name;
        }

        static string BaseLabel(TvSettings tv, int number)
        {
            string name = tv.Name.Trim();
            if (name.Length == 0) return "TV " + number;
            return name.Length > 40 ? name.Substring(0, 39) + "…" : name;
        }

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
            TvSettings first = null;
            var more = new SortedDictionary<int, TvSettings>();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (line.StartsWith("#") || eq < 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string whole = line.Substring(eq + 1).Trim();
                string value = whole;
                int comment = value.IndexOf(" #");   // "input=hdmi1   # a note" still reads as hdmi1
                if (comment < 0) comment = value.IndexOf("\t#");
                if (comment >= 0) value = value.Substring(0, comment);
                value = value.Trim();

                int number;
                string field = TvKey(key, out number);
                if (field != null)
                {
                    TvSettings tv;
                    if (number == 1) tv = first ?? (first = new TvSettings());
                    else if (!more.TryGetValue(number, out tv)) more[number] = tv = new TvSettings();
                    // a TV's name can have a # in it ("Den #2"), so it's read whole
                    ReadTv(tv, field, field == "name" ? whole : value, key);
                    continue;
                }
                switch (key)
                {
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
            if (first != null) s.Tvs.Add(first);
            foreach (TvSettings tv in more.Values)
            {
                // the same TV twice would have every button pressed on it twice
                if (tv.Serial.Length > 0 && s.Tvs.Exists(t => t.Serial == tv.Serial))
                {
                    Log.Write("settings: a TV is listed twice; the second one is ignored");
                    continue;
                }
                s.Tvs.Add(tv);
            }
            return s;
        }

        // "ip" is the first TV's address and "tv2.ip" the second's. Returns the key without its
        // "tvN." and the TV's number, or null for a key that isn't about a TV.
        static string TvKey(string key, out int number)
        {
            number = 1;
            int dot = key.IndexOf('.');
            if (key.StartsWith("tv") && dot > 2)
            {
                if (!int.TryParse(key.Substring(2, dot - 2), NumberStyles.None, CultureInfo.InvariantCulture, out number) ||
                    number < 1 || number > MaxTvs) return null;
                key = key.Substring(dot + 1);
            }
            switch (key)
            {
                case "ip": case "serial": case "input": case "wake_mac": case "mac": case "gateway_mac": case "name": return key;
                default: return null;
            }
        }

        static void ReadTv(TvSettings tv, string field, string value, string key)
        {
            switch (field)
            {
                case "ip":
                    // the address goes straight into a URL, so only an IPv4 address will do
                    uint ipValue;
                    if (LocalNetwork.TryParse(value, out ipValue)) tv.Ip = value;
                    else if (value.Length > 0) Log.Write("settings: " + key + "=" + value + " isn't an IPv4 address, ignored");
                    break;
                case "serial": tv.Serial = value; break;
                case "input": tv.Input = value.ToLowerInvariant(); break;
                case "wake_mac":
                case "mac": tv.WakeMac = value; break;
                case "gateway_mac": tv.GatewayMac = value; break;
                case "name": tv.Name = value; break;
            }
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
            var text = new StringBuilder("# TVFollow settings. The setup window writes this file.\r\n");
            if (Tvs.Count > 0) WriteTv(text, "", Tvs[0]);
            text.Append(
                "off_with_monitors=" + (OffWithMonitors ? "yes" : "no") + "\r\n" +
                "on_with_monitors=" + (OnWithMonitors ? "yes" : "no") + "\r\n" +
                "start_with_windows=" + (StartWithWindows ? "yes" : "no") + "\r\n" +
                "desktop_shortcuts=" + (DesktopShortcuts ? "yes" : "no") + "\r\n" +
                "check_updates=" + (CheckUpdates ? "yes" : "no") + "\r\n" +
                "remote_layout=" + OneLine(RemoteLayout) + "\r\n" +
                (Paused ? "paused=yes\r\n" : "") +
                (Port != DefaultPort ? "port=" + Port + "\r\n" : ""));
            if (Tvs.Count > 1) text.Append("# the other TVs\r\n");
            for (int i = 1; i < Tvs.Count; i++) WriteTv(text, "tv" + (i + 1) + ".", Tvs[i]);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, text.ToString());
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }

        static void WriteTv(StringBuilder text, string prefix, TvSettings tv)
        {
            text.Append(
                prefix + "ip=" + OneLine(tv.Ip) + "\r\n" +
                prefix + "serial=" + OneLine(tv.Serial) + "\r\n" +
                prefix + "input=" + OneLine(tv.Input) + "\r\n" +
                prefix + "wake_mac=" + OneLine(tv.WakeMac) + "\r\n" +
                prefix + "gateway_mac=" + OneLine(tv.GatewayMac) + "\r\n" +
                prefix + "name=" + OneLine(tv.Name) + "\r\n");
        }

        // Some values come from the TV's own answers (its serial, its network card, its input
        // names). A line break in one would start a line of its own in the file and could set
        // ip=, serial= or port= behind the user's back, so control characters never get written.
        static string OneLine(string value)
        {
            var line = new System.Text.StringBuilder();
            foreach (char c in value ?? "") if (c >= ' ') line.Append(c);
            return line.ToString();
        }

        static bool IsNo(string value)
        {
            value = value.ToLowerInvariant();
            return value == "no" || value == "false" || value == "0" || value == "off";
        }
    }
}
