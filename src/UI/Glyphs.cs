using System.Drawing;
using System.Drawing.Drawing2D;

namespace WorkMode.UI
{
    // Draws small, crisp vector glyphs used on buttons (play / stop / edit / add).
    // Everything is rendered with GDI+ so the app needs no external icon assets.
    internal static class Glyphs
    {
        public static Bitmap Play(int size, Color color)
        {
            Graphics g;
            var bmp = NewBitmap(size, out g);
            float m = size * 0.28f;
            using (var brush = new SolidBrush(color))
            {
                var pts = new[]
                {
                    new PointF(m, m),
                    new PointF(m, size - m),
                    new PointF(size - m, size / 2f)
                };
                g.FillPolygon(brush, pts);
            }
            g.Dispose();
            return bmp;
        }

        public static Bitmap Stop(int size, Color color)
        {
            Graphics g;
            var bmp = NewBitmap(size, out g);
            float m = size * 0.30f;
            using (var brush = new SolidBrush(color))
            using (var path = RoundedRect(new RectangleF(m, m, size - 2 * m, size - 2 * m), size * 0.06f))
                g.FillPath(brush, path);
            g.Dispose();
            return bmp;
        }

        public static Bitmap Edit(int size, Color color)
        {
            Graphics g;
            var bmp = NewBitmap(size, out g);

            // A diagonal pencil: pointed tip at the lower-left, eraser end at the
            // upper-right, drawn as a solid silhouette so it reads clearly as "edit".
            var tip = new PointF(size * 0.20f, size * 0.80f);
            var end = new PointF(size * 0.80f, size * 0.20f);

            float dx = end.X - tip.X, dy = end.Y - tip.Y;
            float len = (float)System.Math.Sqrt(dx * dx + dy * dy);
            float ux = dx / len, uy = dy / len;   // along the pencil
            float px = -uy, py = ux;               // perpendicular

            float half = size * 0.115f;            // half-width of the shaft
            float tipLen = size * 0.24f;           // length of the pointed nib

            var baseCenter = new PointF(tip.X + ux * tipLen, tip.Y + uy * tipLen);
            var b1 = new PointF(baseCenter.X + px * half, baseCenter.Y + py * half);
            var b2 = new PointF(baseCenter.X - px * half, baseCenter.Y - py * half);
            var e1 = new PointF(end.X + px * half, end.Y + py * half);
            var e2 = new PointF(end.X - px * half, end.Y - py * half);

            using (var brush = new SolidBrush(color))
            {
                using (var body = new GraphicsPath())
                {
                    body.AddPolygon(new[] { b1, e1, e2, b2 });
                    g.FillPath(brush, body);
                }
                using (var nib = new GraphicsPath())
                {
                    nib.AddPolygon(new[] { tip, b1, b2 });
                    g.FillPath(brush, nib);
                }
            }
            g.Dispose();
            return bmp;
        }

        public static Bitmap Plus(int size, Color color)
        {
            Graphics g;
            var bmp = NewBitmap(size, out g);
            using (var pen = new Pen(color, size * 0.12f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float m = size * 0.26f;
                float c = size / 2f;
                g.DrawLine(pen, m, c, size - m, c);
                g.DrawLine(pen, c, m, c, size - m);
            }
            g.Dispose();
            return bmp;
        }

        public static Bitmap Trash(int size, Color color)
        {
            Graphics g;
            var bmp = NewBitmap(size, out g);
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(color, size * 0.09f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cx = size / 2f;
                // Lid and handle across the top.
                float lidY = size * 0.28f;
                g.DrawLine(pen, size * 0.20f, lidY, size * 0.80f, lidY);
                g.DrawLine(pen, size * 0.40f, lidY, size * 0.42f, size * 0.18f);
                g.DrawLine(pen, size * 0.60f, lidY, size * 0.58f, size * 0.18f);
                g.DrawLine(pen, size * 0.42f, size * 0.18f, size * 0.58f, size * 0.18f);

                // Can body (tapered bucket).
                using (var body = new GraphicsPath())
                {
                    body.AddPolygon(new[]
                    {
                        new PointF(size * 0.26f, lidY + size * 0.04f),
                        new PointF(size * 0.74f, lidY + size * 0.04f),
                        new PointF(size * 0.68f, size * 0.82f),
                        new PointF(size * 0.32f, size * 0.82f)
                    });
                    g.FillPath(brush, body);
                }
            }
            g.Dispose();
            return bmp;
        }

        private static Bitmap NewBitmap(int size, out Graphics g)
        {
            var bmp = new Bitmap(size, size);
            g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            // Nudge everything up ~1px; on the buttons the glyphs otherwise read
            // as sitting a touch below the vertical centre.
            g.TranslateTransform(0f, -1f);
            return bmp;
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2f;
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
