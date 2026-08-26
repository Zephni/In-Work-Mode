using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WorkMode.Models;

namespace WorkMode.UI
{
    // A single row in the workspace list: title, time counted, and start/stop,
    // reset, edit, log-time and delete icon buttons. Rendered as a rounded dark
    // "card".
    public sealed class WorkspaceControl : UserControl
    {
        private const int CornerRadius = 10;
        private const int HeaderHeight = 56;
        private const int NotesDefaultHeight = 80;
        private const int NotesMinHeight = 40;
        private const int NotesMaxHeight = 400;
        private const int GripHeight = 10;
        private const int NotesSidePadding = 12;
        private const int NotesBottomPadding = 8;

        private readonly Workspace _workspace;
        private readonly Label _titleLabel;
        private readonly Label _timeLabel;
        private readonly Button _toggleButton;
        private readonly Button _resetButton;
        private readonly Button _editButton;
        private readonly Button _logTimeButton;
        private readonly Button _deleteButton;
        private readonly TextBox _notesBox;
        private readonly Panel _notesGrip;
        private readonly ToolTip _tooltip;
        private bool _selected;
        private bool _hover;
        // Desired edit-button visibility. Tracked separately from Button.Visible,
        // whose getter also depends on the whole parent chain (including the not
        // -yet-shown top-level Form during construction), which would otherwise
        // make LayoutButtons() miscalculate the gap for it.
        private bool _editVisible;
        private int _notesHeight = NotesDefaultHeight;
        private bool _resizingNotes;
        private int _resizeStartY;
        private int _resizeStartHeight;

        // Raised when the user toggles this workspace's timer on or off.
        public event EventHandler ToggleRequested;
        // Raised when the user asks to reset this workspace's counted time.
        public event EventHandler ResetRequested;
        // Raised when the user clicks the row to select it.
        public event EventHandler SelectRequested;
        // Raised when the user edits this workspace's notes.
        public event EventHandler NotesChanged;
        // Raised when the user asks to edit this workspace.
        public event EventHandler EditRequested;
        // Raised when the user asks to delete this workspace.
        public event EventHandler DeleteRequested;

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

        // Whether this row is currently the selected one (accent border). When
        // selected the notes textarea is revealed beneath the header.
        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                UpdateNotesVisibility();
                Invalidate();
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
            _toggleButton.Click += (s, e) => { EnsureSelected(); var h = ToggleRequested; if (h != null) h(this, EventArgs.Empty); };

            _resetButton = new Button
            {
                Size = new Size(34, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Image = Glyphs.Reset(20, Color.White),
                ImageAlign = ContentAlignment.MiddleCenter
            };
            Theme.StyleButton(_resetButton, Theme.Muted, Theme.MutedHover, Color.White);
            _resetButton.Click += (s, e) => { EnsureSelected(); var h = ResetRequested; if (h != null) h(this, EventArgs.Empty); };

            _editButton = new Button
            {
                Size = new Size(34, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Image = Glyphs.Edit(20, Color.White),
                ImageAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            Theme.StyleButton(_editButton, Theme.NeutralGray, Theme.NeutralGrayHover, Color.White);
            _editButton.Click += (s, e) => { EnsureSelected(); var h = EditRequested; if (h != null) h(this, EventArgs.Empty); };

            _logTimeButton = new Button
            {
                Size = new Size(34, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Image = Glyphs.Clock(20, Color.White),
                ImageAlign = ContentAlignment.MiddleCenter
            };
            Theme.StyleButton(_logTimeButton, Theme.NeutralGray, Theme.NeutralGrayHover, Color.White);
            _logTimeButton.Click += (s, e) => { EnsureSelected(); OnLogTimeClicked(s, e); };

            _deleteButton = new Button
            {
                Size = new Size(34, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Image = Glyphs.Trash(20, Color.White),
                ImageAlign = ContentAlignment.MiddleCenter
            };
            Theme.StyleButton(_deleteButton, Theme.NeutralGray, Theme.NeutralGrayHover, Color.White);
            _deleteButton.Click += (s, e) => { EnsureSelected(); var h = DeleteRequested; if (h != null) h(this, EventArgs.Empty); };

            // Multiline notes area, hidden until the row is selected.
            _notesBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.SurfaceAlt,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Regular),
                WordWrap = true,
                Visible = false,
                Text = _workspace.Notes ?? string.Empty
            };
            _notesBox.KeyUp += OnNotesKeyUp;
            _notesBox.HandleCreated += (s, e) => ApplyNotesPadding();

            // A slim drag handle beneath the notes box for resizing its height.
            _notesGrip = new Panel
            {
                Height = GripHeight,
                BackColor = Theme.Surface,
                Cursor = Cursors.SizeNS,
                Visible = false
            };
            _notesGrip.MouseDown += OnGripMouseDown;
            _notesGrip.MouseMove += OnGripMouseMove;
            _notesGrip.MouseUp += OnGripMouseUp;
            _notesGrip.Paint += OnGripPaint;

            _tooltip = new ToolTip { InitialDelay = 350, ReshowDelay = 200, ShowAlways = true };
            _tooltip.SetToolTip(_resetButton, "Reset");
            _tooltip.SetToolTip(_editButton, "Edit");
            _tooltip.SetToolTip(_logTimeButton, "Log time");
            _tooltip.SetToolTip(_deleteButton, "Delete");
            _tooltip.SetToolTip(_notesGrip, "Drag to resize notes");

            Controls.Add(_titleLabel);
            Controls.Add(_timeLabel);
            Controls.Add(_toggleButton);
            Controls.Add(_resetButton);
            Controls.Add(_editButton);
            Controls.Add(_logTimeButton);
            Controls.Add(_deleteButton);
            Controls.Add(_notesBox);
            Controls.Add(_notesGrip);

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

            Resize += (s, e) => { LayoutButtons(); LayoutNotes(); };
            LayoutButtons();
            LayoutNotes();
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

        // Selects this row without toggling it off if it's already selected;
        // used by the icon buttons so clicking them never deselects the row.
        private void EnsureSelected()
        {
            if (_selected) return;
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
            // Right-to-left: delete, log time, edit (dev mode only), reset, start/stop.
            int buttonTop = (HeaderHeight - _resetButton.Height) / 2;
            _deleteButton.Location = new Point(Width - _deleteButton.Width - 10, buttonTop);
            _logTimeButton.Location = new Point(_deleteButton.Left - _logTimeButton.Width - 8, buttonTop);

            int afterLogTime = _logTimeButton.Left;
            if (_editVisible)
            {
                _editButton.Location = new Point(afterLogTime - _editButton.Width - 8, buttonTop);
                afterLogTime = _editButton.Left;
            }

            _resetButton.Location = new Point(afterLogTime - _resetButton.Width - 8, buttonTop);
            _toggleButton.Location = new Point(_resetButton.Left - _toggleButton.Width - 8, buttonTop);

            int labelWidth = _toggleButton.Left - 22;
            if (labelWidth < 40) labelWidth = 40;
            _titleLabel.Width = labelWidth;
            _timeLabel.Width = labelWidth;
        }

        // Positions the notes box and its resize grip below the header.
        private void LayoutNotes()
        {
            int width = Width - NotesSidePadding * 2;
            if (width < 40) width = 40;
            _notesBox.SetBounds(NotesSidePadding, HeaderHeight, width, _notesHeight);
            _notesGrip.SetBounds(NotesSidePadding, HeaderHeight + _notesHeight, width, GripHeight);
            ApplyNotesPadding();
        }

        // Shows or hides the notes area and grows/shrinks the card to fit.
        private void UpdateNotesVisibility()
        {
            _notesBox.Visible = _selected;
            _notesGrip.Visible = _selected;
            Height = _selected
                ? HeaderHeight + _notesHeight + GripHeight + NotesBottomPadding
                : HeaderHeight;
            LayoutNotes();
        }

        private void ApplyNotesPadding()
        {
            if (!_notesBox.IsHandleCreated) return;
            Interop.NativeMethods.SetTextBoxPadding(
                _notesBox.Handle, 5, 4, _notesBox.Width, _notesBox.Height);
        }

        private void OnNotesKeyUp(object sender, KeyEventArgs e)
        {
            _workspace.Notes = _notesBox.Text;
            var h = NotesChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void OnLogTimeClicked(object sender, EventArgs e)
        {
            _workspace.Sync();
            string line = DateTime.Now.ToString("yyyy-MM-dd") + " hours: " + Workspace.Format(_workspace.ElapsedSeconds);
            string current = _notesBox.Text;
            if (string.IsNullOrEmpty(current))
                _notesBox.Text = line;
            else if (current.StartsWith("\r\n") || current.StartsWith("\n"))
                _notesBox.Text = line + current;
            else
                _notesBox.Text = line + "\r\n" + current;

            _workspace.Notes = _notesBox.Text;
            var h = NotesChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private void OnGripMouseDown(object sender, MouseEventArgs e)
        {
            _resizingNotes = true;
            _resizeStartY = Cursor.Position.Y;
            _resizeStartHeight = _notesHeight;
        }

        private void OnGripMouseMove(object sender, MouseEventArgs e)
        {
            if (!_resizingNotes) return;
            int height = _resizeStartHeight + (Cursor.Position.Y - _resizeStartY);
            if (height < NotesMinHeight) height = NotesMinHeight;
            if (height > NotesMaxHeight) height = NotesMaxHeight;
            if (height == _notesHeight) return;

            _notesHeight = height;
            Height = HeaderHeight + _notesHeight + GripHeight + NotesBottomPadding;
            LayoutNotes();
        }

        private void OnGripMouseUp(object sender, MouseEventArgs e)
        {
            _resizingNotes = false;
        }

        // Draws a subtle two-line grab handle centred in the grip.
        private void OnGripPaint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = _notesGrip.Width / 2;
            int cy = _notesGrip.Height / 2;
            using (var pen = new Pen(Theme.TextMuted, 1.4f))
            {
                e.Graphics.DrawLine(pen, cx - 12, cy - 2, cx + 12, cy - 2);
                e.Graphics.DrawLine(pen, cx - 12, cy + 2, cx + 12, cy + 2);
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
            if (disposing && _tooltip != null) _tooltip.Dispose();
            base.Dispose(disposing);
        }
    }
}
