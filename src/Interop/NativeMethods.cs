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
    }
}
