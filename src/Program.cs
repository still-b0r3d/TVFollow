// TVFollow: turns a TV with Roku built in on and off along with this PC's monitors.
//
//   TVFollow.exe            set up on first run, then follow the monitors in the background;
//                           opened again while running, it shows the setup window
//   TVFollow.exe setup      show the setup window
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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Settings settings = Settings.Load();
            bool takeOver = false;
            if (cmd == "setup" || (cmd.Length == 0 && (!settings.IsComplete || Instance.IsRunning())))
            {
                using (var setup = new SetupForm(settings))
                {
                    if (setup.ShowDialog() != DialogResult.OK) return 0;
                }
                settings = Settings.Load();
                cmd = "";
                takeOver = true; // a copy that's already running has the old settings
            }

            if (!settings.IsComplete)
            {
                MessageBox.Show("TVFollow isn't set up yet. Open TVFollow to set it up.", "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show("Unknown command: " + args[0] + "\n\nUse setup, on, off, pc or --version.", "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }

            return Follow(settings, controller, takeOver);
        }

        static int Follow(Settings settings, Controller controller, bool takeOver)
        {
            using (var mutex = new Mutex(false, Instance.MutexName))
            {
                if (!Instance.Acquire(mutex, 0))
                {
                    if (!takeOver) return 0;
                    Instance.AskToQuit();
                    if (!Instance.Acquire(mutex, 10000))
                    {
                        MessageBox.Show("TVFollow saved your settings, but the copy that's already running didn't stop. " +
                            "Restart the PC, or end TVFollow in Task Manager and open it again.", "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return 1;
                    }
                }
                try
                {
                    Log.Write("started " + Version + ", following the monitors");
                    var follower = new Follower(settings, controller);
                    follower.Start();
                    Application.Run();
                    GC.KeepAlive(follower);
                }
                finally { mutex.ReleaseMutex(); }
            }
            return 0;
        }
    }
}
