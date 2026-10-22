using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WorkMode.Models;

namespace WorkMode.UI
{
    internal sealed class CustomActionRow : UserControl
    {
        private readonly Label _title;
        private readonly Label _status;
        private readonly Button _run;
        private readonly Button _edit;
        private readonly Button _delete;
        private readonly ToolTip _tooltip;
        private readonly Timer _feedbackTimer;
        private readonly RowReorderBehavior _reorder;
        private bool _hover;

        public event EventHandler RunRequested;
        public event EventHandler EditRequested;
        public event EventHandler DeleteRequested;
        public event EventHandler ReorderRequested;
        public event EventHandler ReorderCompleted;

        internal CustomAction Action { get; private set; }
        internal Button RunButton { get { return _run; } }
        internal string StatusText { get { return _status.Text; } }

        public CustomActionRow(CustomAction action)
        {
            Action = action;
            Height = 56;
            Margin = new Padding(0, 0, 0, 8);
            BackColor = Theme.Surface;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            _title = new Label
            {
                Text = action.Title, Location = new Point(14, 8), Height = 20,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Theme.Text, BackColor = Color.Transparent,
                AutoEllipsis = true, Cursor = Cursors.Hand
            };
            _status = new Label
            {
                Location = new Point(14, 30), Height = 18,
                Font = new Font("Segoe UI", 9f), ForeColor = Theme.TextMuted,
                BackColor = Color.Transparent, AutoEllipsis = true
            };
            _run = MakeButton("Run " + action.Title, Glyphs.Play(20, Color.White), Theme.Accent, Theme.AccentHover);
            _edit = MakeButton("Edit " + action.Title, Glyphs.Edit(20, Color.White), Theme.NeutralGray, Theme.NeutralGrayHover);
            _delete = MakeButton("Delete " + action.Title, Glyphs.Trash(20, Color.White), Theme.NeutralGray, Theme.NeutralGrayHover);
            _run.Click += (s, e) => { var handler = RunRequested; if (handler != null) handler(this, e); };
            _title.Click += (s, e) => { if (_run.Enabled && !_reorder.SuppressClick) _run.PerformClick(); };
            _edit.Click += (s, e) => { var handler = EditRequested; if (handler != null) handler(this, e); };
            _delete.Click += (s, e) => { var handler = DeleteRequested; if (handler != null) handler(this, e); };
            _tooltip = new ToolTip { InitialDelay = 350, ReshowDelay = 200, ShowAlways = true };
            _tooltip.SetToolTip(_run, "Run");
            _tooltip.SetToolTip(_edit, "Edit");
            _tooltip.SetToolTip(_delete, "Delete");
            _feedbackTimer = new Timer { Interval = 3500 };
            _feedbackTimer.Tick += (s, e) => ResetFeedback(action);
            Controls.AddRange(new Control[] { _title, _status, _run, _edit, _delete });
            _reorder = new RowReorderBehavior(this, this, _title, _status);
            _reorder.ReorderRequested += (s, e) => { var h = ReorderRequested; if (h != null) h(this, e); };
            _reorder.ReorderCompleted += (s, e) => { var h = ReorderCompleted; if (h != null) h(this, e); };
            MouseEnter += OnHoverChanged;
            MouseLeave += OnHoverChanged;
            foreach (Control child in Controls)
            {
                child.MouseEnter += OnHoverChanged;
                child.MouseLeave += OnHoverChanged;
            }
            Resize += (s, e) => LayoutRow();
            LayoutRow();
            ResetFeedback(action);
        }

        private static Button MakeButton(string name, Bitmap image, Color fill, Color hover)
        {
            var button = new Button { AccessibleName = name, Size = new Size(34, 30), Image = image };
            Theme.StyleButton(button, fill, hover, Color.White);
            return button;
        }

        private void LayoutRow()
        {
            int x = Width - 14 - _delete.Width;
            _delete.Location = new Point(x, 13);
            x -= 6 + _edit.Width;
            _edit.Location = new Point(x, 13);
            x -= 6 + _run.Width;
            _run.Location = new Point(x, 13);
            _title.Width = _status.Width = Math.Max(10, x - 24);
        }

        private void SetIcon(Bitmap image)
        {
            Image previous = _run.Image;
            _run.Image = image;
            if (previous != null) previous.Dispose();
        }

        public void SetRunning(string message = "Running...")
        {
            _feedbackTimer.Stop();
            _status.Text = message;
            _status.ForeColor = Theme.Accent;
            SetIcon(Glyphs.Clock(20, Color.White));
        }

        public void SetSuccess(bool launchedInTerminal = false)
        {
            _status.Text = launchedInTerminal ? "Opened in terminal" : "Ran successfully";
            _status.ForeColor = Theme.Accent;
            SetIcon(Glyphs.Check(20, Color.White));
            _feedbackTimer.Start();
        }

        public void SetError()
        {
            _status.Text = "Error running command";
            _status.ForeColor = Theme.Danger;
            SetIcon(Glyphs.Info(20, Color.White));
        }

        public void SetCancelled()
        {
            _status.Text = "Cancelled";
            _status.ForeColor = Theme.TextMuted;
            SetIcon(Glyphs.Play(20, Color.White));
            _feedbackTimer.Start();
        }

        private void ResetFeedback(CustomAction action)
        {
            _feedbackTimer.Stop();
            _status.Text = action.Description;
            _status.ForeColor = Theme.TextMuted;
            SetIcon(Glyphs.Play(20, Color.White));
        }

        private void OnHoverChanged(object sender, EventArgs e)
        {
            _hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Theme.Background);
            using (var path = new GraphicsPath())
            using (var brush = new SolidBrush(Theme.Surface))
            using (var pen = _reorder.IsReady
                ? new Pen(Color.FromArgb(180, Theme.BlueHover), 1.5f)
                : new Pen(_hover ? Theme.WorkspaceBorder : Theme.Border))
            {
                const int diameter = 20;
                int inset = _reorder.IsReady ? 1 : 0;
                path.AddArc(inset, inset, diameter, diameter, 180, 90);
                path.AddArc(Width - 1 - diameter - inset, inset, diameter, diameter, 270, 90);
                path.AddArc(Width - 1 - diameter - inset, Height - 1 - diameter - inset, diameter, diameter, 0, 90);
                path.AddArc(inset, Height - 1 - diameter - inset, diameter, diameter, 90, 90);
                path.CloseFigure();
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reorder.Dispose();
                _feedbackTimer.Dispose();
                _tooltip.Dispose();
                _run.Image.Dispose();
                _edit.Image.Dispose();
                _delete.Image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
