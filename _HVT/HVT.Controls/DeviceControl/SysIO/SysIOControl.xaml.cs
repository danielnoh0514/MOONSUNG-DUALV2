using HVT.Controls.DeviceControl;
using HVT.Controls.DevicesControl;
using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HVT.Controls
{
    /// <summary>
    /// Interaction logic for SYSIOcontrol.xaml
    /// </summary>
    public partial class SysIOControl : UserControl
    {
        private SystemBoard _System_Board;

        public SystemBoard System_Board
        {
            get { return _System_Board; }
            set
            {
                if (value != null || value != _System_Board)
                    _System_Board = value;
                this.DataContext = System_Board.MachineIO;
            }
        }

        public SysIOControl()
        {
            InitializeComponent();
            AddExtension();
            this.DataContext = System_Board.MachineIO;
        }

        private void AddExtension()
        {
            _System_Board = new SystemBoard(this);
        }

        private bool _EnableGetSoundData = false;

        public bool EnableGetSoundData
        {
            get { return _EnableGetSoundData; }
            set
            {
                if (value != _EnableGetSoundData) _EnableGetSoundData = value;

                if (_EnableGetSoundData == true)
                {
                    System_Board.MachineIO.ClearSamples();
                    _System_Board.Sampling.Start();
                }
            }
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            System_Board.SendControl();
        }

        public void StartRecordMic(double Interval)
        {
            this.Dispatcher.Invoke(new System.Action(() =>
            {
                _System_Board.Sampling.Interval = TimeSpan.FromMilliseconds(Interval);
                EnableGetSoundData = true;
            }));
        }

        public void StopRecordMic()
        {
            this.Dispatcher.Invoke(new System.Action(() =>
            {
                EnableGetSoundData = false;
            }));
        }

        private void ToggleRecordMicButton_Unchecked(object sender, RoutedEventArgs e)
        {
            EnableGetSoundData = false;
        }

        private void ToggleRecordMicButton_Checked(object sender, RoutedEventArgs e)
        {
            _System_Board.Sampling.Interval = TimeSpan.FromMilliseconds(System_Board.MachineIO.SampleRate);
            EnableGetSoundData = true;
        }
    }
}