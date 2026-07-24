using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using VTM_Report;

namespace VTM
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private SplashScreen splashScreen = new SplashScreen();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) => LogCrash("AppDomain", args.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, args) => LogCrash("Task", args.Exception);

            // UI-freeze watchdog: reports the last checkpoint to CrashLogs when the UI thread hangs
            HVT.Utility.HangDiag.Start(this.Dispatcher, Path.Combine(AppContext.BaseDirectory, "CrashLogs"));

            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            //initialize the splash screen and set it as the application main window
            this.MainWindow = splashScreen;
            splashScreen.Show();

            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            String macAddr = string.Empty;

            string[] whiteListMacAddress = { 
                "A8A15940E606", 
                "D8BBC1B21D13",
                "089798BE8F26", 
                "00D861E3E550",  
                "60CF848339C8", 
                "60CF848338A6", 
                "D843AE12EB2A", 
                "D8BBC1B21D13", 
                "D843AE12EC0A" , 
                "60CF848339D5", 
                "60CF848339D4" ,
                "D4F32D1F99D9",
                "D6F3D1F99D8",
                "60CF848336BE", 
                "60CF84833ACA", 
                "60CF848336C0" ,
                "60CF848338F0"};

            bool exists = false;
            foreach (NetworkInterface adapter in nics)
            {

                macAddr = adapter.GetPhysicalAddress().ToString();

                exists = whiteListMacAddress.Contains(macAddr);

                if (exists) break;
            }

            if (!exists)
            {
                MessageBox.Show("This Program Cannot be Copied!!!", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                App.Current.Shutdown();
            }

            //in order to ensure the UI stays responsive, we need to
            //do the work on a different thread

            var mainWindow = new MainWindow();
            mainWindow.Loaded += MainWindow_Loaded;
            this.MainWindow = mainWindow;
            mainWindow.Show();
            mainWindow.WarningLogDirNotExist();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            splashScreen.Close();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash("UI", e.Exception);
            MessageBox.Show("Unhandled exception occurred: \n" + e.Exception.Message + "\n\nStacktrace:\n" + e.Exception.StackTrace, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void LogCrash(string source, Exception ex)
        {
            try
            {
                string crashLogDir = Path.Combine(AppContext.BaseDirectory, "CrashLogs");
                Directory.CreateDirectory(crashLogDir);
                string logFile = Path.Combine(crashLogDir, "CrashLog_" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
                string content = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] [" + source + "] "
                    + (ex != null ? ex.ToString() : "Unknown exception") + Environment.NewLine + Environment.NewLine;
                File.AppendAllText(logFile, content);
            }
            catch
            {
                // never let crash logging itself crash the app
            }
        }
    }
}