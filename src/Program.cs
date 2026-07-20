using System;
using System.Windows.Forms;
using WorkMode.Configuration;
using WorkMode.UI;

namespace WorkMode
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(AppConfig.Load()));
        }
    }
}
