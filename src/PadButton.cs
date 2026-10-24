using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TVFollow
{
    // Light or dark, following Windows' app setting, and the icon font for the remote.
    static class Theme
    {
        public static readonly bool Dark = ReadDark();

        public static Color Window { get { return Dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243); } }
        public static Color Button { get { return Dark ? Color.FromArgb(52, 52, 52) : Color.White; } }
        public static Color ButtonHover { get { return Dark ? Color.FromArgb(64, 64, 64) : Color.FromArgb(234, 234, 234); } }
        public static Color ButtonDown { get { return Dark ? Color.FromArgb(44, 44, 44) : Color.FromArgb(222, 222, 222); } }
        public static Color Border { get { return Dark ? Color.FromArgb(80, 80, 80) : Color.FromArgb(206, 206, 206); } }
        public static Color Text { get { return Dark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(26, 26, 26); } }
        public static Color Muted { get { return Dark ? Color.FromArgb(170, 170, 170) : Color.FromArgb(100, 100, 100); } }
        public static Color Accent { get { return Color.FromArgb(42, 111, 219); } }        // TVFollow's blue
        public static Color AccentHover { get { return Color.FromArgb(58, 126, 232); } }
        public static Color AccentSoft { get { return Dark ? Color.FromArgb(28, 52, 92) : Color.FromArgb(221, 233, 251); } }
        public static Color AccentText { get { return Dark ? Color.FromArgb(150, 190, 250) : Color.FromArgb(22, 72, 160); } }
        public static Color Danger { get { return Dark ? Color.FromArgb(255, 120, 110) : Color.FromArgb(196, 43, 28); } }
        public static Color Warn { get { return Dark ? Color.FromArgb(240, 190, 90) : Color.FromArgb(154, 103, 0); } }

        static bool ReadDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object light = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return light is int && (int)light == 0;
                }
            }
            catch { return false; }
        }

        // Windows 11's icon font, or Windows 10's (same glyphs for everything used here)
        static string iconFamily;
        public static Font Icons(float points)
        {
            if (iconFamily == null)
            {
                iconFamily = "Segoe MDL2 Assets";
                using (var f = new Font("Segoe Fluent Icons", 10f))
                    if (f.Name == "Segoe Fluent Icons") iconFamily = "Segoe Fluent Icons";
            }
            return new Font(iconFamily, points);
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // a dark title bar to match, on Windows 10 2004+ and 11 (ignored elsewhere)
        public static void TitleBar(Form form)
        {
            if (!Dark) return;
            int on = 1;
            try { DwmSetWindowAttribute(form.Handle, 20, ref on, 4); } catch { }
        }
    }

    // Icon glyphs (Segoe Fluent Icons / MDL2 Assets).
    static class Glyphs
    {
        public const string Power = "", Home = "", Back = "";
        public const string Up = "", Down = "", Left = "", Right = "";
        public const string Play = "", Rewind = "", Forward = "", Replay = "";
        public const string Search = "", Mute = "", VolumeDown = "", VolumeUp = "";
        public const string Plus = "", Minus = "", Keyboard = "", Tv = "", Pin = "";
    }

    // A remote button: rounded (or round), an icon and/or a label, hover and pressed states.
    // It never takes keyboard focus, so typing into the TV isn't interrupted by a click.
    class PadButton : Control
    {
        public string Glyph = "";
        public string Label = "";
        public float GlyphPoints = 12f;
        public bool Accent, Danger, Round, Selected;
        bool hover, down;

        public PadButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color fill, border, text;
            if (Accent || Selected)
            {
                fill = Accent ? (down ? Theme.Accent : hover ? Theme.AccentHover : Theme.Accent) : Theme.AccentSoft;
                border = Theme.Accent;
                text = Accent ? Color.White : Theme.AccentText;
            }
            else
            {
                fill = down ? Theme.ButtonDown : hover ? Theme.ButtonHover : Theme.Button;
                border = Theme.Border;
                text = Danger ? Theme.Danger : Enabled ? Theme.Text : Theme.Muted;
            }
            using (GraphicsPath path = Round ? Ellipse(r) : Rounded(r, Math.Min(8f, r.Height / 2)))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            TextFormatFlags centre = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            if (Glyph.Length > 0 && Label.Length > 0)
            {
                using (Font icons = Theme.Icons(GlyphPoints))
                {
                    Size gs = TextRenderer.MeasureText(g, Glyph, icons, Size.Empty, TextFormatFlags.NoPadding);
                    Size ls = TextRenderer.MeasureText(g, Label, Font, Size.Empty, TextFormatFlags.NoPadding);
                    int gap = 5, x = (Width - gs.Width - gap - ls.Width) / 2;
                    TextRenderer.DrawText(g, Glyph, icons, new Rectangle(x, 0, gs.Width, Height), text, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(g, Label, Font, new Rectangle(x + gs.Width + gap, 0, ls.Width + 2, Height), text, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                }
            }
            else if (Glyph.Length > 0)
            {
                using (Font icons = Theme.Icons(GlyphPoints)) TextRenderer.DrawText(g, Glyph, icons, ClientRectangle, text, centre);
            }
            else
            {
                TextRenderer.DrawText(g, Label, Font, ClientRectangle, text, centre | TextFormatFlags.EndEllipsis);
            }
        }

        static GraphicsPath Rounded(RectangleF r, float radius)
        {
            float d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static GraphicsPath Ellipse(RectangleF r)
        {
            var p = new GraphicsPath();
            p.AddEllipse(r);
            return p;
        }
    }
}
