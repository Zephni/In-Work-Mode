using System;
using System.Windows.Forms;
using FakeActiveUser.Configuration;
using FakeActiveUser.UI;

namespace FakeActiveUser
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new OverlayForm(AppConfig.Load()));
        }
    }
}
