// TVFollow: turns a TV with Roku built in on and off along with this PC's monitors.
//
//   TVFollow.exe            follow the monitors in the background
//   TVFollow.exe on         turn the TV on (whatever it shows)
//   TVFollow.exe pc         turn the TV on and switch it to this PC
//   TVFollow.exe off        turn the TV off
//   TVFollow.exe --version  print the version
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow
{
    static class Program
    {
        public const string Version = "0.1.0";

        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int processId);

        [STAThread]
        static int Main(string[] args)
        {
            string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            if (cmd == "--version" || cmd == "-v")
            {
                AttachConsole(-1); // print into the window it was started from
                Console.WriteLine("TVFollow " + Version);
                return 0;
            }

            Settings settings = Settings.Load();
            if (!settings.IsComplete)
            {
                MessageBox.Show("TVFollow isn't set up yet.", "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }

            var controller = new Controller(settings);
            try
            {
                if (cmd == "on") { controller.TurnOn(TurnOnMode.PowerOnly); return 0; }
                if (cmd == "pc") { controller.TurnOn(TurnOnMode.ToPc); return 0; }
                if (cmd == "off") { controller.TurnOff(true, false); return 0; }
            }
            catch (Exception e) { Log.Write(cmd + ": error: " + e.Message); return 1; }
            if (cmd.Length > 0)
            {
                MessageBox.Show("Unknown command: " + args[0] + "\n\nUse on, off, pc or --version.", "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }

            bool first;
            using (var mutex = new Mutex(true, "Local\\TVFollow", out first))
            {
                if (!first) return 0;
                Log.Write("started " + Version + ", following the monitors");
                var follower = new Follower(settings, controller);
                follower.Start();
                Application.Run();
                GC.KeepAlive(follower);
            }
            return 0;
        }
    }
}
