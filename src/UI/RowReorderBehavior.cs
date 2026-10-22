using System;
using System.Drawing;
using System.Windows.Forms;

namespace WorkMode.UI
{
    internal sealed class RowReorderBehavior : IDisposable
    {
        private readonly Control _row;
        private readonly Control[] _surfaces;
        private readonly Timer _holdTimer;
        private readonly Cursor[] _cursors;
        private Point _start;
        private Point _previewOffset;
        private bool _pending;
        private bool _dragging;
        private DragPreviewForm _preview;

        public bool IsReady { get; private set; }
        public bool SuppressClick { get; private set; }
        public event EventHandler ReorderRequested;
        public event EventHandler ReorderCompleted;

        public RowReorderBehavior(Control row, params Control[] surfaces)
        {
            _row = row;
            _surfaces = surfaces;
            _cursors = new Cursor[surfaces.Length];
            _holdTimer = new Timer { Interval = 50 };
            _holdTimer.Tick += OnHoldElapsed;
            for (int i = 0; i < surfaces.Length; i++)
            {
                Control surface = surfaces[i];
                _cursors[i] = surface.Cursor;
                surface.MouseDown += OnMouseDown;
                surface.MouseMove += OnMouseMove;
                surface.MouseUp += OnMouseUp;
            }
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_row.Enabled) return;
            SuppressClick = false;
            _start = Cursor.Position;
            Point origin = _row.PointToScreen(Point.Empty);
            _previewOffset = new Point(_start.X - origin.X, _start.Y - origin.Y);
            _pending = true;
            IsReady = false;
            _holdTimer.Start();
        }

        private void OnHoldElapsed(object sender, EventArgs e)
        {
            _holdTimer.Stop();
            if (!_pending || (Control.MouseButtons & MouseButtons.Left) == 0)
            {
                Reset();
                return;
            }
            IsReady = true;
            var snapshot = new Bitmap(_row.Width, _row.Height);
            _row.DrawToBitmap(snapshot, _row.ClientRectangle);
            _preview = new DragPreviewForm(snapshot);
            PositionPreview();
            _preview.Show(_row);
            foreach (Control surface in _surfaces) surface.Cursor = Cursors.SizeAll;
            _row.Invalidate();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_pending || !IsReady || (Control.MouseButtons & MouseButtons.Left) == 0) return;
            PositionPreview();
            if (!_dragging)
            {
                Size size = SystemInformation.DragSize;
                var bounds = new Rectangle(_start.X - size.Width / 2, _start.Y - size.Height / 2,
                    size.Width, size.Height);
                if (bounds.Contains(Cursor.Position)) return;
                _dragging = true;
                SuppressClick = true;
            }
            var handler = ReorderRequested;
            if (handler != null) handler(_row, EventArgs.Empty);
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            bool reordered = _dragging;
            Reset();
            if (!reordered) return;
            var handler = ReorderCompleted;
            if (handler != null) handler(_row, EventArgs.Empty);
        }

        private void Reset()
        {
            _holdTimer.Stop();
            if (_preview != null)
            {
                _preview.Close();
                _preview.Dispose();
                _preview = null;
            }
            _pending = false;
            IsReady = false;
            _dragging = false;
            for (int i = 0; i < _surfaces.Length; i++) _surfaces[i].Cursor = _cursors[i];
            _row.Invalidate();
        }

        private void PositionPreview()
        {
            Point pointer = Cursor.Position;
            _preview.Location = new Point(pointer.X - _previewOffset.X, pointer.Y - _previewOffset.Y);
        }

        internal static void MoveRow(FlowLayoutPanel list, Control draggedRow, int footerCount, Point pointer)
        {
            if (draggedRow == null || draggedRow.Parent != list) return;
            int currentIndex = list.Controls.GetChildIndex(draggedRow);
            int targetIndex = list.Controls.Count - footerCount;
            for (int i = 0; i < list.Controls.Count - footerCount; i++)
            {
                Control row = list.Controls[i];
                if (row == draggedRow) continue;
                if (pointer.Y < row.Top + row.Height / 2)
                {
                    targetIndex = i;
                    break;
                }
            }
            if (currentIndex < targetIndex) targetIndex--;
            if (targetIndex != currentIndex) list.Controls.SetChildIndex(draggedRow, targetIndex);
        }

        public void Dispose()
        {
            Reset();
            _holdTimer.Dispose();
            foreach (Control surface in _surfaces)
            {
                surface.MouseDown -= OnMouseDown;
                surface.MouseMove -= OnMouseMove;
                surface.MouseUp -= OnMouseUp;
            }
        }

        private sealed class DragPreviewForm : Form
        {
            private const int WsExTransparent = 0x20;
            private const int WsExToolWindow = 0x80;
            private const int WsExNoActivate = 0x08000000;
            private const int WmNcHitTest = 0x0084;
            private const int HtTransparent = -1;
            private readonly Bitmap _snapshot;

            public DragPreviewForm(Bitmap snapshot)
            {
                _snapshot = snapshot;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                Size = snapshot.Size;
                Opacity = 0.3;
            }

            protected override bool ShowWithoutActivation { get { return true; } }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams parameters = base.CreateParams;
                    parameters.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
                    return parameters;
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.DrawImageUnscaled(_snapshot, Point.Empty);
            }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == WmNcHitTest)
                {
                    message.Result = new IntPtr(HtTransparent);
                    return;
                }
                base.WndProc(ref message);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _snapshot.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
