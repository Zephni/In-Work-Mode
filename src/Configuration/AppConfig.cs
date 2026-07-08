using System;
using System.IO;
using System.Windows.Forms;

namespace FakeActiveUser.Configuration
{
    // INI-backed configuration. Loaded from config.ini next to the exe.
    // The INI format supports "#" (or ";") comments so each option can be
    // documented inline.
    public sealed class AppConfig
    {
        // "Taskbar" or "SystemTray". Defaults to Taskbar.
        public string DisplayMode { get; set; }

        // Key (or key combination) that quits the app. Supports "+" combinations
        // using Ctrl/Shift/Alt modifiers, e.g. "Ctrl+Shift+Q". Spaces are ignored.
        // Defaults to "Escape".
        public string QuitKeys { get; set; }

        public AppConfig()
        {
            DisplayMode = "Taskbar";
            QuitKeys = "Escape";
        }

        public DisplayMode DisplayModeValue
        {
            get
            {
                DisplayMode mode;
                if (Enum.TryParse(DisplayMode, true, out mode)) return mode;
                return Configuration.DisplayMode.Taskbar;
            }
        }

        // Parses QuitKeys into a QuitKeyCombination (main key, modifiers, display).
        public QuitKeyCombination ParseQuitKeys()
        {
            return QuitKeyCombination.Parse(QuitKeys);
        }

        // Loads config.ini from the exe folder. Missing/invalid config falls
        // back to defaults so the app always starts.
        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            try
            {
                string path = Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath), "config.ini");
                if (File.Exists(path))
                {
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string line = raw.Trim();
                        // Skip blanks and comment lines (# or ;).
                        if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;

                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;

                        string keyName = line.Substring(0, eq).Trim();
                        string value = line.Substring(eq + 1).Trim();

                        if (keyName.Equals("DisplayMode", StringComparison.OrdinalIgnoreCase))
                            cfg.DisplayMode = value;
                        else if (keyName.Equals("QuitKeys", StringComparison.OrdinalIgnoreCase) ||
                                 keyName.Equals("QuitKey", StringComparison.OrdinalIgnoreCase))
                            cfg.QuitKeys = value;
                    }
                }
            }
            catch { }
            return cfg;
        }
    }
}
