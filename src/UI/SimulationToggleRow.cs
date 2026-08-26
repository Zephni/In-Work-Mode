using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WorkMode.UI
{
    // Developer-only "card" shown below the add-workspace placeholder. Toggles the
    // activity simulation (the harmless F15 heartbeat that keeps the machine from
    // going idle). Off by default; matches the shape/height of the other rows.
    public sealed class SimulationToggleRow : UserControl
    {
        private const int CornerRadius = 10;
        private const int RowHeight = 56;

        private bool _hover;
        private bool _active;

        public event EventHandler ToggleRequested;

        public SimulationToggleRow()
        {
            Height = RowHeight;
            Margin = new Padding(0, 0, 0, 8);
            BackColor = Theme.Background;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            Click += (s, e) => { var h = ToggleRequested; if (h != null) h(this, EventArgs.Empty); };
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; Invalidate(); };
        }

        // Whether the simulation is currently running. Controls the card's look.
        public bool Active
        {
            get { return _active; }
            set
            {
                if (_active == value) return;
                _active = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Theme.Background);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedRect(rect, CornerRadius))
            {
                if (_active)
                {
                    using (var fill = new SolidBrush(Theme.SurfaceAlt))
                        e.Graphics.FillPath(fill, path);
                    using (var pen = new Pen(Theme.Accent))
                        e.Graphics.DrawPath(pen, path);
                }
                else
                {
                    Color lineColor = _hover ? Theme.Accent : Theme.Border;
                    using (var pen = new Pen(lineColor) { DashStyle = DashStyle.Dash })
                        e.Graphics.DrawPath(pen, path);
                }
            }

            Color contentColor = _active
                ? Theme.Accent
                : (_hover ? Theme.Accent : Theme.TextMuted);
            string text = _active ? "Simulating Activity — On" : "Simulate Activity — Off";
            using (var font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Regular))
            using (var icon = Glyphs.Clock(18, contentColor))
            {
                Size textSize = TextRenderer.MeasureText(text, font);
                const int gap = 8;
                int totalWidth = icon.Width + gap + textSize.Width;
                int startX = (Width - totalWidth) / 2;

                e.Graphics.DrawImage(icon, startX, (Height - icon.Height) / 2);

                var textRect = new Rectangle(startX + icon.Width + gap, 0, textSize.Width, Height);
                TextRenderer.DrawText(e.Graphics, text, font, textRect, contentColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
