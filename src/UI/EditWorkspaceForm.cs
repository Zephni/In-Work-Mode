using System;
using System.Drawing;
using System.Windows.Forms;
using WorkMode.Models;

namespace WorkMode.UI
{
    // Modal dialog used to add a new workspace or edit an existing one. Collects
    // a title and a time-counted value (HH:MM:SS).
    public sealed class EditWorkspaceForm : Form
    {
        private readonly TextBox _titleBox;
        private readonly TextBox _timeBox;

        public string WorkspaceTitle { get; private set; }
        public long ElapsedSeconds { get; private set; }

        public EditWorkspaceForm(string caption, string title, long elapsedSeconds)
        {
            Text = caption;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(320, 150);

            var titleLabel = new Label
            {
                Text = "Title",
                Location = new Point(12, 15),
                AutoSize = true
            };
            _titleBox = new TextBox
            {
                Text = title ?? string.Empty,
                Location = new Point(12, 34),
                Width = 296
            };

            var timeLabel = new Label
            {
                Text = "Time counted (HH:MM:SS)",
                Location = new Point(12, 66),
                AutoSize = true
            };
            _timeBox = new TextBox
            {
                Text = Workspace.Format(elapsedSeconds),
                Location = new Point(12, 85),
                Width = 296
            };

            var okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(152, 116),
                Width = 75
            };
            okButton.Click += OnOk;

            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(233, 116),
                Width = 75
            };

            Controls.Add(titleLabel);
            Controls.Add(_titleBox);
            Controls.Add(timeLabel);
            Controls.Add(_timeBox);
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void OnOk(object sender, EventArgs e)
        {
            string title = _titleBox.Text.Trim();
            if (title.Length == 0)
            {
                MessageBox.Show(this, "Please enter a title.", "Work Mode",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            long seconds;
            if (!Workspace.TryParseTime(_timeBox.Text, out seconds))
            {
                MessageBox.Show(this, "Please enter the time as HH:MM:SS.", "Work Mode",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            WorkspaceTitle = title;
            ElapsedSeconds = seconds;
        }
    }
}
