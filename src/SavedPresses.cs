using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace TVFollow
{
    // "Presses saved for you" in the main window: each time TVFollow, following the monitors, did
    // what you'd otherwise have done with the TV's remote. That is: it woke the TV and the TV came
    // on, or it pressed the TV's power on, power off or input for this PC and the TV took the
    // press. The buttons, the remote, the desktop shortcuts and the developer tools don't count
    // (those are presses you made). With several TVs it's one count for them all. Kept in
    // settings.ini: today's, which starts again at midnight, and all of them.
    static class SavedPresses
    {
        // the counts change on the TVs' threads and are read on the windows' thread
        public static readonly object CountLock = new object();

        // the clock, for the day today's count is for (the tests set it)
        public static Func<DateTime> Now = () => DateTime.Now;

        // A press is written to settings.ini this long after it's counted, on a thread of its own,
        // so a settings file another program holds (a sync or backup tool) never holds up a TV on
        // its way to the PC. A burst of presses (a wake, then the switch) is one write. 0 or less:
        // written at once by whoever counted (the tests, so no write lands in a later test).
        public static int WriteAfterMs = 3000;

        static Settings unwritten;   // counted and not written yet (under CountLock)
        static bool writeComing;     // the timer is set for it (under CountLock)
        static bool forgotten;       // this copy is stopping for a new one (under CountLock)
        static readonly object WriteLock = new object();  // one write at a time
        static readonly Timer writer = new Timer(delegate { WriteNow(); });

        // how many times the counts have been written (the tests look)
        internal static int Writes;

        static string Day(DateTime when) { return when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

        // One more, from a TV's thread: counted at once, written a moment later.
        public static void Count(Settings settings)
        {
            bool now;
            lock (CountLock)
            {
                string today = Day(Now());
                if (settings.PressesDay != today)
                {
                    settings.PressesDay = today;
                    settings.PressesToday = 0;
                }
                settings.PressesToday++;
                settings.PressesAllTime++;
                // until the counts are settled (taken from the log), they stay out of settings.ini:
                // the log has this press too, and the next start counts it from there
                if (!settings.PressesKept || forgotten) return;
                unwritten = settings;
                now = WriteAfterMs <= 0;
                if (!now && !writeComing)
                {
                    writeComing = true;
                    writer.Change(WriteAfterMs, Timeout.Infinite);
                }
            }
            if (now) WriteNow();
        }

        // What's counted and not written yet, written now: TVFollow quitting, or Windows shutting
        // down (after the following's own off). Never throws.
        public static void Flush() { WriteNow(); }

        // A new copy is taking over, and it has saved settings of its own: nothing counted here,
        // waiting or still to come as this copy stops, is written over them.
        public static void Forget()
        {
            lock (CountLock)
            {
                forgotten = true;
                unwritten = null;
                writeComing = false;
                writer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        static void WriteNow()
        {
            lock (WriteLock)
            {
                Settings settings;
                lock (CountLock)
                {
                    settings = unwritten;
                    unwritten = null;
                    writeComing = false;
                    writer.Change(Timeout.Infinite, Timeout.Infinite);
                }
                if (settings == null) return;
                try
                {
                    settings.Save();
                    Writes++;
                }
                catch (Exception e)
                {
                    Log.Write("couldn't save the presses saved: " + e.Message);
                    // tried again with the next press, or when TVFollow quits
                    lock (CountLock) { if (unwritten == null && !forgotten) unwritten = settings; }
                }
            }
        }

        // today's and all of them, as the main window shows them (today's is 0 once the day is over)
        public static void Read(Settings settings, out int today, out int allTime)
        {
            lock (CountLock)
            {
                today = settings.PressesDay == Day(Now()) ? settings.PressesToday : 0;
                allTime = settings.PressesAllTime;
            }
        }

        // The first time a TVFollow that counts runs, there are no counts yet. They come from
        // TVFollow's log, which has a line for each of those presses since 1.0 (as far back as it
        // goes: it keeps about 512 KB, the older half in TVFollow.log.old). Only once: from then
        // on the counts are in settings.ini. A log that can't be read (another program holding
        // it) is tried again for a few seconds, like settings.ini, and then at the next start;
        // meanwhile the presses counted are only shown, never written. The log has their lines
        // too, so its numbers then take their place: nothing is counted twice.
        public static void FromLogOnce(Settings settings)
        {
            if (settings.PressesKept) return;
            List<string> lines = null;
            string why = null;
            for (int attempt = 1; lines == null; attempt++)
            {
                try
                {
                    var read = new List<string>();
                    foreach (string file in new[] { Log.FilePath + ".old", Log.FilePath })
                        if (File.Exists(file)) read.AddRange(ReadLines(file));
                    lines = read;
                }
                catch (Exception e)
                {
                    if (attempt >= Settings.ReadRetries) { why = e.Message; break; }
                    Thread.Sleep(Settings.ReadRetryMs);
                }
            }
            if (lines == null)
            {
                Log.Write("couldn't read the log for the presses saved, so they're counted from it at the next start: " + why);
                return;
            }
            int today, all;
            // the day as the log writes it (in this PC's own calendar)
            FromLog(lines, Now().ToString("yyyy-MM-dd"), out today, out all);
            lock (CountLock)
            {
                settings.PressesAllTime = all;
                settings.PressesToday = today;
                settings.PressesDay = Day(Now());
                settings.PressesKept = true;
            }
            // (written after the log is read, so this line never counts)
            Log.Write("presses saved so far, from the log: " + all + " (" + today + " today)");
            try { settings.Save(); }
            catch (Exception e) { Log.Write("couldn't save the settings: " + e.Message); }
        }

        // the log as it is, without keeping TVFollow from writing to it meanwhile
        static List<string> ReadLines(string path)
        {
            var lines = new List<string>();
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(file, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
            }
            return lines;
        }

        // What the log's lines add up to, by the rule the counting itself follows. A line is the
        // time, two spaces, then what happened: "2026-10-23 23:21:00  on: TV woke up (PowerOn)".
        // The following's own lines start "on: " and "off: " (the buttons' start "on (button)",
        // "pc" and "off (button)"); with several TVs, after the TV's name: "Den: on: …". The lines,
        // as TVFollow has written them since 1.0:
        // - "off: power off -> 200", "on: power on (was DisplayOff) -> 200" and "on: switching to
        //   the PC -> 200" count when the TV took the press (2xx). (Up to 1.2.1 only the first
        //   press of the input had a line; since 1.3 the first one the TV took has one too.)
        // - "on: TV woke up (PowerOn)" counts when a wake packet went out ("TV not answering, sent
        //   a wake packet") and the TV came up on its home screen or an input, which the TV's next
        //   "on: " line tells: "TV is on and showing 12, leaving it alone" means an app was on, so
        //   the TV was only slow to answer. Woken into standby ("(DisplayOff)") it isn't on yet: the
        //   power on after it is the press. (Since 1.3 a TV that answers right after the packet
        //   says "TV answered right away" instead; before, the log can't tell it from a wake.)
        // today: the day as the log writes it.
        public static void FromLog(IEnumerable<string> lines, string today, out int todayCount, out int allTime)
        {
            todayCount = allTime = 0;
            var packet = new Dictionary<string, bool>();   // a TV's last wake: did a packet go out?
            var woke = new Dictionary<string, bool>();     // a TV's wake that its next line settles: today's?
            foreach (string line in lines)
            {
                int gap = line.IndexOf("  ", StringComparison.Ordinal);
                if (gap < 10) continue;
                string tv, said;
                bool on;
                if (!Following(line.Substring(gap + 2), out tv, out on, out said)) continue;
                bool isToday = line.StartsWith(today + " ", StringComparison.Ordinal);
                int counted = 0;
                if (!on)
                {
                    if (Took(said, "power off -> ")) counted = 1;
                }
                else
                {
                    bool wokeToday;
                    if (woke.TryGetValue(tv, out wokeToday))
                    {
                        woke.Remove(tv);
                        if (!LeftAnApp(said)) { allTime++; if (wokeToday) todayCount++; }
                    }
                    if (said.StartsWith("TV not answering, ", StringComparison.Ordinal))
                        packet[tv] = said.StartsWith("TV not answering, sent a wake packet", StringComparison.Ordinal);
                    else
                    {
                        // (the wake's lines come one after the other: "TV woke up" is the line after)
                        bool sent;
                        if (!packet.TryGetValue(tv, out sent)) sent = false;
                        packet.Remove(tv);
                        if (said == "TV woke up (PowerOn)") { if (sent) woke[tv] = isToday; }
                        else if (Took(said, "power on (was ") || Took(said, "switching to the PC -> ")) counted = 1;
                    }
                }
                allTime += counted;
                if (isToday) todayCount += counted;
            }
            // a wake whose next line never came (the log ends there) counted as it happened
            foreach (bool wokeToday in woke.Values)
            {
                allTime++;
                if (wokeToday) todayCount++;
            }
        }

        // One of the following's own lines: which TV ("" with one TV), on or off, and the rest.
        static bool Following(string message, out string tv, out bool on, out string said)
        {
            tv = said = null;
            on = false;
            int onAt = Tag(message, "on: "), offAt = Tag(message, "off: ");
            if (onAt < 0 && offAt < 0) return false;
            on = onAt >= 0 && (offAt < 0 || onAt < offAt);
            int at = on ? onAt : offAt;
            tv = at == 0 ? "" : message.Substring(0, at - 2);
            said = message.Substring(at + (on ? 4 : 5));
            return true;
        }

        // where the tag starts: at the start, or after a TV's name and ": "
        static int Tag(string message, string tag)
        {
            if (message.StartsWith(tag, StringComparison.Ordinal)) return 0;
            int at = message.IndexOf(": " + tag, StringComparison.Ordinal);
            return at < 0 ? -1 : at + 2;
        }

        // "<start><anything> -> 2xx": a press the TV took
        static bool Took(string said, string start)
        {
            if (!said.StartsWith(start, StringComparison.Ordinal)) return false;
            int arrow = said.LastIndexOf(" -> ", StringComparison.Ordinal);
            int code;
            return arrow >= 0 && int.TryParse(said.Substring(arrow + 4).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out code) &&
                code >= 200 && code <= 299;
        }

        // the following leaving a woken TV alone because an app was on it (not the home screen or an input)
        static bool LeftAnApp(string said)
        {
            const string Start = "TV is on and showing ", End = ", leaving it alone";
            if (!said.StartsWith(Start, StringComparison.Ordinal) || !said.EndsWith(End, StringComparison.Ordinal)) return false;
            string app = said.Substring(Start.Length, said.Length - Start.Length - End.Length);
            return app != "the Roku home screen" && !app.StartsWith("tvinput.", StringComparison.Ordinal);
        }
    }
}
