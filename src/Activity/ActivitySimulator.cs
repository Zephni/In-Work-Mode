using System;
using System.Windows.Forms;
using FakeActiveUser.Interop;

namespace FakeActiveUser.Activity
{
    // Keeps the machine (and display) awake and periodically simulates harmless
    // activity so idle-detection in Teams / Slack / Discord keeps reporting the
    // user as active.
    public sealed class ActivitySimulator : IDisposable
    {
        private const int IntervalMs = 25000;

        private readonly Timer _timer;

        public ActivitySimulator()
        {
            _timer = new Timer { Interval = IntervalMs };
            _timer.Tick += (s, e) => Pulse();
        }

        public void Start()
        {
            // Keep awake immediately, then on every heartbeat.
            Pulse();
            _timer.Start();
        }

        // One heartbeat: refresh the "stay awake" request and press a harmless key.
        private static void Pulse()
        {
            // Tell Windows we are still "in use" so it never sleeps.
            NativeMethods.SetThreadExecutionState(
                NativeMethods.ES_CONTINUOUS |
                NativeMethods.ES_SYSTEM_REQUIRED |
                NativeMethods.ES_DISPLAY_REQUIRED);

            // Send a completely harmless key (F15). It resets the system idle
            // timer that Teams / Discord / Slack read, but does nothing else.
            NativeMethods.keybd_event(NativeMethods.VK_F15, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeMethods.VK_F15, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            // Release the "stay awake" request so normal power settings resume.
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
        }
    }
}
