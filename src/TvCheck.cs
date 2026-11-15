using System;
using System.Collections.Generic;

namespace TVFollow
{
    // How a TV is right now: whether it answers, whether it's on, and what it shows. The Settings
    // window's TV page and the main window both ask this way, and say it in their own words.
    // Ask talks to the TV (a few seconds at most when nothing answers), so it runs off the
    // windows' thread.
    class TvCheck
    {
        public enum Kind { NoAnswer, OtherRoku, Limited, Off, OnPc, OnHome, OnOther }

        public DeviceInfo Info;   // what the TV said about itself; null when nothing answered
        public List<KeyValuePair<string, string>> Inputs;   // its HDMI inputs, when they were asked for
        public string Showing;    // what's on its screen: an app's or input's id, "" for Roku's home screen
        public Kind State;

        public static TvCheck Ask(TvSettings tv, int port, bool inputs)
        {
            var check = new TvCheck();
            var client = new RokuClient(tv.Ip, port);
            check.Info = client.GetDeviceInfo(2500);
            if (check.Info != null)
            {
                if (inputs) check.Inputs = client.GetHdmiInputs(2500);
                check.Showing = client.GetActiveApp(2500);
            }
            check.State = KindOf(tv, check.Info, check.Showing);
            return check;
        }

        static Kind KindOf(TvSettings tv, DeviceInfo info, string showing)
        {
            if (info == null) return Kind.NoAnswer;
            // the router gave the TV's address to something else: the TV gets looked for when
            // TVFollow next needs it
            if (info.Serial != tv.Serial) return Kind.OtherRoku;
            if (!info.AllowsControl) return Kind.Limited;
            if (!info.IsOn) return Kind.Off;
            if (showing == tv.PcApp) return Kind.OnPc;
            if (showing == "") return Kind.OnHome;
            return Kind.OnOther;
        }

        // ✔ (true), ⚠ (false), or neither (null)
        public static bool? Mark(Kind kind)
        {
            if (kind == Kind.OnPc) return true;
            if (kind == Kind.NoAnswer || kind == Kind.OtherRoku || kind == Kind.Limited) return false;
            return null;
        }

        // the Settings window's words, under the line with the TV's name
        public static string Words(Kind kind)
        {
            switch (kind)
            {
                case Kind.NoAnswer: return "Not answering right now. Is it on, and on this network?";
                case Kind.OtherRoku: return "Another Roku answers at this address right now. TVFollow looks for your TV by itself when it needs it.";
                case Kind.Limited:
                    return "Network access is on Limited, so TVFollow can't press its buttons. On the TV: Settings → System → " +
                        "Advanced system settings → Control by mobile apps → Network access → Enabled.";
                case Kind.Off: return "Off right now.";
                case Kind.OnPc: return "On, showing this PC.";
                case Kind.OnHome: return "On, showing Roku's home screen.";
                default: return "On, showing something else.";
            }
        }

        // the main window's: one sentence that names the TV ("Living Room TV is on, showing this PC.")
        public static string Sentence(Kind kind, string tv)
        {
            switch (kind)
            {
                case Kind.NoAnswer: return tv + " isn't answering right now.";
                case Kind.OtherRoku: return "Another Roku answers at " + tv + "'s address right now.";
                case Kind.Limited: return tv + "'s Network access is on Limited, so TVFollow can't press its buttons.";
                case Kind.Off: return tv + " is off.";
                case Kind.OnPc: return tv + " is on, showing this PC.";
                case Kind.OnHome: return tv + " is on, showing Roku's home screen.";
                default: return tv + " is on, showing something else.";
            }
        }

        // the words with their mark in front, as both windows write them
        public static string Marked(bool? ok, string text)
        {
            return (ok == null ? "" : ok.Value ? "✔  " : "⚠  ") + text;
        }
    }
}
