using System;
using System.Drawing;
using System.Windows.Forms;
using WorkMode.Models;

namespace WorkMode.UI
{
    internal sealed class EditActionForm : Form
    {
        private readonly TextBox _title;
        private readonly ComboBox _type;
        private readonly TextBox _command;
        private readonly TextBox _directory;

        public CustomAction Action { get; private set; }

        public EditActionForm(CustomAction action)
        {
            Text = action == null ? "Add Custom Action" : "Edit Custom Action";
            action = action ?? new CustomAction { OpenInTerminal = true };
            Theme.ApplyForm(this);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ClientSize = new Size(560, 470);
            MinimumSize = Size;

            _title = AddInput("Name", action.Title, 12, 28);
            Controls.Add(new Label { Text = "Action type", AutoSize = true, Location = new Point(12, 76), ForeColor = Theme.TextMuted });
            _type = new ComboBox
            {
                Location = new Point(12, 96), Width = ClientSize.Width - 24,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text,
                Font = new Font("Segoe UI", 10f), DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 24
            };
            _type.DrawItem += DrawActionType;
            _type.Items.Add(CustomAction.TerminalCommandName);
            _type.Items.Add(CustomAction.BackgroundCommandName);
            if (action.Type != CustomAction.BatchCommand)
                _type.Items.Add("Unsupported: " + action.Type);
            _type.SelectedIndex = action.Type == CustomAction.BatchCommand ? (action.OpenInTerminal ? 0 : 1) : 2;
            Controls.Add(_type);

            _command = AddInput("Command (batch script)", action.Command, 132, 182);
            _command.Multiline = true;
            _command.AcceptsReturn = true;
            _command.ScrollBars = ScrollBars.Vertical;
            _command.Font = new Font("Consolas", 10f);
            _command.Anchor |= AnchorStyles.Bottom;
            var directoryLabel = new Label
            {
                Text = "Working folder (optional; defaults to the app folder)", AutoSize = true,
                Location = new Point(12, 346), ForeColor = Theme.TextMuted,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            Controls.Add(directoryLabel);
            _directory = AddInput(null, action.WorkingDirectory, 346, 28);
            _directory.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            var hint = new Label
            {
                Location = new Point(12, 401), Size = new Size(ClientSize.Width - 24, 24), ForeColor = Theme.TextMuted,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Action updateHint = () => hint.Text = _type.SelectedIndex == 0
                ? "Interactive input supported. The check icon confirms launch, not script completion."
                : "Runs hidden without interactive input. Output and errors are captured by the app.";
            _type.SelectedIndexChanged += (s, e) => updateHint();
            updateHint();
            Controls.Add(hint);

            var save = new Button
            {
                Text = "Save", Location = new Point(ClientSize.Width - 178, 432), Size = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            Theme.StyleButton(save, Theme.Accent, Theme.AccentHover, Color.White);
            save.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_title.Text) || string.IsNullOrWhiteSpace(_command.Text))
                {
                    MessageBox.Show(this, "Please enter a name and a command.", "Custom Action",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (_type.SelectedIndex < 0 || _type.SelectedIndex > 1)
                {
                    MessageBox.Show(this, "Please select a supported action type.", "Custom Action",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Action = new CustomAction
                {
                    Title = _title.Text.Trim(), Type = CustomAction.BatchCommand,
                    Command = _command.Text.Trim(), WorkingDirectory = _directory.Text.Trim(),
                    OpenInTerminal = _type.SelectedIndex == 0
                };
                DialogResult = DialogResult.OK;
            };
            var cancel = new Button
            {
                Text = "Cancel", DialogResult = DialogResult.Cancel,
                Location = new Point(ClientSize.Width - 92, 432), Size = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            Theme.StyleSurfaceButton(cancel);
            Controls.Add(save);
            Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void DrawActionType(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0 &&
                (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var background = new SolidBrush(selected ? Theme.Accent : Theme.SurfaceAlt))
                e.Graphics.FillRectangle(background, e.Bounds);
            Rectangle textBounds = e.Bounds;
            textBounds.Inflate(-6, 0);
            TextRenderer.DrawText(e.Graphics, _type.Items[e.Index].ToString(), _type.Font,
                textBounds, selected ? Color.White : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            e.DrawFocusRectangle();
        }

        private TextBox AddInput(string label, string value, int top, int height)
        {
            if (label != null)
                Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(12, top), ForeColor = Theme.TextMuted });
            var input = new TextBox
            {
                Text = value, Location = new Point(12, top + 20), Size = new Size(ClientSize.Width - 24, height),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoSize = false, BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f)
            };
            Controls.Add(input);
            return input;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Interop.NativeMethods.UseDarkTitleBar(Handle);
        }
    }
}
