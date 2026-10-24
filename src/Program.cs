// TVFollow: turns a TV with Roku built in on and off along with this PC's monitors.
//
//   TVFollow.exe            set up on first run, then follow the monitors from the taskbar
//                           corner; opened again while running, it shows the setup window
//   TVFollow.exe setup      show the setup window
//   TVFollow.exe on         turn the TV on (whatever it shows)
//   TVFollow.exe pc         turn the TV on and switch it to this PC
//   TVFollow.exe off        turn the TV off
//   TVFollow.exe uninstall  remove TVFollow from this PC (--quiet: don't ask)
//   TVFollow.exe --version  print the version
// Used by TVFollow itself: --startup (at sign-in, quietly) and follow (after setup).
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow
{
    static class Program
    {
        public const string Version = "1.0";

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

            if (cmd == "uninstall") return Uninstall(args.Length > 1 && args[1].ToLowerInvariant() == "--quiet");

            Settings settings = Settings.Load();
            if (cmd == "setup" || (cmd.Length == 0 && (!settings.IsComplete || Instance.IsRunning())))
            {
                using (var setup = new SetupForm(settings))
                {
                    if (setup.ShowDialog() != DialogResult.OK) return 0;
                }
                return InstallAndStart(Settings.Load());
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

            if (cmd == "--startup") return Follow(settings, controller, false, false);
            if (cmd == "follow") return Follow(settings, controller, true, true);
            if (cmd.Length == 0) return Follow(settings, controller, false, true);

            MessageBox.Show("Unknown command: " + args[0] + "\n\nUse setup, on, off, pc, uninstall or --version.", "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 2;
        }

        // After setup: put the exe in its folder (replacing an older one), then follow from there.
        static int InstallAndStart(Settings settings)
        {
            InstallPaths paths = InstallPaths.ForThisPC();
            string me = Application.ExecutablePath;
            bool fromInstalled = Installer.SamePath(me, paths.Exe);
            if (!fromInstalled && !StopRunningCopy())
            {
                MessageBox.Show("TVFollow saved your settings, but the copy that's already running didn't stop, so it couldn't be updated. " +
                    "Restart the PC, or end TVFollow in Task Manager and open it again.", "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
            try { Installer.Install(paths, settings, me); }
            catch (Exception e)
            {
                MessageBox.Show("TVFollow saved your settings but couldn't install itself: " + e.Message, "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            Log.Write("installed in " + paths.Dir);
            if (fromInstalled) return Follow(settings, new Controller(settings), true, true);
            Process.Start(paths.Exe, "follow");
            return 0;
        }

        static int Uninstall(bool quiet)
        {
            if (!quiet && MessageBox.Show("Remove TVFollow from this PC?\n\nYour TV will stop turning on and off with the monitors. Nothing on the TV changes.",
                "Remove TVFollow", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
            StopRunningCopy();
            try { Installer.Uninstall(InstallPaths.ForThisPC()); }
            catch (Exception e)
            {
                if (!quiet) MessageBox.Show("Couldn't remove everything: " + e.Message, "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
            if (!quiet) MessageBox.Show("TVFollow is removed.", "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        static bool StopRunningCopy()
        {
            if (!Instance.IsRunning()) return true;
            Instance.AskToQuit();
            for (int i = 0; i < 100 && Instance.IsRunning(); i++) Thread.Sleep(100);
            return !Instance.IsRunning();
        }

        static int Follow(Settings settings, Controller controller, bool takeOver, bool greet)
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
                    using (var tray = new Tray(follower, controller))
                    {
                        if (greet) tray.Greet();
                        Application.Run();
                    }
                    GC.KeepAlive(follower);
                }
                finally { mutex.ReleaseMutex(); }
            }
            return 0;
        }
    }
}
