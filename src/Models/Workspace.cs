using System;
using System.Diagnostics;

namespace WorkMode.Models
{
    // A single tracked workspace: a title plus the total time counted against it.
    // While running, elapsed time is measured with a Stopwatch and folded back
    // into ElapsedSeconds so the persisted value always reflects the live total.
    public sealed class Workspace
    {
        public string Title { get; set; }

        // Total counted time, in whole seconds. Kept current while running.
        public long ElapsedSeconds { get; set; }

        // Free-form notes the user can jot against this workspace.
        public string Notes { get; set; }

        public bool IsRunning { get; private set; }

        private Stopwatch _stopwatch;
        private long _baseSeconds;

        public Workspace(string title, long elapsedSeconds)
        {
            Title = title ?? string.Empty;
            ElapsedSeconds = elapsedSeconds < 0 ? 0 : elapsedSeconds;
            Notes = string.Empty;
        }

        public void Start()
        {
            if (IsRunning) return;
            _baseSeconds = ElapsedSeconds;
            _stopwatch = Stopwatch.StartNew();
            IsRunning = true;
        }

        public void Stop()
        {
            if (!IsRunning) return;
            Sync();
            _stopwatch = null;
            IsRunning = false;
        }

        // Clears the counted time back to zero. If the timer is currently running
        // it keeps running, but counts up again from zero.
        public void Reset()
        {
            ElapsedSeconds = 0;
            _baseSeconds = 0;
            if (IsRunning) _stopwatch = Stopwatch.StartNew();
        }

        // Refreshes ElapsedSeconds from the running stopwatch. No-op when stopped.
        public void Sync()
        {
            if (IsRunning && _stopwatch != null)
                ElapsedSeconds = _baseSeconds + (long)_stopwatch.Elapsed.TotalSeconds;
        }

        // Formats a second count as HH:MM:SS (hours are not zero-padded to two
        // digits once they exceed 99, so long sessions still read correctly).
        public static string Format(long totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            long hours = totalSeconds / 3600;
            long minutes = (totalSeconds % 3600) / 60;
            long seconds = totalSeconds % 60;
            return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
        }

        // Parses an HH:MM:SS (or MM:SS, or SS) string into a second count.
        // Returns false when the text cannot be understood.
        public static bool TryParseTime(string text, out long totalSeconds)
        {
            totalSeconds = 0;
            if (string.IsNullOrEmpty(text)) return false;

            string[] parts = text.Trim().Split(':');
            if (parts.Length == 0 || parts.Length > 3) return false;

            long[] values = new long[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                long v;
                if (!long.TryParse(parts[i].Trim(), out v) || v < 0) return false;
                values[i] = v;
            }

            if (parts.Length == 3)
                totalSeconds = values[0] * 3600 + values[1] * 60 + values[2];
            else if (parts.Length == 2)
                totalSeconds = values[0] * 60 + values[1];
            else
                totalSeconds = values[0];

            return true;
        }
    }
}
