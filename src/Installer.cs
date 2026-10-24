using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace TVFollow
{
    // Where TVFollow puts itself. Everything is per user, so it never needs admin.
    class InstallPaths
    {
        public string Dir;            // the program folder
        public string StartMenuLink;  // Start menu shortcut
        public string DesktopDir;     // where the optional TV shortcuts go
        public string RegistryRoot;   // under HKEY_CURRENT_USER; null = leave the registry alone
        public bool Portable;         // runs from the user's own folder; nothing is installed

        public string Exe { get { return Path.Combine(Dir, "TVFollow.exe"); } }
        public string IconDir { get { return Path.Combine(Dir, "icons"); } }
        public string RunKey { get { return RegistryRoot + @"\Microsoft\Windows\CurrentVersion\Run"; } }
        public string UninstallKey { get { return RegistryRoot + @"\Microsoft\Windows\CurrentVersion\Uninstall\TVFollow"; } }

        public static InstallPaths ForThisPC()
        {
            if (Settings.IsTrial)
            {
                // a trial copy (TVFOLLOW_HOME) keeps everything inside its own folder
                return new InstallPaths
                {
                    Dir = Path.Combine(Settings.Dir, "Program"),
                    StartMenuLink = Path.Combine(Settings.Dir, "Start menu", "TVFollow.lnk"),
                    DesktopDir = Path.Combine(Settings.Dir, "Desktop")
                };
            }
            if (Settings.IsPortable) return new InstallPaths { Dir = Settings.ExeDir, Portable = true };
            return Installed();
        }

        // where the installed copy lives, whatever mode this copy runs in
        public static InstallPaths Installed()
        {
            return new InstallPaths
            {
                Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TVFollow"),
                StartMenuLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "TVFollow.lnk"),
                DesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                RegistryRoot = "Software"
            };
        }
    }

    static class Installer
    {
        public const string RunValue = "TVFollow";
        static readonly string[] IconNames = { "tv.ico", "tv-paused.ico", "tv-on.ico", "tv-off.ico", "tv-pc.ico" };

        // the optional desktop shortcuts: file name, command, icon
        static readonly string[][] DesktopLinks =
        {
            new[] { "TV on.lnk", "on", "tv-on.ico", "Turn the TV on" },
            new[] { "TV off.lnk", "off", "tv-off.ico", "Turn the TV off" },
            new[] { "TV to PC.lnk", "pc", "tv-pc.ico", "Turn the TV on and switch it to this PC" }
        };

        public static bool SamePath(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        // Copies the exe in (unless it's already running from there) and sets up the shortcuts,
        // start-with-Windows and the Apps list entry to match the settings.
        // The copy that's already running must be stopped first, or its exe can't be replaced.
        public static void Install(InstallPaths p, Settings s, string sourceExe)
        {
            Directory.CreateDirectory(p.Dir);
            Directory.CreateDirectory(p.IconDir);
            if (!SamePath(sourceExe, p.Exe)) CopyExe(sourceExe, p.Exe);
            ClearDownloadMark(p.Exe);
            foreach (string name in IconNames) WriteResource(name, Path.Combine(p.IconDir, name));

            Directory.CreateDirectory(Path.GetDirectoryName(p.StartMenuLink));
            Shortcut.Create(p.StartMenuLink, p.Exe, "", Path.Combine(p.IconDir, "tv.ico"), "TVFollow: open its settings");

            foreach (string[] link in DesktopLinks)
            {
                string path = Path.Combine(p.DesktopDir, link[0]);
                if (s.DesktopShortcuts)
                {
                    Directory.CreateDirectory(p.DesktopDir);
                    Shortcut.Create(path, p.Exe, link[1], Path.Combine(p.IconDir, link[2]), link[3]);
                }
                else DeleteFile(path);
            }

            if (p.RegistryRoot == null) return;
            using (RegistryKey run = Registry.CurrentUser.CreateSubKey(p.RunKey))
            {
                if (s.StartWithWindows) run.SetValue(RunValue, "\"" + p.Exe + "\" --startup");
                else run.DeleteValue(RunValue, false);
            }
            using (RegistryKey app = Registry.CurrentUser.CreateSubKey(p.UninstallKey))
            {
                app.SetValue("DisplayName", "TVFollow");
                app.SetValue("DisplayVersion", Program.Version);
                app.SetValue("Publisher", "b0r3d.org");
                app.SetValue("DisplayIcon", Path.Combine(p.IconDir, "tv.ico"));
                app.SetValue("InstallLocation", p.Dir);
                app.SetValue("UninstallString", "\"" + p.Exe + "\" uninstall");
                app.SetValue("QuietUninstallString", "\"" + p.Exe + "\" uninstall --quiet");
                app.SetValue("URLInfoAbout", SetupForm.HelpUrl);
                app.SetValue("HelpLink", SetupForm.HelpUrl);
                app.SetValue("NoModify", 1, RegistryValueKind.DWord);
                app.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                app.SetValue("EstimatedSize", (int)(new FileInfo(p.Exe).Length / 1024) + 300, RegistryValueKind.DWord);
            }
        }

        // Copying over an exe that something still has open (a TV shortcut mid-wake, a second
        // settings window) fails for a moment, so it tries for about five seconds.
        static void CopyExe(string source, string target)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { File.Copy(source, target, true); return; }
                catch (IOException) { if (attempt >= 20) throw; }
                catch (UnauthorizedAccessException) { if (attempt >= 20) throw; }
                Thread.Sleep(250);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool DeleteFileW(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFileAttributesW(string path);

        // Windows marks downloads as "from the internet" in a hidden Zone.Identifier stream, and
        // File.Copy carries it along. The installed copy drops it, so starting TVFollow at
        // sign-in or from its shortcuts never brings up a security prompt.
        public static void ClearDownloadMark(string path)
        {
            DeleteFileW(path + ":Zone.Identifier");
        }

        public static bool HasDownloadMark(string path)
        {
            return GetFileAttributesW(path + ":Zone.Identifier") != 0xFFFFFFFF;
        }

        // Takes everything back out: registry, shortcuts, the settings and log in settingsDir,
        // and the program folder. Returns true if the program folder still has to go once this
        // copy has exited (it's running from inside it): call DeleteAfterExit as the very last
        // thing. A portable copy only loses its settings: its exe and folder are the user's.
        public static bool Uninstall(InstallPaths p, string settingsDir)
        {
            bool later = false;
            if (p.Portable)
            {
                RemoveSettings(settingsDir);
                return false;
            }
            if (p.RegistryRoot != null)
            {
                using (RegistryKey run = Registry.CurrentUser.OpenSubKey(p.RunKey, true))
                    if (run != null) run.DeleteValue(RunValue, false);
                Registry.CurrentUser.DeleteSubKeyTree(p.UninstallKey, false);
            }
            DeleteFile(p.StartMenuLink);
            foreach (string[] link in DesktopLinks) DeleteFile(Path.Combine(p.DesktopDir, link[0]));
            if (p.RegistryRoot == null)
            {
                // a trial copy's stand-in folders; never the real Start menu or desktop
                DeleteEmptyDir(Path.GetDirectoryName(p.StartMenuLink), "Start menu");
                DeleteEmptyDir(p.DesktopDir, "Desktop");
            }

            // only ever delete a folder that is clearly TVFollow's own
            if (File.Exists(p.Exe) && (Path.GetFileName(p.Dir) == "TVFollow" || Path.GetFileName(p.Dir) == "Program"))
            {
                if (SamePath(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), p.Dir)) later = true;
                else Directory.Delete(p.Dir, true);
            }
            RemoveSettings(settingsDir);
            return later;
        }

        // TVFollow's own files in a settings folder, then the folder if that left it empty
        public static void RemoveSettings(string dir)
        {
            foreach (string name in new[] { "settings.ini", "settings.ini.tmp", "updates.ini", "TVFollow.log", "TVFollow.log.old" })
                DeleteFile(Path.Combine(dir, name));
            DeleteEmptyDir(dir, null);
        }

        // A hidden Command Prompt removes the folder once this copy has exited, trying for about
        // fifteen seconds. The folder goes in through an environment variable, so nothing in the
        // path is read as a command, and the prompt starts in Temp so it doesn't sit in the folder.
        public static void DeleteAfterExit(string dir)
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                "/d /c for /l %i in (1,1,15) do (ping -n 2 127.0.0.1 >nul & rmdir /s /q \"%TVF_DIR%\" 2>nul & if not exist \"%TVF_DIR%\" exit /b 0)")
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath()
            };
            psi.EnvironmentVariables["TVF_DIR"] = dir;
            Process.Start(psi);
        }

        static void WriteResource(string name, string path)
        {
            using (Stream res = Assembly.GetExecutingAssembly().GetManifestResourceStream("TVFollow." + name))
            {
                if (res == null) return;
                using (FileStream file = File.Create(path)) res.CopyTo(file);
            }
        }

        static void DeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // only if it's empty, and (when a name is given) only if it has that name
        static void DeleteEmptyDir(string dir, string mustBeNamed)
        {
            try
            {
                if (!Directory.Exists(dir) || Directory.GetFileSystemEntries(dir).Length > 0) return;
                if (mustBeNamed != null && Path.GetFileName(dir) != mustBeNamed) return;
                Directory.Delete(dir);
            }
            catch { }
        }
    }

    // .lnk files through the Windows Script Host, so no extra libraries are needed.
    static class Shortcut
    {
        public static void Create(string path, string target, string arguments, string icon, string description)
        {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                object link = Invoke(shell, "CreateShortcut", path);
                Set(link, "TargetPath", target);
                Set(link, "Arguments", arguments);
                Set(link, "WorkingDirectory", Path.GetDirectoryName(target));
                Set(link, "IconLocation", icon + ",0");
                Set(link, "Description", description);
                Invoke(link, "Save");
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
            }
            finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
        }

        // (target, arguments) of an existing shortcut
        public static string[] Read(string path)
        {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            try
            {
                object link = Invoke(shell, "CreateShortcut", path);
                var result = new[] { (string)Get(link, "TargetPath"), (string)Get(link, "Arguments") };
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
                return result;
            }
            finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
        }

        static object Invoke(object target, string method, params object[] args)
        {
            return target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args);
        }

        static void Set(object target, string property, object value)
        {
            target.GetType().InvokeMember(property, BindingFlags.SetProperty, null, target, new[] { value });
        }

        static object Get(object target, string property)
        {
            return target.GetType().InvokeMember(property, BindingFlags.GetProperty, null, target, null);
        }
    }
}
