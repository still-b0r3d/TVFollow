using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TVFollow.Tests
{
    // Developer tools, not part of the tests:
    //   TVFollowTests.exe --shots <folder>  click through the setup window against pretend TVs and
    //                                       save a picture of every step
    //   TVFollowTests.exe --probe           list the Rokus on this network and their inputs (only reads)
    static class Shots
    {
        public static int Run(string folder)
        {
            Directory.CreateDirectory(folder);
            Settings.Dir = Path.Combine(Path.GetTempPath(), "TVFollowShots-" + Guid.NewGuid().ToString("N"));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // This drives the window from outside a message loop. Keep one UI context for good, so
            // the pages' awaits come back to this thread like they do under ShowDialog.
            WindowsFormsSynchronizationContext.AutoInstall = false;
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

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
                form.Scan = () => { Thread.Sleep(600); return TvScanner.Probe(new[] { "127.0.0.1", "127.0.0.2" }, tv.Port); };
                form.Test.OffWaitMs = 3000;
                form.Test.StandbyCheckMs = 300;
                form.Test.PollMs = 100;
                form.Test.SendWake = mac => { };
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000); // drawn off screen, out of the way
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
            }
            Console.WriteLine("saved the pictures in " + folder);
            return 0;
        }

        public static int Probe()
        {
            foreach (FoundTv tv in TvScanner.FindAll(Settings.DefaultPort))
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
            using (var bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
                bmp.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
            }
        }
    }
}
