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
    }
}
