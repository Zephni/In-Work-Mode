using System.Drawing;
using System.Windows.Forms;

namespace WorkMode.UI
{
    // Central dark colour scheme for the whole application. A near-black base,
    // slightly lighter "surface" tones for cards / controls, and a greenish-teal
    // accent used for primary actions and highlights.
    internal static class Theme
    {
        // Backgrounds, darkest to lightest.
        public static readonly Color Background = Color.FromArgb(24, 26, 30);   // window base
        public static readonly Color Surface = Color.FromArgb(34, 37, 43);      // cards / rows
        public static readonly Color SurfaceAlt = Color.FromArgb(44, 48, 56);   // inputs / mid highlights
        public static readonly Color Border = Color.FromArgb(58, 63, 72);

        // Greenish-teal accent (primary).
        public static readonly Color Accent = Color.FromArgb(20, 184, 166);
        public static readonly Color AccentHover = Color.FromArgb(45, 212, 191);
        public static readonly Color AccentPressed = Color.FromArgb(13, 148, 136);

        // Destructive / stop action.
        public static readonly Color Danger = Color.FromArgb(224, 90, 90);
        public static readonly Color DangerHover = Color.FromArgb(238, 112, 112);

        // Text.
        public static readonly Color Text = Color.FromArgb(233, 236, 239);
        public static readonly Color TextMuted = Color.FromArgb(150, 158, 168);

        // Applies the base dark look to a form.
        public static void ApplyForm(Form form)
        {
            form.BackColor = Background;
            form.ForeColor = Text;
        }

        // Styles a flat, rounded-looking button with the given fill/hover colours.
        public static void StyleButton(Button button, Color fill, Color hover, Color foreColor)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = hover;
            button.FlatAppearance.MouseDownBackColor = hover;
            button.BackColor = fill;
            button.ForeColor = foreColor;
            button.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Regular);
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
        }

        // A subtle "surface" button (used for secondary actions like Edit / Cancel).
        public static void StyleSurfaceButton(Button button)
        {
            StyleButton(button, SurfaceAlt, Border, Text);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Border;
        }
    }
}
