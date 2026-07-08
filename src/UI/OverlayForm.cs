using System;
using System.Drawing;
using System.Windows.Forms;
using FakeActiveUser.Activity;
using FakeActiveUser.Configuration;
using FakeActiveUser.Input;
using FakeActiveUser.Interop;

namespace FakeActiveUser.UI
{
    // A frameless, always-on-top, click-through overlay that shows an "active
    // user" badge. It owns the activity simulator and the global quit hook.
    public sealed class OverlayForm : Form
    {
        private readonly bool _showInTaskbar;
        private readonly ActivitySimulator _activity;
        private readonly GlobalKeyboardHook _quitHook;
        private NotifyIcon _tray;

        public OverlayForm(AppConfig config)
        {
            if (config == null) config = new AppConfig();

            QuitKeyCombination quit = config.ParseQuitKeys();

            // Decide where the app presents itself.
            _showInTaskbar = config.DisplayModeValue == DisplayMode.Taskbar;

            ConfigureWindow();

            // App icon (used for the window and the tray).
            Icon appIcon = IconLoader.LoadAppIcon();
            if (appIcon != null) Icon = appIcon;

            // System-tray icon with a right-click Quit option (only in SystemTray mode).
            if (!_showInTaskbar) CreateTrayIcon(appIcon, quit.Display);

            AddBadgeLabel(quit.Display);
            PositionTopRight();

            // Keep the machine awake and simulate harmless activity.
            _activity = new ActivitySimulator();
            _activity.Start();

            // Install the global quit hook.
            _quitHook = new GlobalKeyboardHook(quit.MainKey, quit.Modifiers);
            _quitHook.Triggered += (s, e) => Application.Exit();
            _quitHook.Install();

            Load += (s, e) => MakeClickThrough();
            FormClosing += (s, e) => Cleanup();
        }

        // Configures the frameless, always-on-top overlay appearance.
        private void ConfigureWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = _showInTaskbar;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(20, 20, 20);
            Opacity = 0.82;              // slightly see-through so nothing is fully hidden
            AutoSize = false;
            Size = new Size(240, 58);
        }

        private void CreateTrayIcon(Icon appIcon, string quitKeyDisplay)
        {
            _tray = new NotifyIcon();
            _tray.Icon = appIcon ?? SystemIcons.Application;
            _tray.Text = "Active User Mode - press " + quitKeyDisplay + " to quit";
            _tray.Visible = true;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Quit", null, (s, e) => Application.Exit());
            _tray.ContextMenuStrip = menu;
        }

        private void AddBadgeLabel(string quitKeyDisplay)
        {
            var label = new Label();
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(80, 230, 120);
            label.Text = "\u25CF  ACTIVE USER MODE\r\nPress " + quitKeyDisplay + " to quit";
            Controls.Add(label);
        }

        // Places the overlay in the top-right corner of the primary screen.
        private void PositionTopRight()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Size.Width - 16, wa.Top + 16);
        }

        // Prevent the overlay from stealing focus / clicks.
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // In taskbar mode we omit WS_EX_TOOLWINDOW so a taskbar button appears.
                cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE;
                if (!_showInTaskbar) cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        private void MakeClickThrough()
        {
            int ex = NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(Handle, NativeMethods.GWL_EXSTYLE,
                ex | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT);
        }

        private void Cleanup()
        {
            _quitHook.Dispose();
            _activity.Dispose();

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
        }
    }
}
