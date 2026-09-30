using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WorkMode.Activity;
using WorkMode.Configuration;
using WorkMode.Models;

namespace WorkMode.UI
{
    // The main application window. Shows the list of workspaces, lets the user
    // add / edit them, and starts / stops per-workspace time tracking. Running
    // workspaces are counted up in real time and persisted to config.ini every
    // second.
    public sealed class MainForm : Form
    {
        // The window size used on first launch and whenever the user resets it
        // via the "restore default size" button. Change these to alter both.
        private const int DefaultWindowWidth = 740;
        private const int DefaultWindowHeight = 420;

        private readonly AppConfig _config;
        private readonly ActivitySimulator _activity;
        private readonly FlowLayoutPanel _list;
        private readonly Timer _tickTimer;
        private readonly AddWorkspaceRow _addRow;
        private readonly SimulationToggleRow _simToggleRow;
        private readonly HashSet<Keys> _heldKeys = new HashSet<Keys>();
        private bool _resizeRowsPending;

        public MainForm(AppConfig config)
        {
            _config = config ?? new AppConfig();
            _activity = new ActivitySimulator();

            Text = "In Work Mode";
            StartPosition = FormStartPosition.CenterScreen;
            // Minimum height is roughly half the old 300px floor so the window can
            // sit compactly; the app opens at this minimum unless a size is saved.
            MinimumSize = new Size(360, 170);

            int startWidth = _config.WindowWidth > 0 ? _config.WindowWidth : DefaultWindowWidth;
            int startHeight = _config.WindowHeight > 0 ? _config.WindowHeight : DefaultWindowHeight;
            Size = new Size(startWidth, startHeight);

            Theme.ApplyForm(this);

            Icon appIcon = IconLoader.LoadAppIcon();
            if (appIcon != null) Icon = appIcon;

            // Scrolling list of workspaces (fills the window above the add button).
            _list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = Theme.Background
            };
            // Darken the native scrollbar so it matches the rest of the theme.
            _list.HandleCreated += (s, e) => Interop.NativeMethods.UseDarkScrollBar(_list.Handle);

            Controls.Add(_list);

            foreach (Workspace ws in _config.Workspaces)
                AddRow(ws);

            // Placeholder "card", that opens the add dialog.
            _addRow = new AddWorkspaceRow();
            _addRow.AddRequested += OnAddWorkspace;
            _list.Controls.Add(_addRow);

            // Developer-only toggle for the activity simulation, pinned below the
            // add-workspace card. Hidden unless dev mode is enabled.
            _simToggleRow = new SimulationToggleRow();
            _simToggleRow.ToggleRequested += OnToggleSimulation;
            _simToggleRow.Visible = _config.DevMode;
            _list.Controls.Add(_simToggleRow);

            // Fit each row to the list width.
            _list.Resize += (s, e) => QueueResizeRows();
            ResizeRows();

            _tickTimer = new Timer { Interval = 1000 };
            _tickTimer.Tick += OnTick;
            _tickTimer.Start();

            ResizeEnd += (s, e) => SaveWindowSize();
            FormClosing += (s, e) => Cleanup();

            // Secret combo: hold Z, E, P and tap H to toggle dev mode.
            // Only tracked while focused.
            KeyPreview = true;
            KeyDown += OnMainFormKeyDown;
            KeyUp += OnMainFormKeyUp;
            Deactivate += (s, e) => _heldKeys.Clear();
        }

        // Paint the title bar dark once the native window handle exists.
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Interop.NativeMethods.UseDarkTitleBar(Handle);
        }

        // Records the current window size so it can be restored next launch.
        private void SaveWindowSize()
        {
            if (WindowState != FormWindowState.Normal) return;
            _config.WindowWidth = Width;
            _config.WindowHeight = Height;
            _config.Save();
        }

        // Tracks held-down keys; holding Z+E+P and tapping H toggles dev mode.
        private void OnMainFormKeyDown(object sender, KeyEventArgs e)
        {
            _heldKeys.Add(e.KeyCode);
            if (e.KeyCode == Keys.H &&
                _heldKeys.Contains(Keys.Z) && _heldKeys.Contains(Keys.E) && _heldKeys.Contains(Keys.P))
            {
                ToggleDevMode();
            }
        }

        private void OnMainFormKeyUp(object sender, KeyEventArgs e)
        {
            _heldKeys.Remove(e.KeyCode);
        }

        // Flips dev mode, persists it, and shows/hides the simulation toggle.
        private void ToggleDevMode()
        {
            _config.DevMode = !_config.DevMode;
            _config.Save();

            // Reveal / hide the simulation toggle. Leaving dev mode also stops any
            // running simulation so it can't keep going with no visible control.
            _simToggleRow.Visible = _config.DevMode;
            if (!_config.DevMode && _activity.IsRunning)
            {
                _activity.Stop();
                _simToggleRow.Active = false;
            }
        }

        // Dev-only: toggles the harmless activity simulation on and off.
        private void OnToggleSimulation(object sender, EventArgs e)
        {
            if (_activity.IsRunning) _activity.Stop();
            else _activity.Start();

            _simToggleRow.Active = _activity.IsRunning;
        }

        private void AddRow(Workspace ws)
        {
            var row = new WorkspaceControl(ws);
            row.ToggleRequested += OnToggle;
            row.ResetRequested += OnReset;
            row.NotesChanged += OnNotesChanged;
            row.EditRequested += OnEditWorkspace;
            row.DeleteRequested += OnDeleteWorkspace;
            row.ReorderRequested += OnWorkspaceReorderRequested;
            row.ReorderCompleted += OnWorkspaceReorderCompleted;
            _list.Controls.Add(row);
            SizeRow(row);

            PinFooterRows();
        }

        // Keeps the add-workspace card and the dev-only simulation toggle pinned to
        // the bottom of the list, in that order.
        private void PinFooterRows()
        {
            if (_addRow == null || _simToggleRow == null) return;
            _list.Controls.SetChildIndex(_simToggleRow, _list.Controls.Count - 1);
            _list.Controls.SetChildIndex(_addRow, _list.Controls.Count - 2);
        }

        private void OnWorkspaceReorderRequested(object sender, EventArgs e)
        {
            var draggedRow = sender as WorkspaceControl;
            if (draggedRow == null || draggedRow.Parent != _list) return;

            Point pointer = _list.PointToClient(Cursor.Position);
            int currentIndex = _list.Controls.GetChildIndex(draggedRow);
            int targetIndex = _list.Controls.Count - 2;

            foreach (Control control in _list.Controls)
            {
                var row = control as WorkspaceControl;
                if (row == null || row == draggedRow) continue;
                if (pointer.Y < row.Top + row.Height / 2)
                {
                    targetIndex = _list.Controls.GetChildIndex(row);
                    break;
                }
            }

            if (currentIndex < targetIndex) targetIndex--;
            if (targetIndex != currentIndex)
            {
                _list.Controls.SetChildIndex(draggedRow, targetIndex);
                PinFooterRows();
            }
        }

        private void OnWorkspaceReorderCompleted(object sender, EventArgs e)
        {
            _config.Workspaces.Clear();
            foreach (Control control in _list.Controls)
            {
                var row = control as WorkspaceControl;
                if (row != null) _config.Workspaces.Add(row.Workspace);
            }
            _config.Save();
        }

        // Persists notes as the user types into a workspace's notes area.
        private void OnNotesChanged(object sender, EventArgs e)
        {
            _config.Save();
        }

        private void OnDeleteWorkspace(object sender, EventArgs e)
        {
            var row = (WorkspaceControl)sender;
            if (row == null) return;

            Workspace ws = row.Workspace;
            ws.Stop();

            _list.Controls.Remove(row);
            row.Dispose();
            _config.Workspaces.Remove(ws);

            UpdateActivityState();
            _config.Save();
        }

        private void ResizeRows()
        {
            foreach (Control row in _list.Controls)
                SizeRow(row);
        }

        private void QueueResizeRows()
        {
            if (_resizeRowsPending || !_list.IsHandleCreated) return;
            _resizeRowsPending = true;
            _list.BeginInvoke((Action)(() =>
            {
                _resizeRowsPending = false;
                if (_list.IsDisposed) return;
                ResizeRows();
            }));
        }

        private void SizeRow(Control row)
        {
            int width = _list.ClientSize.Width - _list.Padding.Horizontal - row.Margin.Horizontal;
            if (width < 100) width = 100;
            row.Width = width;
        }

        private void OnAddWorkspace(object sender, EventArgs e)
        {
            using (var dialog = new EditWorkspaceForm("Add Workspace", string.Empty, 0))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                var ws = new Workspace(dialog.WorkspaceTitle, dialog.ElapsedSeconds);
                _config.Workspaces.Add(ws);
                AddRow(ws);
                _config.Save();
            }
        }

        private void OnToggle(object sender, EventArgs e)
        {
            var row = (WorkspaceControl)sender;
            Workspace ws = row.Workspace;

            if (ws.IsRunning) ws.Stop();
            else ws.Start();

            row.Refresh();
            UpdateActivityState();
            _config.Save();
        }

        private void OnReset(object sender, EventArgs e)
        {
            var row = (WorkspaceControl)sender;
            Workspace ws = row.Workspace;

            var result = MessageBox.Show(this,
                string.Format("Reset the timer for \"{0}\" to 00:00:00?", ws.Title),
                "Reset Timer", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes) return;

            ws.Reset();
            row.Refresh();
            _config.Save();
        }

        private void OnEditWorkspace(object sender, EventArgs e)
        {
            var row = (WorkspaceControl)sender;
            if (row == null) return;

            Workspace ws = row.Workspace;

            // Capture the live total before editing.
            ws.Sync();

            using (var dialog = new EditWorkspaceForm("Edit Workspace", ws.Title, ws.ElapsedSeconds))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                bool wasRunning = ws.IsRunning;
                if (wasRunning) ws.Stop();

                ws.Title = dialog.WorkspaceTitle;
                ws.ElapsedSeconds = dialog.ElapsedSeconds;

                // Resume counting from the edited value if it was running.
                if (wasRunning) ws.Start();

                row.Refresh();
                _config.Save();
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            bool anyRunning = false;
            foreach (Control c in _list.Controls)
            {
                var row = c as WorkspaceControl;
                if (row == null) continue;
                if (row.Workspace.IsRunning)
                {
                    anyRunning = true;
                    row.Refresh();
                }
            }

            if (anyRunning) _config.Save();
        }

        private void UpdateActivityState()
        {
        }

        private void Cleanup()
        {
            _tickTimer.Stop();
            _tickTimer.Dispose();

            foreach (Workspace ws in _config.Workspaces)
                ws.Stop();

            // Persist the final window size as well as workspace state.
            if (WindowState == FormWindowState.Normal)
            {
                _config.WindowWidth = Width;
                _config.WindowHeight = Height;
            }
            _config.Save();
            _activity.Dispose();
        }
    }
}
