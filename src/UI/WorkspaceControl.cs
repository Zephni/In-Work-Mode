using System;
using System.Drawing;
using System.Windows.Forms;
using WorkMode.Models;

namespace WorkMode.UI
{
    // A single row in the workspace list: title, time counted, a start/stop
    // toggle button and an edit button.
    public sealed class WorkspaceControl : UserControl
    {
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

            Height = 52;
            Margin = new Padding(0, 0, 0, 6);
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = Color.White;

            _titleLabel = new Label
            {
                Location = new Point(10, 6),
                AutoSize = false,
                Size = new Size(220, 20),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                AutoEllipsis = true
            };

            _timeLabel = new Label
            {
                Location = new Point(10, 28),
                AutoSize = false,
                Size = new Size(220, 18),
                Font = new Font("Consolas", 10f, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60)
            };

            _toggleButton = new Button
            {
                Size = new Size(70, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _toggleButton.Click += (s, e) => { var h = ToggleRequested; if (h != null) h(this, EventArgs.Empty); };

            _editButton = new Button
            {
                Text = "Edit",
                Size = new Size(60, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _editButton.Click += (s, e) => { var h = EditRequested; if (h != null) h(this, EventArgs.Empty); };

            Controls.Add(_titleLabel);
            Controls.Add(_timeLabel);
            Controls.Add(_toggleButton);
            Controls.Add(_editButton);

            Resize += (s, e) => LayoutButtons();
            LayoutButtons();
            Refresh();
        }

        private void LayoutButtons()
        {
            _editButton.Location = new Point(Width - _editButton.Width - 8, 6);
            _toggleButton.Location = new Point(_editButton.Left - _toggleButton.Width - 6, 6);

            int labelWidth = _toggleButton.Left - 18;
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
                _toggleButton.BackColor = Color.FromArgb(230, 90, 90);
                _toggleButton.ForeColor = Color.White;
            }
            else
            {
                _toggleButton.Text = "Start";
                _toggleButton.BackColor = Color.FromArgb(80, 190, 120);
                _toggleButton.ForeColor = Color.White;
            }

            base.Refresh();
        }
    }
}
