using System;
using System.Reflection;
using System.Windows.Forms;
using WorkMode.Configuration;
using WorkMode.UI;

[assembly: AssemblyTitle("In Work Mode")]
[assembly: AssemblyProduct("In Work Mode")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

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
