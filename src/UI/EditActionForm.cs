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
        private readonly ComboBox _display;
        private static readonly string[] Languages =
            { CustomAction.BatchCommand, CustomAction.PythonCommand, CustomAction.PowerShellCommand };
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
            Controls.Add(new Label { Text = "Language", AutoSize = true, Location = new Point(12, 76), ForeColor = Theme.TextMuted });
            Controls.Add(new Label { Text = "Display", AutoSize = true, Location = new Point(286, 76), ForeColor = Theme.TextMuted });
            _type = AddSelect("Language", 12, 262);
            _display = AddSelect("Display", 286, 262);
            foreach (string language in Languages) _type.Items.Add(CustomAction.GetLanguageName(language));
            int languageIndex = Array.IndexOf(Languages, action.Type);
            if (languageIndex < 0)
            {
                _type.Items.Add(CustomAction.GetLanguageName(action.Type));
                languageIndex = Languages.Length;
            }
            _type.SelectedIndex = languageIndex;
            _display.Items.Add(CustomAction.TerminalCommandName);
            _display.Items.Add(CustomAction.CloseTerminalCommandName);
            _display.Items.Add(CustomAction.BackgroundCommandName);
            _display.DropDownWidth = 340;
            _display.SelectedIndex = action.OpenInTerminal ? (action.CloseTerminalAfterExecution ? 1 : 0) : 2;
            Resize += (s, e) =>
            {
                int width = (ClientSize.Width - 36) / 2;
                _type.Width = width;
                _display.Left = width + 24;
                _display.Width = ClientSize.Width - _display.Left - 12;
                foreach (Control control in Controls)
                    if (control is Label && control.Text == "Display") control.Left = _display.Left;
            };

            _command = AddInput("Script", action.Command, 132, 182);
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
            Action updateHint = () => hint.Text = _display.SelectedIndex == 2
                ? "Runs hidden without interactive input. Output and errors are captured by the app."
                : _display.SelectedIndex == 1
                    ? "Interactive input supported. Closes when the script exits, including on errors."
                    : "Interactive input supported. The check icon confirms launch, not script completion.";
            _display.SelectedIndexChanged += (s, e) => updateHint();
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
                if (_type.SelectedIndex < 0 || _type.SelectedIndex >= Languages.Length)
                {
                    MessageBox.Show(this, "Please select a supported language.", "Custom Action",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Action = new CustomAction
                {
                    Title = _title.Text.Trim(), Type = Languages[_type.SelectedIndex],
                    Command = _command.Text, WorkingDirectory = _directory.Text.Trim(),
                    OpenInTerminal = _display.SelectedIndex != 2,
                    CloseTerminalAfterExecution = _display.SelectedIndex == 1
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

        private ComboBox AddSelect(string name, int left, int width)
        {
            var select = new ComboBox
            {
                AccessibleName = name, Location = new Point(left, 96), Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text,
                Font = new Font("Segoe UI", 10f), DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 24
            };
            select.DrawItem += DrawActionType;
            Controls.Add(select);
            return select;
        }

        private void DrawActionType(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var select = (ComboBox)sender;
            bool selected = (e.State & DrawItemState.Selected) != 0 &&
                (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var background = new SolidBrush(selected ? Theme.Accent : Theme.SurfaceAlt))
                e.Graphics.FillRectangle(background, e.Bounds);
            Rectangle textBounds = e.Bounds;
            textBounds.Inflate(-6, 0);
            TextRenderer.DrawText(e.Graphics, select.Items[e.Index].ToString(), select.Font,
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
