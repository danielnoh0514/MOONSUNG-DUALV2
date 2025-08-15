using HVT.Controls.DevicesControl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;

using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace HVT.Controls
{
    public class CounterTimer : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private SerialPortDisplay serialPort = new SerialPortDisplay();
        public SerialPortDisplay SerialPort
        {
            get { return serialPort; }
            set
            {
                serialPort = value;
            }
        }
        public string Name = "CounterTimer";

        private byte[] TXFrame_ResetCounter = new byte[8] { 0x01, 0x05, 0x00, 0x00, 0xFF, 0xFF, 0xCC, 0x7A };
        private byte[] TXFrame_CounterRead = new byte[8] { 0x01 , 0x04, 0x03, 0xEB, 0x00, 0x01, 0x41, 0xBA };


        public CounterTimer()
        {
            SerialPort.DeviceName = this.Name;
            SerialPort.BlinkTime = 50;
            SerialPort.Port = new SerialPort()
            {
                PortName = "COM20",
                BaudRate = 9600,
                ReadTimeout = 500

            };
        }

        private Int16 position = 0;
        public Int16 Position
        {
            get { return position; }
            set
            {
                position = value;
            }
        }


        public async void CheckCommunication(string COM_NAME)
        {
            try
            {
                SerialPort.DeviceName = this.Name;
                SerialPort.BlinkTime = 50;
                SerialPort.Port = new SerialPort()
                {
                    PortName = COM_NAME,
                    BaudRate = 9600,
                    ReadTimeout = 1000,
                    DtrEnable = true
                };
                SerialPort.Port.Open();
                SerialPort.OpenPort();
            }
            catch (Exception e)
            {
                HVT.Utility.Debug.Write(Name + ": " + e.Message, HVT.Utility.Debug.ContentType.Error);
            }
        }

        public bool Read()
        {

            if (SerialPort.SendAndRead(TXFrame_CounterRead, 1000, out List<byte> Response))
            {
                if (Response.Count < 7) return false;
                else
                {
                    Position = BitConverter.ToInt16(new byte[2] { Response[4], Response[3] }, 0);
                    return true;
                }
            }

            return false;

        }

        public bool Reset()
        {

            if (SerialPort.SendAndRead(TXFrame_ResetCounter, 1000, out List<byte> Response))
            {
                if (Response.Count < 5) return false;
                else
                {
                    Position = 0;
                    return true;
                }
            }

            return false;


        }




    }
}
