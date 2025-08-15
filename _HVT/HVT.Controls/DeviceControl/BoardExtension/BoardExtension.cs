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


        static byte[] HexStringToByteArray(string hex)
        {
            string[] hexValuesSplit = hex.Split(' ');
            byte[] bytes = new byte[hexValuesSplit.Length];

            for (int i = 0; i < hexValuesSplit.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexValuesSplit[i], 16);
            }
            return bytes;
        }
        public double ConvertToDouble(byte[] value, int index)
        {
            double f = 0;
            try
            {
                byte[] hexData = { value[index], value[index + 1], value[index + 2], value[index + 3] };

                
                Console.WriteLine($"hexData: {hexData[0].ToString("X2")} {hexData[1].ToString("X2")} {hexData[2].ToString("X2")} {hexData[3].ToString("X2")}");
                double result = BitConverter.ToUInt32(hexData, 0) ;
                Console.WriteLine($"result: {result}");


                return result;
            }
            catch (Exception e)
            {
            }
            return f;
        }        

        public bool SendKey(List<int> keyList, bool level)
        {
            byte byteControl = (byte)0;

            for (int i = 0; i < keyList.Count; i++)
            {
                int bitInByteIndex = keyList[i];
                byte mask = (byte)(1 << bitInByteIndex);
                if (level)
                {
                    byteControl = (byte)(byteControl | mask);
                }
                else
                {
                    byteControl = (byte)(byteControl & ~mask);
                }
            }
            return SerialPort.SendToControls(new byte[] { byteControl }, 500, new byte[] { 0x52, 0x00 });
        }

        public void GetSampleMic()
        {
            byte[] Response;

            if (SerialPort.Port.IsOpen)
            {
                if (SerialPort.SendAndRead(new byte[] { 0x49 }, 0x49, 1500, out Response))
                {
                    if (Response.Length == 10)
                    {
                        SysIOcontrol.System_Board.MachineIO.MIC_A = (Response[5] << 8) | Response[4];
                        SysIOcontrol.System_Board.MachineIO.MIC_B = (Response[7] << 8) | Response[6];

                        SysIOcontrol.System_Board.MachineIO.SamplesMicA.Add(SysIOcontrol.System_Board.MachineIO.MIC_A);
                        SysIOcontrol.System_Board.MachineIO.SamplesMicB.Add(SysIOcontrol.System_Board.MachineIO.MIC_B);
                    }
                }
            }
        }
    }
}