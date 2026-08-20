using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using WorkMode.Models;

namespace WorkMode.Configuration
{
    // INI-backed configuration. Loaded from / saved to config.ini in the user's
    // per-user app data folder (%AppData%\WorkMode\config.ini), which is always
    // writable regardless of where the exe is installed/run from.
    // Each workspace is stored as a "[Workspace]" section with Title and Seconds
    // keys, e.g.
    //
    //   [Workspace]
    //   Title=Client project
    //   Seconds=3661
    //
    // Lines starting with "#" or ";" are treated as comments.
    public sealed class AppConfig
    {
        public List<Workspace> Workspaces { get; private set; }

        // Persisted window size. 0 means "not set" (use the default / minimum).
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }

        public AppConfig()
        {
            Workspaces = new List<Workspace>();
        }

        private static string ConfigDirectory
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "WorkMode");
            }
        }

        private static string ConfigPath
        {
            get { return Path.Combine(ConfigDirectory, "config.ini"); }
        }

        // Old location used by earlier versions: config.ini next to the exe.
        private static string LegacyConfigPath
        {
            get
            {
                return Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath), "config.ini");
            }
        }

        // One-time migration: if a config already exists next to the exe (from an
        // older version) but none exists yet in app data, copy it over so users
        // don't lose their existing workspaces/settings.
        private static void MigrateLegacyConfigIfNeeded()
        {
            try
            {
                string newPath = ConfigPath;
                if (File.Exists(newPath)) return;

                string oldPath = LegacyConfigPath;
                if (!File.Exists(oldPath)) return;

                Directory.CreateDirectory(ConfigDirectory);
                File.Copy(oldPath, newPath);
            }
            catch { }
        }

        // Loads config.ini from the user's app data folder. A missing or invalid
        // file simply yields an empty workspace list so the app always starts.
        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            try
            {
                MigrateLegacyConfigIfNeeded();

                string path = ConfigPath;
                if (!File.Exists(path)) return cfg;

                string currentTitle = null;
                long currentSeconds = 0;
                string currentNotes = null;
                bool inWorkspace = false;
                bool inWindow = false;

                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;

                    if (line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        // New section: flush the previous workspace (if any).
                        if (inWorkspace) cfg.Add(currentTitle, currentSeconds, currentNotes);

                        string section = line.Substring(1, line.Length - 2).Trim();
                        inWorkspace = section.Equals("Workspace", StringComparison.OrdinalIgnoreCase);
                        inWindow = section.Equals("Window", StringComparison.OrdinalIgnoreCase);
                        currentTitle = null;
                        currentSeconds = 0;
                        currentNotes = null;
                        continue;
                    }

                    if (!inWorkspace && !inWindow) continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string keyName = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();

                    if (inWindow)
                    {
                        int n;
                        if (keyName.Equals("Width", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out n))
                            cfg.WindowWidth = n;
                        else if (keyName.Equals("Height", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out n))
                            cfg.WindowHeight = n;
                        continue;
                    }

                    if (keyName.Equals("Title", StringComparison.OrdinalIgnoreCase))
                        currentTitle = value;
                    else if (keyName.Equals("Seconds", StringComparison.OrdinalIgnoreCase))
                    {
                        long s;
                        if (long.TryParse(value, out s)) currentSeconds = s;
                    }
                    else if (keyName.Equals("Notes", StringComparison.OrdinalIgnoreCase))
                        currentNotes = UnescapeNotes(value);
                }

                // Flush the final workspace.
                if (inWorkspace) cfg.Add(currentTitle, currentSeconds, currentNotes);
            }
            catch { }
            return cfg;
        }

        private void Add(string title, long seconds, string notes)
        {
            Workspaces.Add(new Workspace(title ?? string.Empty, seconds)
            {
                Notes = notes ?? string.Empty
            });
        }

        // Notes may span multiple lines, so newlines (and backslashes) are escaped
        // into a single INI value on save and restored on load.
        private static string EscapeNotes(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\")
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Replace("\n", "\\n");
        }

        private static string UnescapeNotes(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    if (n == 'n') { sb.Append("\r\n"); i++; continue; }
                    if (n == '\\') { sb.Append('\\'); i++; continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // Persists all workspaces to config.ini. Running workspaces are synced
        // first so their live elapsed time is written out.
        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Work Mode configuration");
                sb.AppendLine("# Lines starting with # or ; are comments.");
                sb.AppendLine("# Each [Workspace] section stores one tracked workspace.");
                sb.AppendLine();

                if (WindowWidth > 0 && WindowHeight > 0)
                {
                    sb.AppendLine("[Window]");
                    sb.AppendLine("Width=" + WindowWidth);
                    sb.AppendLine("Height=" + WindowHeight);
                    sb.AppendLine();
                }

                foreach (Workspace ws in Workspaces)
                {
                    ws.Sync();
                    sb.AppendLine("[Workspace]");
                    sb.AppendLine("Title=" + (ws.Title ?? string.Empty));
                    sb.AppendLine("Seconds=" + ws.ElapsedSeconds);
                    if (!string.IsNullOrEmpty(ws.Notes))
                        sb.AppendLine("Notes=" + EscapeNotes(ws.Notes));
                    sb.AppendLine();
                }

                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(ConfigPath, sb.ToString());
            }
            catch { }
        }
    }
}
