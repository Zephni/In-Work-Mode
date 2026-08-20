using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WorkMode.UI
{
    // Placeholder "card" shown at the end of the workspace list. Matches the
    // rounded shape and height of a WorkspaceControl row but is rendered as a
    // dashed, muted invitation rather than a real entry; clicking it opens the
    // add-workspace dialog.
    public sealed class AddWorkspaceRow : UserControl
    {
        private const int CornerRadius = 10;
        private const int RowHeight = 56;

        private bool _hover;

        public event EventHandler AddRequested;

        public AddWorkspaceRow()
        {
            Height = RowHeight;
            Margin = new Padding(0, 0, 0, 8);
            BackColor = Theme.Background;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            Click += (s, e) => { var h = AddRequested; if (h != null) h(this, EventArgs.Empty); };
            MouseEnter += (s, e) => { _hover = true; Invalidate(); };
            MouseLeave += (s, e) => { _hover = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Theme.Background);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color lineColor = _hover ? Theme.Accent : Theme.Border;
            using (var path = RoundedRect(rect, CornerRadius))
            using (var pen = new Pen(lineColor) { DashStyle = DashStyle.Dash })
                e.Graphics.DrawPath(pen, path);

            Color contentColor = _hover ? Theme.Accent : Theme.TextMuted;
            const string text = "Add New Workspace";
            using (var font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Regular))
            using (var icon = Glyphs.Plus(18, contentColor))
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
