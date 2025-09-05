using HVT.Utility;
using HVT.VTM.Program;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;
using PropertyChanged;
using System.Net.Sockets;
using System.Threading;
using System.Text.Json;

using VTM.Properties;
using HVT.Controls;

namespace VTM
{
    public class IntStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Int to string
            return value?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // String to int
            if (string.IsNullOrWhiteSpace(value?.ToString()))
                return 0; // or return null for nullable int

            if (int.TryParse(value.ToString(), NumberStyles.Any, culture, out int result))
                return result;

            return 0; // or throw new FormatException("Invalid integer");
        }
    }
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }



    /// <summary>
    /// Interaction logic for SettingPage.xaml
    /// </summary>

    [AddINotifyPropertyChangedInterface]

    public partial class SettingPage : Page
    {

        public AppSetting Setting { get; set; } = new AppSetting();



        public Program Program { get; set; }

        private void OnProgramChanged()
        {
            Setting = Program.AppSetting.Clone();

            PrinterHolder.Child = Program.Printer;
        }

        public SettingPage()
        {
            InitializeComponent();
            this.DataContext = this;

            cbbBarcodePort.ItemsSource = Communication.ComPorts;
            cbbDMM1Port.ItemsSource = Communication.ComPorts;
            cbbDMM2Port.ItemsSource = Communication.ComPorts;
            cbbLevelPort.ItemsSource = Communication.ComPorts;
            cbbMux1Port.ItemsSource = Communication.ComPorts;
            cbbMux2Port.ItemsSource = Communication.ComPorts;
            cbbPrinterPort.ItemsSource = Communication.ComPorts;
            cbbRelayPort.ItemsSource = Communication.ComPorts;
            cbbSolenoidPort.ItemsSource = Communication.ComPorts;
            cbbSysPort.ItemsSource = Communication.ComPorts;
            cbbUUT1Port.ItemsSource = Communication.ComPorts;
            cbbUUT2Port.ItemsSource = Communication.ComPorts;
            cbbUUT3Port.ItemsSource = Communication.ComPorts;
            cbbUUT4Port.ItemsSource = Communication.ComPorts;
            cbbPowerMetterPort.ItemsSource = Communication.ComPorts;
            cbbCounterTimerPort.ItemsSource = Communication.ComPorts;

            cbbExtensionBoardPort1.ItemsSource = Communication.ComPorts;
            cbbExtensionBoardPort2.ItemsSource = Communication.ComPorts;

            cbbBarcodeParity.ItemsSource = Enum.GetNames(typeof(Parity)).ToList();
            cbbBarcodeBaud.ItemsSource = new List<int> { 9600, 19200, 38400, 57600, 115200 };
            cbbBarcodeDatabit.ItemsSource = new List<int> { 7, 8 };
        }



        public bool ApplyDataSetting()
        {
            List<ComPort> selectedPorts = new List<ComPort>
            {
                Setting.Communication.ScannerPort,
                Setting.Communication.DMM1Port,
                Setting.Communication.DMM2Port,
                Setting.Communication.LevelPort,
                Setting.Communication.Mux1Port,
                Setting.Communication.Mux2Port,
                Setting.Communication.PrinterPort,
                Setting.Communication.RelayPort,
                Setting.Communication.SolenoidPort,
                Setting.Communication.SystemIOPort,
                Setting.Communication.UUT1Port,
                Setting.Communication.UUT2Port,
                Setting.Communication.UUT3Port,
                Setting.Communication.UUT4Port,
                Setting.Communication.PowerMetterPort,
                Setting.Communication.CounterTimerPort,
                Setting.Communication.BoardExtensionPort1,
                Setting.Communication.BoardExtensionPort2,

            };

            bool portHasDuplicate = CheckForDuplicatePorts(selectedPorts);
            if (portHasDuplicate)
            {
                System.Windows.MessageBox.Show("Duplicate COM Port!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            Program.AppSetting = Setting.Clone();
            return true;
        }

        public bool CheckForDuplicatePorts(List<ComPort> selectedPorts)
        {
            var portSet = new HashSet<string>();

            foreach (var port in selectedPorts)
            {
                if (port.Use)
                {
                    if (portSet.Contains(port.PortName))
                    {
                        return true; // Duplicate found
                    }
                    portSet.Add(port.PortName);
                }
            }

            return false; // No duplicates found
        }

        private void btApply_Click(object sender, RoutedEventArgs e)
        {
            ApplyDataSetting();
        }

        private void btCancle_Click(object sender, RoutedEventArgs e)
        {
            Setting = Program.AppSetting.Clone();
        }

        private void btSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            bool settingStatus = ApplyDataSetting();

            if (settingStatus)
            {
                Setting.SaveToFile("Config.cfg");
            }
        }


        private void pwbCurrentAdminPass_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (pwbCurrentAdminPass.Password == Setting.SystemAccess.AdminPass)
            {
                pwbCurrentAdminPass.BorderBrush = new SolidColorBrush(Colors.Green);
            }
            else
            {
                pwbCurrentAdminPass.BorderBrush = new SolidColorBrush(Colors.Red);
            }
        }

        private void pwbNewAdminPass_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (pwbNewAdminPass.Password == pwbRenewAdminPass.Password)
            {
                pwbRenewAdminPass.BorderBrush = new SolidColorBrush(Colors.Green);
                pwbNewAdminPass.BorderBrush = new SolidColorBrush(Colors.Green);
            }
            else
            {
                pwbRenewAdminPass.BorderBrush = new SolidColorBrush(Colors.Red);
                pwbNewAdminPass.BorderBrush = new SolidColorBrush(Colors.Red);
            }
        }

        private void pwbRenewAdminPass_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (pwbNewAdminPass.Password == pwbRenewAdminPass.Password)
            {
                pwbRenewAdminPass.BorderBrush = new SolidColorBrush(Colors.Green);
                pwbNewAdminPass.BorderBrush = new SolidColorBrush(Colors.Green);
                if (pwbCurrentAdminPass.Password == Setting.SystemAccess.AdminPass)
                {
                    Setting.SystemAccess.AdminPass = pwbRenewAdminPass.Password;
                }
            }
            else
            {
                pwbRenewAdminPass.BorderBrush = new SolidColorBrush(Colors.Red);
                pwbNewAdminPass.BorderBrush = new SolidColorBrush(Colors.Red);
            }
        }

        private void pwbCurrentOPPass_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (pwbCurrentOPPass.Password == Setting.SystemAccess.OperationPass)
            {
                pwbCurrentOPPass.BorderBrush = new SolidColorBrush(Colors.Green);
            }
            else
            {
                pwbCurrentOPPass.BorderBrush = new SolidColorBrush(Colors.Red);
            }
        }

        private void pwbNewOPPass_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (pwbCurrentOPPass.Password == Setting.SystemAccess.OperationPass)
            {
                Setting.SystemAccess.OperationPass = pwbNewOPPass.Password;
            }
        }


        private void btnChangeExcelDir_Click(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                // Optional: Set a description or starting folder
                folderDialog.Description = "Select Excel Files Directory";

                // Show the dialog and check if the user clicked OK
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    // Get the selected folder path
                    string selectedFolderPath = folderDialog.SelectedPath;

                    Setting.Operations.LogDirectory = selectedFolderPath;

                }
            }
        }

        private void btnChangeNGExcelDir_Click(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                // Optional: Set a description or starting folder
                folderDialog.Description = "Select Excel Files Directory";

                // Show the dialog and check if the user clicked OK
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    // Get the selected folder path
                    string selectedFolderPath = folderDialog.SelectedPath;

                    // Set the selected folder path to the lblExcelFileDir label's content

                    Setting.Operations.NgLogDirectory = selectedFolderPath;
                }
            }
        }


       




    }
}