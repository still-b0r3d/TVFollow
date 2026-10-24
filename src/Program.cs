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
using System.IO;
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
            // "home" is the install folder, or for a portable copy the folder it runs from
            bool home = Settings.IsPortable || Installer.SamePath(Application.ExecutablePath, InstallPaths.ForThisPC().Exe);
            Opening opening = WhatOpens(cmd, settings.IsComplete, Instance.IsRunning(), home);
            if (opening == Opening.Settings)
            {
                Instance.AskToShowSettings(); // the running copy shows its Settings window
                return 0;
            }
            if (opening == Opening.Setup)
            {
                bool portable;
                using (var setup = new SetupForm(settings))
                {
                    if (setup.ShowDialog() != DialogResult.OK) return 0;
                    portable = setup.Portable && !Settings.IsTrial;
                }
                return portable ? StartPortable(Settings.Load()) : InstallAndStart(Settings.Load());
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

        public enum Opening { Follow, Setup, Settings }

        // Opening TVFollow: the setup wizard the first time, and whenever it's opened from anywhere
        // but its home folder (a new download: finishing setup installs it over the old one, or
        // makes it portable). Opened again from home while it runs: the running copy's Settings
        // window. Otherwise it starts following.
        public static Opening WhatOpens(string cmd, bool setUp, bool running, bool home)
        {
            if (cmd == "setup") return Opening.Setup;
            if (cmd.Length > 0) return Opening.Follow; // the other commands are handled on their own
            if (!setUp || !home) return Opening.Setup;
            return running ? Opening.Settings : Opening.Follow;
        }

        // After setup, portable: nothing is installed; this exe follows from where it is.
        static int StartPortable(Settings settings)
        {
            string me = Application.ExecutablePath;
            Installer.ClearDownloadMark(me); // it lives here now, so no security prompt each time it starts
            InstallPaths installed = InstallPaths.Installed();
            if (File.Exists(installed.Exe) && !Installer.SamePath(me, installed.Exe) &&
                MessageBox.Show("TVFollow is also installed on this PC. Remove the installed copy, so only this portable one runs?",
                    "TVFollow", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                StopRunningCopy();
                try
                {
                    Installer.Uninstall(installed, Settings.DefaultDir);
                    Log.Write("removed the installed copy; running portable");
                }
                catch (Exception e) { Log.Write("couldn't remove the installed copy: " + e.Message); }
            }
            Log.Write("running portable from " + Settings.ExeDir);
            return Follow(settings, new Controller(settings), true, true);
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
                Log.Write("couldn't install: " + e.Message);
                bool old = !fromInstalled && File.Exists(paths.Exe);
                if (old) StartFollowing(paths); // the version already installed carries on with the new settings
                MessageBox.Show("TVFollow saved your settings but couldn't install itself: " + e.Message +
                    (old ? "\n\nThe version that was already installed keeps running. Try again later." : ""),
                    "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            Log.Write("installed in " + paths.Dir);
            // a portable copy that's been installed: its folder's settings would make it start
            // portable again next time, so they go
            if (!Settings.IsTrial && !fromInstalled && Directory.Exists(Settings.PortableDir)) Installer.RemoveSettings(Settings.PortableDir);
            if (fromInstalled) return Follow(settings, new Controller(settings), true, true);
            StartFollowing(paths);
            return 0;
        }

        // the installed copy, started in its own folder (not wherever setup was opened from)
        static void StartFollowing(InstallPaths paths)
        {
            Process.Start(new ProcessStartInfo(paths.Exe, "follow") { WorkingDirectory = paths.Dir, UseShellExecute = false });
        }

        static int Uninstall(bool quiet)
        {
            InstallPaths paths = InstallPaths.ForThisPC();
            if (!quiet && MessageBox.Show(
                (paths.Portable ? "Remove TVFollow's settings from this folder?" : "Remove TVFollow from this PC?") +
                "\n\nYour TV will stop turning on and off with the monitors. Nothing on the TV changes.",
                "Remove TVFollow", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
            // only the copy being removed: a portable one shouldn't stop the installed one, or back
            if (IsRunningFrom(paths.Portable ? Application.ExecutablePath : paths.Exe)) StopRunningCopy();
            bool folderLater;
            try { folderLater = Installer.Uninstall(paths, Settings.Dir); }
            catch (Exception e)
            {
                if (!quiet) MessageBox.Show("Couldn't remove everything: " + e.Message, "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 1;
            }
            if (!quiet) MessageBox.Show(paths.Portable ? "TVFollow's settings are removed. Delete TVFollow.exe to finish." : "TVFollow is removed.",
                "TVFollow", MessageBoxButtons.OK, MessageBoxIcon.Information);
            // last of all, once nothing here is waiting on a click: this exe can only be deleted
            // after it has exited
            if (folderLater) Installer.DeleteAfterExit(paths.Dir);
            return 0;
        }

        // is another process running this exact exe?
        static bool IsRunningFrom(string exe)
        {
            int self = Process.GetCurrentProcess().Id;
            foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
            {
                try { if (p.Id != self && Installer.SamePath(p.MainModule.FileName, exe)) return true; }
                catch { }
                finally { p.Dispose(); }
            }
            return false;
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
                        follower.SettingsRequested += tray.ShowSettings;
                        if (greet) tray.Greet();
                        var updates = new UpdateWatcher(tray, settings); // checks only while the setting is on
                        Application.Run();
                        GC.KeepAlive(updates);
                    }
                    GC.KeepAlive(follower);
                }
                finally { mutex.ReleaseMutex(); }
            }
            return 0;
        }
    }
}
