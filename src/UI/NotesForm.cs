using System;
using System.Drawing;
using System.Windows.Forms;
using WorkMode.Models;

namespace WorkMode.UI
{
    public sealed class NotesForm : Form
    {
        private readonly Workspace _workspace;
        private readonly TextBox _notesBox;

        public event EventHandler NotesChanged;

        public NotesForm(Workspace workspace)
        {
            _workspace = workspace;

            Text = workspace.Title + " - Notes";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(420, 320);
            ClientSize = new Size(520, 400);
            MaximizeBox = false;
            MinimizeBox = false;
            Theme.ApplyForm(this);

            _notesBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.SurfaceAlt,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Regular),
                WordWrap = true,
                Text = workspace.Notes ?? string.Empty,
                Location = new Point(12, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _notesBox.TextChanged += OnNotesChanged;
            _notesBox.HandleCreated += (s, e) => ApplyNotesPadding();

            var insertTimeButton = new Button
            {
                Text = "Insert time",
                Image = Glyphs.Clock(18, Color.White),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(8, 0, 10, 0),
                Size = new Size(128, 34),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            Theme.StyleButton(insertTimeButton, Theme.NeutralGray, Theme.NeutralGrayHover, Color.White);
            insertTimeButton.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Regular);
            insertTimeButton.Click += OnInsertTime;

            var closeButton = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Size = new Size(84, 34),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            Theme.StyleSurfaceButton(closeButton);
            closeButton.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Regular);

            Controls.Add(_notesBox);
            Controls.Add(insertTimeButton);
            Controls.Add(closeButton);
            CancelButton = closeButton;

            Resize += (s, e) => LayoutControls(insertTimeButton, closeButton);
            LayoutControls(insertTimeButton, closeButton);
            Shown += (s, e) =>
            {
                _notesBox.SelectionStart = _notesBox.TextLength;
                _notesBox.SelectionLength = 0;
                _notesBox.Focus();
                _notesBox.ScrollToCaret();
            };
        }

        private void LayoutControls(Button insertTimeButton, Button closeButton)
        {
            const int margin = 12;
            const int gap = 10;
            int buttonTop = ClientSize.Height - margin - insertTimeButton.Height;
            _notesBox.SetBounds(
                margin,
                margin,
                ClientSize.Width - margin * 2,
                buttonTop - gap - margin);
            insertTimeButton.Location = new Point(margin, buttonTop);
            closeButton.Location = new Point(ClientSize.Width - margin - closeButton.Width, buttonTop);
            ApplyNotesPadding();
        }

        private void ApplyNotesPadding()
        {
            if (!_notesBox.IsHandleCreated) return;
            Interop.NativeMethods.SetTextBoxPadding(
                _notesBox.Handle, 7, 6, _notesBox.Width, _notesBox.Height);
        }

        private void OnNotesChanged(object sender, EventArgs e)
        {
            _workspace.Notes = _notesBox.Text;
            var handler = NotesChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnInsertTime(object sender, EventArgs e)
        {
            _workspace.Sync();
            string line = DateTime.Now.ToString("yyyy-MM-dd") + " hours: " +
                          Workspace.Format(_workspace.ElapsedSeconds);
            string current = _notesBox.Text;
            if (string.IsNullOrEmpty(current))
                _notesBox.Text = line;
            else if (current.StartsWith("\r\n") || current.StartsWith("\n"))
                _notesBox.Text = line + current;
            else
                _notesBox.Text = line + Environment.NewLine + current;
            _notesBox.SelectionStart = line.Length;
            _notesBox.Focus();
        }
    }
}