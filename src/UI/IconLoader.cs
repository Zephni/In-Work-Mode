using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WorkMode.UI
{
    // Loads the application icon used for the window and the tray.
    internal static class IconLoader
    {
        // Loads the icon embedded in this exe (falls back to a sibling app.ico).
        // Returns null when no icon can be found.
        public static Icon LoadAppIcon()
        {
            try
            {
                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) return ico;
            }
            catch { }

            try
            {
                string path = Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath), "app.ico");
                if (File.Exists(path)) return new Icon(path);
            }
            catch { }

            return null;
        }
    }
}
