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
            ClientSize = new Size(320, 160);
            Theme.ApplyForm(this);

            var titleLabel = new Label
            {
                Text = "Title",
                Location = new Point(12, 15),
                AutoSize = true,
                ForeColor = Theme.TextMuted
            };
            _titleBox = new TextBox
            {
                Text = title ?? string.Empty,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.SurfaceAlt,
                ForeColor = Theme.Text
            };
            var titlePanel = CreateInputPanel(_titleBox, new Point(12, 34), 296);

            var timeLabel = new Label
            {
                Text = "Time counted (HH:MM:SS)",
                Location = new Point(12, 66),
                AutoSize = true,
                ForeColor = Theme.TextMuted
            };
            _timeBox = new TextBox
            {
                Text = Workspace.Format(elapsedSeconds),
                BorderStyle = BorderStyle.None,
                BackColor = Theme.SurfaceAlt,
                ForeColor = Theme.Text,
                Font = new Font("Consolas", 9f, FontStyle.Regular)
            };
            var timePanel = CreateInputPanel(_timeBox, new Point(12, 85), 296);

            var okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(152, 120),
                Size = new Size(75, 30)
            };
            Theme.StyleButton(okButton, Theme.Accent, Theme.AccentHover, Color.White);
            okButton.Click += OnOk;

            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(233, 120),
                Size = new Size(75, 30)
            };
            Theme.StyleSurfaceButton(cancelButton);

            Controls.Add(titleLabel);
            Controls.Add(titlePanel);
            Controls.Add(timeLabel);
            Controls.Add(timePanel);
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        // Hosts a borderless TextBox inside a bordered panel so the text has a
        // natural inset (padding) on all sides instead of hugging the border.
        private static Panel CreateInputPanel(TextBox box, Point location, int width)
        {
            const int padX = 8;
            const int padY = 6;

            var panel = new Panel
            {
                Location = location,
                Width = width,
                BackColor = Theme.SurfaceAlt,
                BorderStyle = BorderStyle.FixedSingle
            };

            box.Location = new Point(padX, padY);
            box.Width = width - (padX * 2) - 2;
            panel.Height = box.PreferredHeight + (padY * 2);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            panel.Controls.Add(box);
            return panel;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Interop.NativeMethods.UseDarkTitleBar(Handle);
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
