using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow.Tests
{
    // Developer tools, not part of the tests:
    //   TVFollowTests.exe --shots <folder>        click through the setup window against pretend TVs
    //                                             and draw every step, off screen (a quick look)
    //   TVFollowTests.exe --screenshots <folder>  the same on screen, in front of a plain backdrop,
    //                                             captured as Windows shows it (for the docs);
    //                                             also the tray menu
    //   TVFollowTests.exe --probe                 list the Rokus on this network and their inputs
    //                                             (only reads)
    class ShownTv : FoundTv
    {
        public override string Address { get { return Shown(Ip); } }

        // home-network addresses for the pretend TVs' loopback ones
        public static string Shown(string ip)
        {
            return ip == "127.0.0.2" ? "192.168.1.50" : ip == "127.0.0.3" ? "192.168.1.51" : ip.StartsWith("127.") ? "192.168.1.62" : ip;
        }
    }

    static class Shots
    {
        static TvSettings Tv(Settings s) { return TestMain.Tv(s); }

        static readonly Color Backdrop = Color.FromArgb(232, 234, 237);
        static bool onScreen;
        static Form backdrop;

        public static int Run(string folder, bool screen)
        {
            onScreen = screen;
            Directory.CreateDirectory(folder);
            Settings.Dir = Path.Combine(Path.GetTempPath(), "TVFollowShots-" + Guid.NewGuid().ToString("N"));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // This drives the window from outside a message loop. Keep one UI context for good, so
            // the pages' awaits come back to this thread like they do under ShowDialog.
            WindowsFormsSynchronizationContext.AutoInstall = false;
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            if (onScreen)
            {
                backdrop = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    BackColor = Backdrop,
                    ShowInTaskbar = false,
                    TopMost = true,
                    StartPosition = FormStartPosition.Manual,
                    Bounds = Screen.PrimaryScreen.WorkingArea
                };
                backdrop.Show();
            }

            using (var tv = new FakeTv("127.0.0.2", 0))
            using (var stick = new FakeTv("127.0.0.1", tv.Port))
            {
                tv.Name = "Living Room TV";
                tv.Location = "Living room";
                tv.InputNames = new[] { "HDMI 1", "Game console", "HDMI 3", "HDMI 4" };
                tv.App = "tvinput.hdmi1";
                tv.ControlMode = "limited";
                stick.IsTv = false;
                stick.Name = "Bedroom Roku";
                stick.Location = "Bedroom";
                stick.Serial = "X000000002";

                var settings = new Settings();
                settings.Port = tv.Port;
                var form = new SetupForm(settings);
                form.Scan = () =>
                {
                    Thread.Sleep(600);
                    // show home-network addresses instead of the pretend TVs' loopback ones
                    return TvScanner.Probe(new[] { "127.0.0.1", "127.0.0.2" }, tv.Port)
                        .ConvertAll(f => (FoundTv)new ShownTv { Ip = f.Ip, Info = f.Info });
                };
                form.Test.OffWaitMs = 3000;
                form.Test.StandbyCheckMs = 300;
                form.Test.PollMs = 100;
                form.Test.SendWake = mac => 0;
                form.StartPosition = FormStartPosition.Manual;
                if (onScreen)
                {
                    form.TopMost = true;
                    Rectangle area = Screen.PrimaryScreen.WorkingArea;
                    form.Location = new Point(area.Left + 80, area.Top + 80);
                }
                else form.Location = new Point(-20000, -20000); // drawn off screen, out of the way
                form.Show();

                WaitFor(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext);
                Save(form, folder, "1-find");
                form.GoNext();

                WaitFor(() => Text(form).Contains("Limited"));
                Save(form, folder, "2-control-limited");
                tv.ControlMode = "enabled";
                WaitFor(() => form.CurrentPage.CanGoNext);
                Save(form, folder, "2-control-ok");
                form.GoNext();

                WaitFor(() => !form.CurrentPage.Busy && form.CurrentPage.CanGoNext);
                Save(form, folder, "3-input");
                form.GoNext();

                Save(form, folder, "4-test-before");
                ((TestPage)form.CurrentPage).StartTest();
                WaitFor(() => !form.CurrentPage.Busy);
                Save(form, folder, "4-test-done");
                form.GoNext();

                Save(form, folder, "5-finish");
                form.Close();

                if (onScreen)
                {
                    SaveRemotesAndSettings(folder, tv);
                    SaveTrayMenu(folder, settings);
                    SaveTwoTvs(folder, tv);
                }
            }
            if (backdrop != null) backdrop.Close();
            try { Directory.Delete(Settings.Dir, true); } catch { }
            Console.WriteLine("saved the pictures in " + folder);
            return 0;
        }

        // the three remote layouts and the Settings window, against the pretend living-room TV
        static void SaveRemotesAndSettings(string folder, FakeTv tv)
        {
            var s = new Settings();
            Tv(s).Ip = tv.Ip;
            s.Port = tv.Port;
            Tv(s).Serial = tv.Serial;
            Tv(s).Input = "hdmi1";
            tv.App = "tvinput.hdmi1";
            tv.PowerMode = "PowerOn";
            SettingsForm.ShownAddress = ShownTv.Shown;

            string[] layouts = { RemoteForm.Classic, RemoteForm.Compact, RemoteForm.Strip };
            for (int i = 0; i < layouts.Length; i++)
            {
                List<Controller> tvs = Controller.For(s);
                var remote = new RemoteForm(s, tvs, tvs[0], layouts[i]);
                remote.Show();
                DateTime until = DateTime.UtcNow.AddSeconds(2);
                while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(30); } // inputs arrive
                WaitFor(() => true);
                Thread.Sleep(250);
                Capture(Rectangle.Inflate(VisibleBounds(remote), 24, 24), Path.Combine(folder, (7 + i) + "-remote-" + layouts[i] + ".png"));
                remote.Close();
                remote.Dispose();
            }

            var follower = new Follower(s, Controller.For(s)); // not started
            using (var tray = new Tray(follower))
            using (var window = new SettingsForm(s, follower, tray))
            {
                Rectangle area = Screen.PrimaryScreen.WorkingArea;
                window.StartPosition = FormStartPosition.Manual;
                window.Location = new Point(area.Left + 80, area.Top + 40);
                window.TopMost = true;
                window.Show();
                WaitFor(() => Text(window).Contains("Living Room TV") && Text(window).Contains("showing this PC"));
                Thread.Sleep(250);
                Capture(Rectangle.Inflate(VisibleBounds(window), 24, 24), Path.Combine(folder, "10-settings.png"));
                window.Close();
            }
        }

        static void SaveTrayMenu(string folder, Settings settings)
        {
            Tv(settings).Ip = "127.0.0.9";
            Tv(settings).Serial = "X000000001";
            var follower = new Follower(settings, Controller.For(settings)); // not started: only its rules are read
            using (var tray = new Tray(follower))
            {
                tray.Menu.Show(backdrop, new Point(200, 140));
                WaitFor(() => tray.Menu.Visible);
                Capture(Rectangle.Inflate(tray.Menu.Bounds, 18, 18), Path.Combine(folder, "6-tray-menu.png"));
                tray.Menu.Close();
            }
        }

        // Two TVs, the living-room one and an office one: setup's first step with both ticked,
        // the tray menu, the Settings window and the remote with its TV picker.
        static void SaveTwoTvs(string folder, FakeTv tv)
        {
            using (var office = new FakeTv("127.0.0.3", tv.Port))
            {
                office.Name = "Office TV";
                office.Location = "Office";
                office.Serial = "X000000003";
                office.App = "tvinput.hdmi2";
                tv.ControlMode = "enabled";
                tv.PowerMode = office.PowerMode = "PowerOn";
                tv.App = "tvinput.hdmi1";
                Rectangle area = Screen.PrimaryScreen.WorkingArea;

                var form = new SetupForm(new Settings { Port = tv.Port });
                form.Scan = () => TvScanner.Probe(new[] { "127.0.0.1", "127.0.0.2", "127.0.0.3" }, tv.Port)
                    .ConvertAll(f => (FoundTv)new ShownTv { Ip = f.Ip, Info = f.Info });
                form.StartPosition = FormStartPosition.Manual;
                form.TopMost = true;
                form.Location = new Point(area.Left + 80, area.Top + 80);
                form.Show();
                WaitFor(() => !form.CurrentPage.Busy && Text(form).Contains("Found 2 TVs"));
                var both = new List<FoundTv>();
                foreach (FoundTv f in TvScanner.Probe(new[] { "127.0.0.2", "127.0.0.3" }, tv.Port)) both.Add(f);
                form.Pick(both.ToArray());
                Save(form, folder, "11-find-two-tvs");
                form.Close();

                var s = new Settings { Port = tv.Port };
                s.Tvs.Add(new TvSettings { Ip = tv.Ip, Serial = tv.Serial, Input = "hdmi1", Name = tv.Name });
                s.Tvs.Add(new TvSettings { Ip = office.Ip, Serial = office.Serial, Input = "hdmi2", Name = office.Name });
                List<Controller> tvs = Controller.For(s);
                var follower = new Follower(s, tvs); // not started
                using (var tray = new Tray(follower))
                {
                    tray.Menu.Show(backdrop, new Point(200, 140));
                    WaitFor(() => tray.Menu.Visible);
                    Capture(Rectangle.Inflate(tray.Menu.Bounds, 18, 18), Path.Combine(folder, "12-tray-menu-two-tvs.png"));
                    tray.Menu.Close();

                    using (var window = new SettingsForm(s, follower, tray))
                    {
                        window.StartPosition = FormStartPosition.Manual;
                        window.Location = new Point(area.Left + 80, area.Top + 20);
                        window.TopMost = true;
                        window.Show();
                        WaitFor(() => Text(window).Contains("1.  Living Room TV  ·  Living room") && Text(window).Contains("2.  Office TV  ·  Office"));
                        Thread.Sleep(250);
                        Capture(Rectangle.Inflate(VisibleBounds(window), 24, 24), Path.Combine(folder, "13-settings-two-tvs.png"));
                        window.Close();
                    }
                }

                var remote = new RemoteForm(s, tvs, tvs[1], RemoteForm.Classic);
                remote.Show();
                DateTime until = DateTime.UtcNow.AddSeconds(2);
                while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(30); } // inputs arrive
                WaitFor(() => true);
                Thread.Sleep(250);
                Capture(Rectangle.Inflate(VisibleBounds(remote), 24, 24), Path.Combine(folder, "14-remote-two-tvs.png"));
                remote.Close();
                remote.Dispose();
            }
        }

        public static int Probe()
        {
            foreach (FoundTv tv in TvScanner.FindAll(Settings.DefaultPort, null))
            {
                Console.WriteLine(tv.Title + "  |  " + tv.Details + "  |  tv=" + tv.Info.IsTv + " control=" + tv.Info.ControlMode +
                    " power=" + tv.Info.PowerMode + " network=" + tv.Info.NetworkType);
                var client = new RokuClient(tv.Ip, Settings.DefaultPort);
                List<KeyValuePair<string, string>> inputs = client.GetHdmiInputs(3000);
                if (inputs == null) Console.WriteLine("    inputs: (not given)");
                else foreach (var input in inputs) Console.WriteLine("    " + input.Key + " = " + input.Value);
                Console.WriteLine("    showing: " + client.GetActiveApp(3000));
            }
            return 0;
        }

        static void WaitFor(Func<bool> done)
        {
            DateTime until = DateTime.UtcNow.AddSeconds(30);
            while (!done())
            {
                if (DateTime.UtcNow > until) throw new TimeoutException("the setup window got stuck");
                Application.DoEvents();
                Thread.Sleep(30);
            }
            for (int i = 0; i < 10; i++) { Application.DoEvents(); Thread.Sleep(20); } // let it paint
        }

        static string Text(Control c)
        {
            string text = c.Text;
            foreach (Control child in c.Controls) if (child.Visible) text += "\n" + Text(child);
            return text;
        }

        static void Save(Form form, string folder, string name)
        {
            WaitFor(() => true);
            string path = Path.Combine(folder, name + ".png");
            if (onScreen)
            {
                Thread.Sleep(250); // let the desktop compositor catch up
                Capture(Rectangle.Inflate(VisibleBounds(form), 24, 24), path);
                return;
            }
            using (var bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
                bmp.Save(path, ImageFormat.Png);
            }
        }

        static void Capture(Rectangle r, string path)
        {
            using (var bmp = new Bitmap(r.Width, r.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
                bmp.Save(path, ImageFormat.Png);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT rect, int size);

        // the window as drawn, without the invisible resize border Windows 10/11 adds around it
        static Rectangle VisibleBounds(Form form)
        {
            RECT r;
            const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
            if (DwmGetWindowAttribute(form.Handle, DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf(typeof(RECT))) == 0)
                return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            return form.Bounds;
        }
    }
}
