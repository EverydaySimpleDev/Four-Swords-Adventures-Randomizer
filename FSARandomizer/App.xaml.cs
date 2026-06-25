using System.IO;
using System.Windows;
using FSALib;

namespace FSARandomizer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Ensure FSALib's Assets class finds "assets\actors" relative to the EXE directory,
            // regardless of the process working directory (VS debugger, shortcuts, etc.)
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            Assets.Reload();
            base.OnStartup(e);
        }
    }
}
