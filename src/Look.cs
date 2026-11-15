using System;
using System.Drawing;
using System.Windows.Forms;

namespace TVFollow
{
    // The light grey that sets the Settings window's page list and the windows' footers apart
    // from the white pages. In Windows' high contrast themes there is no grey of TVFollow's
    // own: the words on it are in the theme's text colour (white, in the dark themes), which
    // can't be read on a light grey. There the list and the footers take the theme's colours,
    // like the rest of the window.
    static class Look
    {
        // Is a high contrast theme on? (The tests answer for Windows.)
        public static Func<bool> HighContrast = () => SystemInformation.HighContrast;

        static readonly Color Grey = Color.FromArgb(243, 243, 243);

        // a footer, with words in the colour Windows writes on buttons and panels
        public static Color Footer { get { return HighContrast() ? SystemColors.Control : Grey; } }

        // a footer's links: quiet, so the buttons beside them stand out; the theme's link colour in high contrast
        public static Color FooterLink { get { return HighContrast() ? SystemColors.HotTrack : SetupPage.Muted; } }

        // the page list, with words in the colour Windows writes in lists
        public static Color List { get { return HighContrast() ? SystemColors.Window : Grey; } }

        // The main window's coloured words: the quiet grey of its version line and status, and
        // the green and amber of how the TVs are (the Settings window's TV page uses the same).
        // In high contrast they're the theme's own text colours: the ✔ and ⚠ in front still
        // say which is which.
        public static Color Quiet { get { return HighContrast() ? SystemColors.GrayText : SetupPage.Muted; } }
        public static Color Good { get { return HighContrast() ? SystemColors.WindowText : SetupPage.Good; } }
        public static Color Warn { get { return HighContrast() ? SystemColors.WindowText : SetupPage.Warn; } }

        // the windows' titles: Segoe UI Semibold where Windows has it, else the body's font in bold
        public static Font TitleFont(Font body, float size)
        {
            var font = new Font("Segoe UI Semibold", size);
            if (font.Name == "Segoe UI Semibold") return font;
            font.Dispose();
            return new Font(body.FontFamily, size, FontStyle.Bold);
        }
    }
}
