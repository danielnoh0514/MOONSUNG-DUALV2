using HVT.Controls.DeviceControl;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Windows.Threading;

namespace HVT.Controls.DevicesControl
{
    public class BoardExtension
    {
        public string Name = "BoardExtension";

        public SevenSegment SevenSegment { get; set; }
        private SerialPortDisplay serialPort { get; set; }
        public SerialPortDisplay SerialPort
        {
            get { return serialPort; }
            set
            {
                serialPort = value;
            }
        }

        public byte byteControl = 0;

        private SysIOControl SysIOcontrol { get; set; }    

        public BoardExtension(string PortName)
        {
            SevenSegment = new SevenSegment(this);
            serialPort = new SerialPortDisplay();

            SerialPort.DeviceName = this.Name;
            SerialPort.BlinkTime = 50;
            SerialPort.Port = new SerialPort()
            {
                PortName = PortName,
                BaudRate = 115200,
                ReadTimeout = 500,
            };
        }
        public async void CheckCommunication(string pComName)
        {
            try
            {
                SerialPort.DeviceName = this.Name;
                SerialPort.BlinkTime = 50;
                SerialPort.Port = new SerialPort()
                {
                    PortName = pComName,
                    BaudRate = 115200,
                    ReadTimeout = 500,
                };
                SerialPort.Port.Open();
                SerialPort.OpenPort();
            }
            catch (Exception e)
            {
                HVT.Utility.Debug.Write(Name + ": " + e.Message, HVT.Utility.Debug.ContentType.Error);
            }
        }





    }
}