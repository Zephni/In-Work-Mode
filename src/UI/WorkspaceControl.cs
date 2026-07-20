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
        private readonly Button _resetButton;
        private readonly Button _editButton;
        private readonly ToolTip _tooltip;
        private bool _selected;
        private bool _hover;

        // Raised when the user toggles this workspace's timer on or off.
        public event EventHandler ToggleRequested;
        // Raised when the user asks to reset this workspace's counted time.
        public event EventHandler ResetRequested;
        // Raised when the user asks to edit this workspace.
        public event EventHandler EditRequested;
        // Raised when the user clicks the row to select it.
        public event EventHandler SelectRequested;

        public Workspace Workspace { get { return _workspace; } }

        // Whether this row is currently the selected one (accent border).
        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
            }
        }

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
                Size = new Size(34, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                ImageAlign = ContentAlignment.MiddleCenter
            };
            _toggleButton.Click += (s, e) => { var h = ToggleRequested; if (h != null) h(this, EventArgs.Empty); };

            _resetButton = new Button
            {
                Size = new Size(34, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Image = Glyphs.Reset(20, Color.White),
                ImageAlign = ContentAlignment.MiddleCenter
            };
            Theme.StyleButton(_resetButton, Theme.Muted, Theme.MutedHover, Color.White);
            _resetButton.Click += (s, e) => { var h = ResetRequested; if (h != null) h(this, EventArgs.Empty); };

            _editButton = new Button
            {
                Size = new Size(34, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Image = Glyphs.Edit(20, Theme.Text),
                ImageAlign = ContentAlignment.MiddleCenter
            };
            Theme.StyleSurfaceButton(_editButton);
            _editButton.Click += (s, e) => { var h = EditRequested; if (h != null) h(this, EventArgs.Empty); };

            _tooltip = new ToolTip { InitialDelay = 350, ReshowDelay = 200, ShowAlways = true };
            _tooltip.SetToolTip(_resetButton, "Reset");
            _tooltip.SetToolTip(_editButton, "Edit");

            Controls.Add(_titleLabel);
            Controls.Add(_timeLabel);
            Controls.Add(_toggleButton);
            Controls.Add(_resetButton);
            Controls.Add(_editButton);

            // Clicking the card body (anywhere that isn't a button) selects the row.
            Click += OnSelectClick;
            _titleLabel.Click += OnSelectClick;
            _timeLabel.Click += OnSelectClick;

            // Track hover across the card and its children so a subtle accent
            // border can hint that the row is clickable.
            MouseEnter += OnHoverChanged;
            MouseLeave += OnHoverChanged;
            foreach (Control child in Controls)
            {
                child.MouseEnter += OnHoverChanged;
                child.MouseLeave += OnHoverChanged;
            }

            Resize += (s, e) => LayoutButtons();
            LayoutButtons();
            Refresh();
        }

        // Rounded card background with a subtle border.
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Theme.Background);

            // Inset by one pixel when selected so the thicker accent border isn't
            // clipped at the control edges.
            int inset = _selected ? 1 : 0;
            var rect = new Rectangle(inset, inset, Width - 1 - inset * 2, Height - 1 - inset * 2);
            using (var path = RoundedRect(rect, CornerRadius))
            using (var fill = new SolidBrush(Theme.Surface))
            using (var pen = BorderPen())
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        // Selected: solid accent. Hovered: faint accent hint. Otherwise: plain border.
        private Pen BorderPen()
        {
            if (_selected) return new Pen(Theme.Accent, 2f);
            if (_hover) return new Pen(Color.FromArgb(90, Theme.Accent));
            return new Pen(Theme.Border);
        }

        private void OnHoverChanged(object sender, EventArgs e)
        {
            bool hovering = ClientRectangle.Contains(PointToClient(Cursor.Position));
            if (_hover == hovering) return;
            _hover = hovering;
            Invalidate();
        }

        private void OnSelectClick(object sender, EventArgs e)
        {
            var h = SelectRequested;
            if (h != null) h(this, EventArgs.Empty);
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
            _resetButton.Location = new Point(_editButton.Left - _resetButton.Width - 8, buttonTop);
            _toggleButton.Location = new Point(_resetButton.Left - _toggleButton.Width - 8, buttonTop);

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
                _toggleButton.Image = Glyphs.Stop(20, Color.White);
                Theme.StyleButton(_toggleButton, Theme.Danger, Theme.DangerHover, Color.White);
                _tooltip.SetToolTip(_toggleButton, "Stop");
            }
            else
            {
                _toggleButton.Image = Glyphs.Play(20, Color.White);
                Theme.StyleButton(_toggleButton, Theme.Accent, Theme.AccentHover, Color.White);
                _tooltip.SetToolTip(_toggleButton, "Start");
            }

            base.Refresh();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _tooltip != null) _tooltip.Dispose();
            base.Dispose(disposing);
        }
    }
}
