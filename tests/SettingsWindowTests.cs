using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace TVFollow.Tests
{
    // The Settings window as a whole: getting round it with the Tab key, and its colours in
    // Windows' high contrast themes.
    static partial class TestMain
    {
        static void RunSettingsWindowTests()
        {
            Run("settings: Tab goes from the page list into the page, then to the footer", SettingsTabOrder);
            Run("settings: in high contrast the page list and the footers take Windows' colours", SettingsHighContrast);
        }

        // what Windows Forms does when Windows says its colours changed (a high contrast theme
        // went on or off): the window hears it, and passes it on to everything in it
        static void ColoursChanged(Control window)
        {
            typeof(Control).GetMethod("OnSystemColorsChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(window, new object[] { EventArgs.Empty });
        }

        static int Apart(Color a, Color b)
        {
            return Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
        }

        // The page list and the two footers, as they are with a high contrast theme on or off.
        // The tests can't switch Windows' theme, so "on" still has Windows' everyday colours:
        // what's checked is that every colour is then one of Windows' own (which a real high
        // contrast theme fills in), with none of TVFollow's greys left, and that the list is
        // really drawn that way.
        static void CheckLook(SettingsForm window, SetupForm setup, bool contrast, string when)
        {
            Color grey = Color.FromArgb(243, 243, 243), greyPatch = Color.FromArgb(232, 232, 232);
            Color listBack = contrast ? SystemColors.Window : grey, footerBack = contrast ? SystemColors.Control : grey;
            NavList list = FindControl<NavList>(window, n => true);
            Check(list.BackColor == listBack && list.Parent.BackColor == listBack,
                when + ": the page list is " + list.BackColor + " on a strip that's " + list.Parent.BackColor);
            Check(list.ForeColor.IsSystemColor, when + ": the page list's words are " + list.ForeColor + ", not one of Windows' colours");

            foreach (Form form in new Form[] { window, setup })
            {
                LinkLabel about = FindControl<LinkLabel>(form, l => l.Text.StartsWith("TVFollow "));
                string where = when + ": the footer of " + form.Text;
                Check(about != null && about.Parent.BackColor == footerBack, where + " is " + (about == null ? "missing" : about.Parent.BackColor.ToString()));
                // the version line is written in one of Windows' colours, the links in a quiet grey
                // of TVFollow's own, or the theme's link colour when the footer is the theme's too
                Check(about.ForeColor.IsSystemColor, where + " has its words in " + about.ForeColor);
                Check(about.LinkColor == (contrast ? SystemColors.HotTrack : SetupPage.Muted), where + " has its links in " + about.LinkColor);
                // (setup's buttons sit on a strip of their own, which has to go with the footer)
                foreach (Control strip in about.Parent.Controls)
                    if (strip is Panel) Check(strip.BackColor == footerBack, where + " has a strip that's " + strip.BackColor);
            }

            // the list as it's drawn, with a page open whose name has some letters to it
            window.ShowPage(SettingsForm.FollowingPage);
            using (var picture = new Bitmap(list.Width, list.Height))
            {
                list.DrawToBitmap(picture, new Rectangle(Point.Empty, list.Size));
                float scale;
                using (Graphics g = list.CreateGraphics()) scale = g.DpiX / 96f;
                Rectangle last = list.GetItemRectangle(list.Items.Count - 1);
                Color below = picture.GetPixel(list.Width / 2, Math.Min(list.Height - 2, last.Bottom + 6));
                Check(below.ToArgb() == listBack.ToArgb(), when + ": under its last page the list is drawn " + below + ", not " + listBack);

                // the open page: its patch, and the words on it (past the little bar at its left)
                Color patch = contrast ? SystemColors.Highlight : greyPatch;
                Color words = contrast ? SystemColors.HighlightText : list.ForeColor;
                Rectangle open = list.GetItemRectangle(list.SelectedIndex);
                int ofPatch = 0, ofWords = 0, dots = 0;
                for (int y = open.Top + (int)(4 * scale); y < open.Bottom - (int)(4 * scale); y++)
                    for (int x = open.Left + (int)(14 * scale); x < open.Right - (int)(8 * scale); x++)
                    {
                        Color dot = picture.GetPixel(x, y);
                        dots++;
                        if (dot.ToArgb() == patch.ToArgb()) ofPatch++;
                        else if (Apart(dot, words) < Apart(dot, patch)) ofWords++;
                    }
                Check(dots > 100 && ofPatch > dots / 2, when + ": the open page's patch isn't " + patch + " (" + ofPatch + " of " + dots + " dots)");
                Check(ofWords > 20, when + ": the open page's name isn't written in " + words + " (" + ofWords + " dots near it)");

                if (!contrast) return;
                int greys = 0;
                for (int y = 0; y < picture.Height; y++)
                    for (int x = 0; x < picture.Width; x++)
                    {
                        int dot = picture.GetPixel(x, y).ToArgb();
                        if (dot == grey.ToArgb() || dot == greyPatch.ToArgb()) greys++;
                    }
                // (Windows' own window colour is white here, so none of the list is either grey)
                Check(greys == 0 || SystemColors.Window.ToArgb() == grey.ToArgb(), when + ": " + greys + " dots of the list are still TVFollow's own grey");
            }
        }

        static void SettingsHighContrast()
        {
            Func<bool> before = Look.HighContrast;
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings s = SettingsFor(tv);
                s.Save();
                var follower = new Follower(s, Controller.For(s)); // not started
                try
                {
                    Look.HighContrast = () => false;
                    using (var tray = new Tray(follower))
                    using (var window = new SettingsForm(s, follower, tray))
                    using (var setup = new SetupForm(new Settings()))
                    {
                        OffScreen(window);
                        window.Show();
                        CheckLook(window, setup, false, "as it opens");
                        // a high contrast theme goes on while the windows are open, then off again
                        Look.HighContrast = () => true;
                        ColoursChanged(window);
                        ColoursChanged(setup);
                        CheckLook(window, setup, true, "once high contrast goes on");
                        Look.HighContrast = () => false;
                        ColoursChanged(window);
                        ColoursChanged(setup);
                        CheckLook(window, setup, false, "once high contrast goes off again");
                        window.Close();
                    }
                    // and windows opened while it's on
                    Look.HighContrast = () => true;
                    using (var tray = new Tray(follower))
                    using (var window = new SettingsForm(s, follower, tray))
                    using (var setup = new SetupForm(new Settings()))
                    {
                        OffScreen(window);
                        window.Show();
                        CheckLook(window, setup, true, "opened in high contrast");
                        window.Close();
                    }
                }
                finally { Look.HighContrast = before; }
                File.Delete(Settings.FilePath);
            }
        }

        // where Tab (or Shift+Tab) goes from a control, as Windows Forms works it out: the next
        // control in tab order that can take the focus and is a tab stop, round the end if need be
        static Control TabFrom(Form window, Control from, bool forward)
        {
            Control c = from;
            for (int i = 0; i < 2000; i++)
            {
                c = window.GetNextControl(c, forward) ?? window.GetNextControl(null, forward);
                if (c == null || c == from) return null;
                if (c.CanSelect && c.TabStop) return c;
            }
            return null;
        }

        // is it on one of the window's pages (and not the page list or the footer)?
        static bool OnAPage(Control c)
        {
            for (; c != null; c = c.Parent) if (c is Column) return true;
            return false;
        }

        static string Named(Control c)
        {
            return c == null ? "nothing" : c.GetType().Name + " \"" + c.Text.Replace("\r\n", " / ") + "\"";
        }

        static void SettingsTabOrder()
        {
            using (var tv = new FakeTv("127.0.0.1", 0))
            {
                Settings s = SettingsFor(tv);
                s.Save();
                var follower = new Follower(s, Controller.For(s)); // not started
                using (var tray = new Tray(follower))
                using (var window = new SettingsForm(s, follower, tray))
                {
                    OffScreen(window);
                    window.Show();
                    NavList list = FindControl<NavList>(window, n => true);
                    Check(list != null && list.CanSelect && list.TabStop, "no page list to tab from");
                    foreach (string page in window.ListedPages)
                    {
                        window.ShowPage(page);
                        // from the list, Tab goes into the page that's open...
                        Control first = TabFrom(window, list, true);
                        Check(OnAPage(first), "on the " + page + " page, Tab from the page list went to " + Named(first) + ", not into the page");
                        // ...through it, and only then to the footer: its links, then Close...
                        Control c = first;
                        for (int i = 0; i < 200 && OnAPage(c); i++) c = TabFrom(window, c, true);
                        Check(c is LinkLabel, "on the " + page + " page, Tab went from the page to " + Named(c) + ", not to the footer's links");
                        c = TabFrom(window, c, true);
                        Check(c is Button && c.Text == "Close", "after the footer's links came " + Named(c) + ", not Close");
                        // ...and round to the list again
                        Check(TabFrom(window, c, true) == list, "after Close came " + Named(TabFrom(window, c, true)) + ", not the page list");
                        // Shift+Tab from the top of the page is back at the list
                        Check(TabFrom(window, first, false) == list, "on the " + page + " page, Shift+Tab from the page's first control went to " + Named(TabFrom(window, first, false)));
                    }
                    window.Close();
                }
                File.Delete(Settings.FilePath);
            }
        }
    }
}
