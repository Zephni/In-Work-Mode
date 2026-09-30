using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WorkMode.Models;

namespace WorkMode.UI
{
    // A single row in the workspace list: title, time counted, and action buttons.
    // Rendered as a rounded dark "card".
    public sealed class WorkspaceControl : UserControl
    {
        private const int CornerRadius = 10;
        private const int HeaderHeight = 56;
        private readonly Workspace _workspace;
        private readonly Label _titleLabel;
        private readonly Label _timeLabel;
        private readonly Button _toggleButton;
        private readonly Button _resetButton;
        private readonly Button _editButton;
        private readonly Button _notesButton;
        private readonly Button _deleteButton;
        private readonly ToolTip _tooltip;
        private readonly Timer _dragHoldTimer;
        private bool _hover;
        // Desired edit-button visibility. Tracked separately from Button.Visible,
        // whose getter also depends on the whole parent chain (including the not
        // -yet-shown top-level Form during construction), which would otherwise
        // make LayoutButtons() miscalculate the gap for it.
        private bool _editVisible;
        private Point _dragStart;
        private bool _dragPending;
        private bool _dragReady;
        private bool _dragging;

        // Raised when the user toggles this workspace's timer on or off.
        public event EventHandler ToggleRequested;
        // Raised when the user asks to reset this workspace's counted time.
        public event EventHandler ResetRequested;
        // Raised when the user edits this workspace's notes.
        public event EventHandler NotesChanged;
        // Raised when the user asks to edit this workspace.
        public event EventHandler EditRequested;
        // Raised when the user asks to delete this workspace.
        public event EventHandler DeleteRequested;
        public event EventHandler ReorderRequested;
        public event EventHandler ReorderCompleted;

        public Workspace Workspace { get { return _workspace; } }

        // Whether the edit icon is shown. Hidden by default; only revealed while
        // dev mode is switched on.
        public bool EditButtonVisible
        {
            get { return _editVisible; }
            set
            {
                if (_editVisible == value) return;
                _editVisible = value;
                _editButton.Visible = value;
                LayoutButtons();
            }
        }

        public WorkspaceControl(Workspace workspace)
        {
            _workspace = workspace;

            Height = HeaderHeight;
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
            Theme.StyleButton(_resetButton, Theme.NeutralGray, Theme.NeutralGrayHover, Color.White);
            _resetButton.Click += (s, e) => { var h = ResetRequested; if (h != null) h(this, EventArgs.Empty); };

            _editButton = new Button
            {
                Size = new Size(34, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Image = Glyphs.Edit(20, Color.White),
                ImageAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            Theme.StyleButton(_editButton, Theme.NeutralGray, Theme.NeutralGrayHover, Color.White);
            _editButton.Click += (s, e) => { var h = EditRequested; if (h != null) h(this, EventArgs.Empty); };

            _notesButton = new Button
            {
                Size = new Size(34, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Image = Glyphs.Notes(20, Color.White),
                ImageAlign = ContentAlignment.MiddleCenter
            };
            Theme.StyleButton(_notesButton, Theme.NeutralGray, Theme.NeutralGrayHover, Color.White);
            _notesButton.Click += OnNotesClicked;

            _deleteButton = new Button
            {
                Size = new Size(34, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Image = Glyphs.Trash(20, Color.White),
                ImageAlign = ContentAlignment.MiddleCenter
            };
            Theme.StyleButton(_deleteButton, Theme.NeutralGray, Theme.NeutralGrayHover, Color.White);
            _deleteButton.Click += (s, e) => { var h = DeleteRequested; if (h != null) h(this, EventArgs.Empty); };

            _tooltip = new ToolTip { InitialDelay = 350, ReshowDelay = 200, ShowAlways = true };
            _tooltip.SetToolTip(_resetButton, "Reset");
            _tooltip.SetToolTip(_editButton, "Edit");
            _tooltip.SetToolTip(_notesButton, "Notes");
            _tooltip.SetToolTip(_deleteButton, "Delete");

            _dragHoldTimer = new Timer { Interval = 200 };
            _dragHoldTimer.Tick += OnDragHoldElapsed;

            Controls.Add(_titleLabel);
            Controls.Add(_timeLabel);
            Controls.Add(_toggleButton);
            Controls.Add(_resetButton);
            Controls.Add(_editButton);
            Controls.Add(_notesButton);
            Controls.Add(_deleteButton);

            MouseDown += OnDragMouseDown;
            MouseMove += OnDragMouseMove;
            MouseUp += OnDragMouseUp;
            _titleLabel.MouseDown += OnDragMouseDown;
            _titleLabel.MouseMove += OnDragMouseMove;
            _titleLabel.MouseUp += OnDragMouseUp;
            _timeLabel.MouseDown += OnDragMouseDown;
            _timeLabel.MouseMove += OnDragMouseMove;
            _timeLabel.MouseUp += OnDragMouseUp;
            _toggleButton.Cursor = Cursors.Default;
            _resetButton.Cursor = Cursors.Default;
            _editButton.Cursor = Cursors.Default;
            _notesButton.Cursor = Cursors.Default;
            _deleteButton.Cursor = Cursors.Default;

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

            int inset = _dragReady ? 1 : 0;
            var rect = new Rectangle(inset, inset, Width - 1 - inset * 2, Height - 1 - inset * 2);
            using (var path = RoundedRect(rect, CornerRadius))
            using (var fill = new SolidBrush(Theme.Surface))
            using (var pen = BorderPen())
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        // Holding the row reveals drag readiness; hover remains a lighter hint.
        private Pen BorderPen()
        {
            if (_dragReady) return new Pen(Color.FromArgb(180, Theme.BlueHover), 1.5f);
            if (_hover) return new Pen(Color.FromArgb(190, Theme.WorkspaceBorder));
            return new Pen(Theme.WorkspaceBorder);
        }

        private void OnHoverChanged(object sender, EventArgs e)
        {
            bool hovering = ClientRectangle.Contains(PointToClient(Cursor.Position));
            if (_hover == hovering) return;
            _hover = hovering;
            Invalidate();
        }

        private void OnDragMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _dragStart = Cursor.Position;
            _dragPending = true;
            _dragReady = false;
            _dragHoldTimer.Start();
        }

        private void OnDragHoldElapsed(object sender, EventArgs e)
        {
            _dragHoldTimer.Stop();
            if (!_dragPending || (Control.MouseButtons & MouseButtons.Left) == 0)
            {
                ResetDragState();
                return;
            }

            _dragReady = true;
            SetDragCursor(true);
            Invalidate();
        }

        private void OnDragMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragPending || (Control.MouseButtons & MouseButtons.Left) == 0) return;
            if (!_dragReady) return;

            if (!_dragging)
            {
                Size dragSize = SystemInformation.DragSize;
                var dragBounds = new Rectangle(
                    _dragStart.X - dragSize.Width / 2,
                    _dragStart.Y - dragSize.Height / 2,
                    dragSize.Width,
                    dragSize.Height);
                if (dragBounds.Contains(Cursor.Position)) return;
                _dragging = true;
            }

            var h = ReorderRequested;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void OnDragMouseUp(object sender, MouseEventArgs e)
        {
            bool reordered = _dragging;
            ResetDragState();

            if (!reordered) return;
            var h = ReorderCompleted;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void ResetDragState()
        {
            _dragHoldTimer.Stop();
            _dragPending = false;
            _dragReady = false;
            _dragging = false;
            SetDragCursor(false);
            Invalidate();
        }

        private void SetDragCursor(bool active)
        {
            Cursor cursor = active ? Cursors.SizeAll : Cursors.Default;
            Cursor = cursor;
            _titleLabel.Cursor = cursor;
            _timeLabel.Cursor = cursor;
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
            // Left-to-right: start/stop, edit (dev mode only), reset, notes, delete.
            int buttonTop = (HeaderHeight - _resetButton.Height) / 2;
            _deleteButton.Location = new Point(Width - _deleteButton.Width - 10, buttonTop);
            _notesButton.Location = new Point(_deleteButton.Left - _notesButton.Width - 8, buttonTop);
            _resetButton.Location = new Point(_notesButton.Left - _resetButton.Width - 8, buttonTop);

            int beforeReset = _resetButton.Left;
            if (_editVisible)
            {
                _editButton.Location = new Point(beforeReset - _editButton.Width - 8, buttonTop);
                beforeReset = _editButton.Left;
            }

            _toggleButton.Location = new Point(beforeReset - _toggleButton.Width - 8, buttonTop);

            int labelWidth = _toggleButton.Left - 22;
            if (labelWidth < 40) labelWidth = 40;
            _titleLabel.Width = labelWidth;
            _timeLabel.Width = labelWidth;
        }

        private void OnNotesClicked(object sender, EventArgs e)
        {
            using (var dialog = new NotesForm(_workspace))
            {
                dialog.NotesChanged += (s, args) =>
                {
                    var h = NotesChanged;
                    if (h != null) h(this, EventArgs.Empty);
                };
                dialog.ShowDialog(FindForm());
            }
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
            if (disposing)
            {
                if (_tooltip != null) _tooltip.Dispose();
                if (_dragHoldTimer != null) _dragHoldTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
