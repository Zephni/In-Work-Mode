using System;
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
        private readonly AppConfig _config;
        private readonly ActivitySimulator _activity;
        private readonly FlowLayoutPanel _list;
        private readonly Timer _tickTimer;

        public MainForm(AppConfig config)
        {
            _config = config ?? new AppConfig();
            _activity = new ActivitySimulator();

            Text = "Work Mode";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(420, 480);
            MinimumSize = new Size(360, 300);

            Icon appIcon = IconLoader.LoadAppIcon();
            if (appIcon != null) Icon = appIcon;

            // Scrolling list of workspaces (fills the window above the add button).
            _list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10)
            };

            var addPanel = new Panel { Dock = DockStyle.Bottom, Height = 56 };
            var addButton = new Button
            {
                Text = "Add Workspace",
                Size = new Size(140, 34),
                Location = new Point(10, 11)
            };
            addButton.Click += OnAddWorkspace;
            addPanel.Controls.Add(addButton);

            Controls.Add(_list);
            Controls.Add(addPanel);

            foreach (Workspace ws in _config.Workspaces)
                AddRow(ws);

            // Fit each row to the list width.
            _list.Resize += (s, e) => ResizeRows();
            ResizeRows();

            _tickTimer = new Timer { Interval = 1000 };
            _tickTimer.Tick += OnTick;
            _tickTimer.Start();

            FormClosing += (s, e) => Cleanup();
        }

        private void AddRow(Workspace ws)
        {
            var row = new WorkspaceControl(ws);
            row.ToggleRequested += OnToggle;
            row.EditRequested += OnEdit;
            _list.Controls.Add(row);
            SizeRow(row);
        }

        private void ResizeRows()
        {
            foreach (Control c in _list.Controls)
            {
                var row = c as WorkspaceControl;
                if (row != null) SizeRow(row);
            }
        }

        private void SizeRow(WorkspaceControl row)
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

        private void OnEdit(object sender, EventArgs e)
        {
            var row = (WorkspaceControl)sender;
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

        // Keeps the activity simulator running only while a timer is active.
        private void UpdateActivityState()
        {
            bool anyRunning = false;
            foreach (Workspace ws in _config.Workspaces)
                if (ws.IsRunning) { anyRunning = true; break; }

            if (anyRunning) _activity.Start();
            else _activity.Stop();
        }

        private void Cleanup()
        {
            _tickTimer.Stop();
            _tickTimer.Dispose();

            foreach (Workspace ws in _config.Workspaces)
                ws.Stop();

            _config.Save();
            _activity.Dispose();
        }
    }
}
