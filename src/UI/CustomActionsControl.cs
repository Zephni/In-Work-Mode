using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WorkMode.Actions;
using WorkMode.Configuration;
using WorkMode.Models;

namespace WorkMode.UI
{
    internal sealed class CustomActionsControl : UserControl
    {
        private readonly AppConfig _config;
        private readonly FlowLayoutPanel _list;
        private readonly AddWorkspaceRow _add;

        public bool IsRunning { get; private set; }

        public CustomActionsControl(AppConfig config)
        {
            _config = config;
            BackColor = Theme.Background;
            _list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(10),
                BackColor = Theme.Background
            };
            _list.HandleCreated += (s, e) => Interop.NativeMethods.UseDarkScrollBar(_list.Handle);
            _list.SizeChanged += (s, e) => ResizeRows();
            Controls.Add(_list);
            _add = new AddWorkspaceRow("Add Custom Action");
            _add.AddRequested += (s, e) => EditAction(null);
            Rebuild();
        }

        private void ResizeRows()
        {
            foreach (Control row in _list.Controls)
                row.Width = Math.Max(100, _list.ClientSize.Width - _list.Padding.Horizontal - row.Margin.Horizontal);
        }

        private void Rebuild()
        {
            _list.SuspendLayout();
            _list.Controls.Remove(_add);
            while (_list.Controls.Count > 0)
                _list.Controls[0].Dispose();
            foreach (CustomAction action in _config.CustomActions)
                _list.Controls.Add(CreateRow(action));
            _list.Controls.Add(_add);
            _list.ResumeLayout();
            ResizeRows();
        }

        private Control CreateRow(CustomAction action)
        {
            var row = new CustomActionRow(action);
            row.EditRequested += (s, e) => EditAction(action);
            row.DeleteRequested += (s, e) =>
            {
                if (MessageBox.Show(this, "Delete \"" + action.Title + "\"?", "Delete Custom Action",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
                int index = _config.CustomActions.IndexOf(action);
                _config.CustomActions.RemoveAt(index);
                if (!SaveActions()) _config.CustomActions.Insert(index, action);
                Rebuild();
            };
            row.RunRequested += async (s, e) =>
            {
                IsRunning = true;
                SetEditingEnabled(false);
                row.SetRunning();
                try
                {
                    CommandResult result = await CommandRunner.RunAsync(action);
                    if (IsDisposed || row.IsDisposed) return;
                    if (result.ExitCode != 0 || !string.IsNullOrWhiteSpace(result.Error))
                    {
                        row.SetError();
                        ShowError(action.Title, "Exit code: " + result.ExitCode + Environment.NewLine +
                            result.Error + Environment.NewLine + result.Output);
                    }
                    else
                    {
                        row.SetSuccess(result.TerminalProcessId.HasValue);
                    }
                }
                catch (Exception error)
                {
                    if (!(error is Win32Exception || error is IOException ||
                        error is UnauthorizedAccessException || error is ArgumentException ||
                        error is NotSupportedException || error is InvalidOperationException ||
                        error is System.Security.SecurityException)) throw;
                    if (IsDisposed || row.IsDisposed) return;
                    row.SetError();
                    ShowError(action.Title, error.Message);
                }
                finally
                {
                    IsRunning = false;
                    if (!IsDisposed) SetEditingEnabled(true);
                }
            };
            return row;
        }

        private void SetEditingEnabled(bool enabled)
        {
            foreach (Control row in _list.Controls) row.Enabled = enabled;
        }

        private void EditAction(CustomAction action)
        {
            using (var dialog = new EditActionForm(action))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                int index = action == null ? _config.CustomActions.Count : _config.CustomActions.IndexOf(action);
                if (action == null) _config.CustomActions.Add(dialog.Action);
                else _config.CustomActions[index] = dialog.Action;
                if (!SaveActions())
                {
                    if (action == null) _config.CustomActions.RemoveAt(index);
                    else _config.CustomActions[index] = action;
                }
                Rebuild();
            }
        }

        private bool SaveActions()
        {
            try
            {
                _config.SaveOrThrow();
                return true;
            }
            catch (Exception error)
            {
                if (!(error is IOException || error is UnauthorizedAccessException ||
                    error is System.Security.SecurityException)) throw;
                ShowError("Could not save custom actions", error.Message);
                return false;
            }
        }

        private void ShowError(string title, string details)
        {
            using (var dialog = new Form())
            {
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new Size(600, 360);
                dialog.MinimizeBox = false;
                Theme.ApplyForm(dialog);
                dialog.HandleCreated += (s, e) => Interop.NativeMethods.UseDarkTitleBar(dialog.Handle);
                dialog.Controls.Add(new TextBox
                {
                    Text = details, Multiline = true, ReadOnly = true,
                    Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, WordWrap = false,
                    BackColor = Theme.Surface, ForeColor = Theme.Text,
                    Font = new Font("Consolas", 10f), BorderStyle = BorderStyle.None
                });
                dialog.ShowDialog(this);
            }
        }
    }
}
