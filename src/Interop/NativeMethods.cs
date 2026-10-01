using System;
using System.Runtime.InteropServices;

namespace WorkMode.Interop
{
    // All P/Invoke declarations and Win32 constants used by the application.
    internal static class NativeMethods
    {
        // ---- Fake activity: harmless F15 key press (does nothing on real systems) ----
        public const byte VK_F15 = 0x7E;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // ---- Keep the machine (and display) awake ----
        public const uint ES_CONTINUOUS = 0x80000000;
        public const uint ES_SYSTEM_REQUIRED = 0x00000001;
        public const uint ES_DISPLAY_REQUIRED = 0x00000002;

        [DllImport("kernel32.dll")]
        public static extern uint SetThreadExecutionState(uint esFlags);

        // ---- Dark title bar (Windows 10 1809+ / Windows 11) ----
        // DWMWA_USE_IMMERSIVE_DARK_MODE was 19 on early builds, then 20.
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attribute, ref int value, int size);

        // ---- TextBox inner padding ----
        public const int WM_SETREDRAW = 0x000B;
        public const int EM_SETMARGINS = 0x00D3;
        public const int EM_SETRECT = 0x00B3;
        public const int EC_LEFTMARGIN = 0x0001;
        public const int EC_RIGHTMARGIN = 0x0002;

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        // Sets horizontal margins (left/right) and a top inset on a TextBox handle.
        public static void SetTextBoxPadding(IntPtr hwnd, int h, int v, int width, int height)
        {
            // Left and right margins via EM_SETMARGINS.
            int margins = (h & 0xFFFF) | ((h & 0xFFFF) << 16);
            SendMessage(hwnd, EM_SETMARGINS, new IntPtr(EC_LEFTMARGIN | EC_RIGHTMARGIN), new IntPtr(margins));

            // Formatting rect insets all four edges.
            var rect = new RECT { Left = h, Top = v, Right = width - h, Bottom = height - v };
            SendMessage(hwnd, EM_SETRECT, IntPtr.Zero, ref rect);
        }

        // Asks the DWM to paint the given window's title bar in dark mode.
        // Silently ignored on OS versions that don't support it.
        public static void UseDarkTitleBar(IntPtr hwnd)
        {
            int enabled = 1;
            try
            {
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref enabled, sizeof(int));
            }
            catch { }
        }

        // ---- Dark scrollbars (Windows 10 1809+ / Windows 11) ----
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        // Switches a scrollable control's native scrollbars to the dark Explorer
        // visual style so they match the app's dark theme instead of the default
        // light-grey system scrollbar. Silently ignored if unsupported.
        public static void UseDarkScrollBar(IntPtr hwnd)
        {
            try { SetWindowTheme(hwnd, "DarkMode_Explorer", null); }
            catch { }
        }
    }
}
