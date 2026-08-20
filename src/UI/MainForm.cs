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
        private readonly Label _devModeLabel;
        private readonly HashSet<Keys> _heldKeys = new HashSet<Keys>();
        private WorkspaceControl _selectedRow;

        public MainForm(AppConfig config)
        {
            _config = config ?? new AppConfig();
            _activity = new ActivitySimulator();

            Text = "Work Mode";
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

            var addPanel = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.Background };

            // Icon-only, backgroundless button that restores the window to its
            // default size. Dim until hovered so it stays out of the way.
            var resetSizeButton = new Button
            {
                Size = new Size(28, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(addPanel.Width - 38, 20),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Background,
                Cursor = Cursors.Hand,
                Image = Glyphs.RestoreWindow(18, Color.FromArgb(110, Theme.TextMuted)),
                ImageAlign = ContentAlignment.MiddleCenter,
                TabStop = false
            };
            resetSizeButton.FlatAppearance.BorderSize = 0;
            resetSizeButton.FlatAppearance.MouseOverBackColor = Theme.Background;
            resetSizeButton.FlatAppearance.MouseDownBackColor = Theme.Background;
            resetSizeButton.MouseEnter += (s, e) => resetSizeButton.Image = Glyphs.RestoreWindow(18, Theme.Text);
            resetSizeButton.MouseLeave += (s, e) => resetSizeButton.Image = Glyphs.RestoreWindow(18, Color.FromArgb(110, Theme.TextMuted));
            resetSizeButton.Click += OnResetWindowSize;
            var resetSizeTip = new ToolTip();
            resetSizeTip.SetToolTip(resetSizeButton, "Restore default window size");
            addPanel.Controls.Add(resetSizeButton);

            // Greyed-out indicator shown only while the hidden dev mode is on
            // (see OnKeyDown). Sits just left of the reset-size button.
            _devModeLabel = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = "Dev Mode",
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                ForeColor = Theme.TextMuted,
                BackColor = Color.Transparent,
                Visible = _config.DevMode
            };
            addPanel.Controls.Add(_devModeLabel);
            _devModeLabel.Location = new Point(
                resetSizeButton.Left - _devModeLabel.PreferredWidth - 8,
                resetSizeButton.Top + (resetSizeButton.Height - _devModeLabel.PreferredHeight) / 2);

            Controls.Add(_list);
            Controls.Add(addPanel);

            // Clicking empty space (the list background, panels or the form) clears
            // the current selection. Buttons and rows handle their own clicks first.
            Click += (s, e) => ClearSelection();
            _list.Click += (s, e) => ClearSelection();
            addPanel.Click += (s, e) => ClearSelection();

            foreach (Workspace ws in _config.Workspaces)
                AddRow(ws);

            // Placeholder "card", always the last item, that opens the add dialog.
            _addRow = new AddWorkspaceRow();
            _addRow.AddRequested += OnAddWorkspace;
            _list.Controls.Add(_addRow);

            // Fit each row to the list width.
            _list.Resize += (s, e) => ResizeRows();
            ResizeRows();

            _tickTimer = new Timer { Interval = 1000 };
            _tickTimer.Tick += OnTick;
            _tickTimer.Start();

            ResizeEnd += (s, e) => SaveWindowSize();
            FormClosing += (s, e) => Cleanup();

            // Secret combo: hold Z, E, P and tap H to toggle dev mode (reveals
            // the per-workspace edit button). Only tracked while focused.
            KeyPreview = true;
            KeyDown += OnMainFormKeyDown;
            KeyUp += OnMainFormKeyUp;
            Deactivate += (s, e) => _heldKeys.Clear();

            _activity.Start();
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

        // Restores the window to its default size (see DefaultWindowWidth/Height).
        private void OnResetWindowSize(object sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Normal)
                WindowState = FormWindowState.Normal;

            Size = new Size(DefaultWindowWidth, DefaultWindowHeight);
            SaveWindowSize();
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

        // Flips dev mode, persists it, and shows/hides the edit button on every row.
        private void ToggleDevMode()
        {
            _config.DevMode = !_config.DevMode;
            _config.Save();

            _devModeLabel.Visible = _config.DevMode;
            foreach (Control c in _list.Controls)
            {
                var row = c as WorkspaceControl;
                if (row != null) row.EditButtonVisible = _config.DevMode;
            }
        }

        private void AddRow(Workspace ws)
        {
            var row = new WorkspaceControl(ws);
            row.ToggleRequested += OnToggle;
            row.ResetRequested += OnReset;
            row.SelectRequested += OnRowSelected;
            row.NotesChanged += OnNotesChanged;
            row.EditRequested += OnEditWorkspace;
            row.DeleteRequested += OnDeleteWorkspace;
            row.EditButtonVisible = _config.DevMode;
            _list.Controls.Add(row);
            SizeRow(row);

            // Keep the "add new" placeholder pinned to the bottom of the list.
            if (_addRow != null)
                _list.Controls.SetChildIndex(_addRow, _list.Controls.Count - 1);
        }

        // Selects the clicked row, highlighting it. Clicking an already-selected
        // row deselects it instead.
        private void OnRowSelected(object sender, EventArgs e)
        {
            var row = sender as WorkspaceControl;
            if (row == null) return;

            if (_selectedRow == row)
            {
                ClearSelection();
                return;
            }

            if (_selectedRow != null)
                _selectedRow.Selected = false;

            _selectedRow = row;
            _selectedRow.Selected = true;
        }

        // Persists notes as the user types into a workspace's notes area.
        private void OnNotesChanged(object sender, EventArgs e)
        {
            _config.Save();
        }

        // Clears any current selection.
        private void ClearSelection()
        {
            if (_selectedRow != null)
            {
                _selectedRow.Selected = false;
                _selectedRow = null;
            }
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

            ClearSelection();
            UpdateActivityState();
            _config.Save();
        }

        private void ResizeRows()
        {
            foreach (Control row in _list.Controls)
                SizeRow(row);
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
