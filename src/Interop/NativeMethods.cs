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
    }
}
