using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using WorkMode.Models;

namespace WorkMode.Configuration
{
    // INI-backed configuration. Loaded from / saved to config.ini next to the exe.
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

        public AppConfig()
        {
            Workspaces = new List<Workspace>();
        }

        private static string ConfigPath
        {
            get
            {
                return Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath), "config.ini");
            }
        }

        // Loads config.ini from the exe folder. A missing or invalid file simply
        // yields an empty workspace list so the app always starts.
        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            try
            {
                string path = ConfigPath;
                if (!File.Exists(path)) return cfg;

                string currentTitle = null;
                long currentSeconds = 0;
                bool inWorkspace = false;

                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;

                    if (line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        // New section: flush the previous workspace (if any).
                        if (inWorkspace) cfg.Add(currentTitle, currentSeconds);

                        string section = line.Substring(1, line.Length - 2).Trim();
                        inWorkspace = section.Equals("Workspace", StringComparison.OrdinalIgnoreCase);
                        currentTitle = null;
                        currentSeconds = 0;
                        continue;
                    }

                    if (!inWorkspace) continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string keyName = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();

                    if (keyName.Equals("Title", StringComparison.OrdinalIgnoreCase))
                        currentTitle = value;
                    else if (keyName.Equals("Seconds", StringComparison.OrdinalIgnoreCase))
                    {
                        long s;
                        if (long.TryParse(value, out s)) currentSeconds = s;
                    }
                }

                // Flush the final workspace.
                if (inWorkspace) cfg.Add(currentTitle, currentSeconds);
            }
            catch { }
            return cfg;
        }

        private void Add(string title, long seconds)
        {
            Workspaces.Add(new Workspace(title ?? string.Empty, seconds));
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

                foreach (Workspace ws in Workspaces)
                {
                    ws.Sync();
                    sb.AppendLine("[Workspace]");
                    sb.AppendLine("Title=" + (ws.Title ?? string.Empty));
                    sb.AppendLine("Seconds=" + ws.ElapsedSeconds);
                    sb.AppendLine();
                }

                File.WriteAllText(ConfigPath, sb.ToString());
            }
            catch { }
        }
    }
}
