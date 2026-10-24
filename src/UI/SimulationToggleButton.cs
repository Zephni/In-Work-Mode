using System.Drawing;
using System.Windows.Forms;

namespace WorkMode.UI
{
    public sealed class SimulationToggleButton : Button
    {
        private readonly ToolTip _tooltip;
        private bool _active;

        public SimulationToggleButton()
        {
            Size = new Size(34, 34);
            Margin = new Padding(0, 5, 10, 5);
            AccessibleName = "Simulate activity";
            _tooltip = new ToolTip { InitialDelay = 350, ReshowDelay = 200, ShowAlways = true };
            UpdateAppearance();
        }

        public bool Active
        {
            get { return _active; }
            set
            {
                if (_active == value) return;
                _active = value;
                UpdateAppearance();
            }
        }

        private void UpdateAppearance()
        {
            Theme.StyleButton(this, _active ? Theme.Accent : Theme.Surface,
                _active ? Theme.AccentHover : Theme.SurfaceAlt, _active ? Color.White : Theme.TextMuted);
            FlatAppearance.BorderSize = _active ? 1 : 0;
            FlatAppearance.BorderColor = Theme.AccentHover;
            Image previous = Image;
            Image = Glyphs.Clock(18, ForeColor);
            if (previous != null) previous.Dispose();
            AccessibleDescription = _active
                ? "Simulate activity is on. Activate to turn off."
                : "Simulate activity is off. Activate to turn on.";
            _tooltip.SetToolTip(this, _active
                ? "Simulate activity: On - click to turn off"
                : "Simulate activity: Off - click to turn on");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tooltip.Dispose();
                if (Image != null)
                {
                    Image.Dispose();
                    Image = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
