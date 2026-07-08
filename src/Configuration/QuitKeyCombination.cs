using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FakeActiveUser.Configuration
{
    // A parsed "quit" key combination: a main key plus optional modifiers,
    // together with a friendly display string (e.g. "Ctrl+Shift+Q").
    public sealed class QuitKeyCombination
    {
        public Keys MainKey { get; private set; }
        public Keys Modifiers { get; private set; }
        public string Display { get; private set; }

        private QuitKeyCombination(Keys mainKey, Keys modifiers, string display)
        {
            MainKey = mainKey;
            Modifiers = modifiers;
            Display = display;
        }

        // Parses text like "Ctrl+Shift+Q". Spaces are ignored and tokens are
        // separated by "+". Recognised modifiers: Ctrl (or Control), Shift, Alt.
        // The main key may be any name from System.Windows.Forms.Keys.
        // Falls back to Escape when nothing usable is found.
        public static QuitKeyCombination Parse(string text)
        {
            Keys mainKey = Keys.None;
            Keys modifiers = Keys.None;

            string source = string.IsNullOrEmpty(text) ? "Escape" : text;
            foreach (string token in source.Split('+'))
            {
                string t = token.Trim();
                if (t.Length == 0) continue;

                if (t.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("Control", StringComparison.OrdinalIgnoreCase))
                    modifiers |= Keys.Control;
                else if (t.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    modifiers |= Keys.Shift;
                else if (t.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    modifiers |= Keys.Alt;
                else
                {
                    Keys k;
                    if (Enum.TryParse(t, true, out k)) mainKey = k;
                }
            }

            // If nothing usable was parsed, fall back to Escape.
            if (mainKey == Keys.None && modifiers == Keys.None) mainKey = Keys.Escape;

            // Build a friendly display string: modifiers first, then the key.
            var parts = new List<string>();
            if ((modifiers & Keys.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & Keys.Shift) != 0) parts.Add("Shift");
            if ((modifiers & Keys.Alt) != 0) parts.Add("Alt");
            if (mainKey != Keys.None) parts.Add(mainKey.ToString());

            return new QuitKeyCombination(mainKey, modifiers, string.Join("+", parts.ToArray()));
        }
    }
}
