using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FakeActiveUser.Interop;

namespace FakeActiveUser.Input
{
    // Installs a global low-level keyboard hook and raises Triggered when a
    // specific key combination is pressed anywhere in the system.
    public sealed class GlobalKeyboardHook : IDisposable
    {
        private readonly int _targetVkCode;
        private readonly Keys _targetModifiers;

        // Keep a reference to the delegate so it is not garbage collected while
        // the hook is installed.
        private readonly NativeMethods.LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        // Raised (on the hooking thread) when the target combination is pressed.
        public event EventHandler Triggered;

        public GlobalKeyboardHook(Keys mainKey, Keys modifiers)
        {
            _targetVkCode = (int)mainKey;
            _targetModifiers = modifiers;
            _proc = HookCallback;
        }

        public void Install()
        {
            using (Process process = Process.GetCurrentProcess())
            using (ProcessModule module = process.MainModule)
            {
                _hookId = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WH_KEYBOARD_LL,
                    _proc,
                    NativeMethods.GetModuleHandle(module.ModuleName),
                    0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (int)wParam == NativeMethods.WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == _targetVkCode && Control.ModifierKeys == _targetModifiers)
                {
                    EventHandler handler = Triggered;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            }
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }
}
