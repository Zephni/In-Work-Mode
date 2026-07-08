using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FakeActiveUser
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new OverlayForm());
        }
    }

    public class OverlayForm : Form
    {
        // ---- Extended window styles (make the overlay click-through) ----
        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // ---- Fake activity: harmless F15 key press (does nothing on real systems) ----
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        const byte VK_F15 = 0x7E;
        const uint KEYEVENTF_KEYUP = 0x0002;

        // ---- Keep the machine (and display) awake ----
        [DllImport("kernel32.dll")]
        static extern uint SetThreadExecutionState(uint esFlags);
        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;
        const uint ES_DISPLAY_REQUIRED = 0x00000002;

        // ---- Global low-level keyboard hook (to catch ESC anywhere) ----
        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x0100;
        const int VK_ESCAPE = 0x1B;

        delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        static LowLevelKeyboardProc _proc;
        static IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        Timer _timer;

        public OverlayForm()
        {
            // Frameless, always-on-top, invisible-to-taskbar overlay.
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(20, 20, 20);
            Opacity = 0.82;              // slightly see-through so nothing is fully hidden
            AutoSize = false;

            // Build the label text.
            var label = new Label();
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(80, 230, 120);
            label.Text = "\u25CF  ACTIVE USER MODE\r\nPress ESC to quit";
            Controls.Add(label);

            // Size + position: top-right corner of the primary screen.
            Size = new Size(240, 58);
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Size.Width - 16, wa.Top + 16);

            // Heartbeat: fire a harmless key + refresh awake state every 25 seconds.
            _timer = new Timer();
            _timer.Interval = 25000;
            _timer.Tick += (s, e) => Heartbeat();
            _timer.Start();

            // Keep awake immediately at startup too.
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);

            Load += (s, e) => MakeClickThrough();
            FormClosing += (s, e) => Cleanup();

            // Install the global ESC hook.
            _proc = HookCallback;
            _hookID = SetHook(_proc);
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
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        void MakeClickThrough()
        {
            int ex = GetWindowLong(Handle, GWL_EXSTYLE);
            SetWindowLong(Handle, GWL_EXSTYLE, ex | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }

        void Heartbeat()
        {
            // Tell Windows we are still "in use" so it never sleeps.
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);

            // Send a completely harmless key (F15). It resets the system idle
            // timer that Teams / Discord / Slack read, but does nothing else.
            keybd_event(VK_F15, 0, 0, UIntPtr.Zero);
            keybd_event(VK_F15, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var mod = System.Diagnostics.Process.GetCurrentProcess().MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(mod.ModuleName), 0);
            }
        }

        static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (int)wParam == WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_ESCAPE)
                {
                    Application.Exit();
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        void Cleanup()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            // Release the "stay awake" request so normal power settings resume.
            SetThreadExecutionState(ES_CONTINUOUS);
            if (_timer != null) _timer.Stop();
        }
    }
}
