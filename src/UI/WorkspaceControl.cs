using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WorkMode.Models;

namespace WorkMode.UI
{
    // A single row in the workspace list: title, time counted, a start/stop
    // toggle button and an edit button. Rendered as a rounded dark "card".
    public sealed class WorkspaceControl : UserControl
    {
        private const int CornerRadius = 10;

        private readonly Workspace _workspace;
        private readonly Label _titleLabel;
        private readonly Label _timeLabel;
        private readonly Button _toggleButton;
        private readonly Button _editButton;

        // Raised when the user toggles this workspace's timer on or off.
        public event EventHandler ToggleRequested;
        // Raised when the user asks to edit this workspace.
        public event EventHandler EditRequested;

        public Workspace Workspace { get { return _workspace; } }

        public WorkspaceControl(Workspace workspace)
        {
            _workspace = workspace;

            Height = 56;
            Margin = new Padding(0, 0, 0, 8);
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            _titleLabel = new Label
            {
                Location = new Point(14, 8),
                AutoSize = false,
                Size = new Size(220, 20),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            _timeLabel = new Label
            {
                Location = new Point(14, 30),
                AutoSize = false,
                Size = new Size(220, 18),
                Font = new Font("Consolas", 11.5f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent
            };

            _toggleButton = new Button
            {
                Size = new Size(78, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(4, 0, 0, 0)
            };
            _toggleButton.Click += (s, e) => { var h = ToggleRequested; if (h != null) h(this, EventArgs.Empty); };

            _editButton = new Button
            {
                Text = "Edit",
                Size = new Size(66, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Image = Glyphs.Edit(16, Theme.Text),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(4, 0, 0, 0)
            };
            Theme.StyleSurfaceButton(_editButton);
            _editButton.Click += (s, e) => { var h = EditRequested; if (h != null) h(this, EventArgs.Empty); };

            Controls.Add(_titleLabel);
            Controls.Add(_timeLabel);
            Controls.Add(_toggleButton);
            Controls.Add(_editButton);

            Resize += (s, e) => LayoutButtons();
            LayoutButtons();
            Refresh();
        }

        // Rounded card background with a subtle border.
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Theme.Background);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedRect(rect, CornerRadius))
            using (var fill = new SolidBrush(Theme.Surface))
            using (var pen = new Pen(Theme.Border))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
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

        private void LayoutButtons()
        {
            int buttonTop = (Height - _editButton.Height) / 2;
            _editButton.Location = new Point(Width - _editButton.Width - 10, buttonTop);
            _toggleButton.Location = new Point(_editButton.Left - _toggleButton.Width - 8, buttonTop);

            int labelWidth = _toggleButton.Left - 22;
            if (labelWidth < 40) labelWidth = 40;
            _titleLabel.Width = labelWidth;
            _timeLabel.Width = labelWidth;
        }

        // Refreshes the displayed title, time and button state from the model.
        public new void Refresh()
        {
            _titleLabel.Text = _workspace.Title;
            _timeLabel.Text = Workspace.Format(_workspace.ElapsedSeconds);

            if (_workspace.IsRunning)
            {
                _toggleButton.Text = "Stop";
                _toggleButton.Image = Glyphs.Stop(14, Color.White);
                Theme.StyleButton(_toggleButton, Theme.Danger, Theme.DangerHover, Color.White);
            }
            else
            {
                _toggleButton.Text = "Start";
                _toggleButton.Image = Glyphs.Play(14, Color.White);
                Theme.StyleButton(_toggleButton, Theme.Accent, Theme.AccentHover, Color.White);
            }

            base.Refresh();
        }
    }
}
