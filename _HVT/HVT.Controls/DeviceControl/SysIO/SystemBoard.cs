using HVT.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Windows.Threading;
using Debug = HVT.Utility.Debug;

namespace HVT.Controls
{
    public class SystemBoard
    {
        public SystemMachineIO MachineIO = new SystemMachineIO();
        private SysIOControl SysIOcontrol { get; set; }

        private SerialPortDisplay _SerialPort = new SerialPortDisplay();

        public SerialPortDisplay SerialPort
        {
            get { return _SerialPort; }
            set
            {
                if (value != _SerialPort) _SerialPort = value;
            }
        }

        public DispatcherTimer Sampling = new DispatcherTimer()
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        
        public void GetSampleMic()
        {
            byte[] Response;

            if (SerialPort.Port.IsOpen)
            {
                if (SerialPort.SendAndRead(new byte[] { 0x50 }, 0x50, 1500, out Response))
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
        
        private void Sampling_Tick(object sender, EventArgs e)
        {
            if (SysIOcontrol.EnableGetSoundData)
            {
                if (SysIOcontrol.System_Board.MachineIO.SamplesMicA.Count < 1000)
                {
                    GetSampleMic();
                }
                else
                {
                    SysIOcontrol.EnableGetSoundData = false;
                }
            }
        }

        public SystemBoard(SysIOControl pSysIOControl = null)
        {
            if (pSysIOControl != null)
            {
                SysIOcontrol = pSysIOControl;
            }
            SerialPort.DeviceName = "SYSTEM";
            SerialPort.Port = new System.IO.Ports.SerialPort
            {
                PortName = "COM10",
                BaudRate = 9600,
                DataBits = 8,
                Parity = System.IO.Ports.Parity.None,
                StopBits = System.IO.Ports.StopBits.One,
                ReceivedBytesThreshold = 1,
            };
            Sampling.Tick += Sampling_Tick;
        }

        public async void CheckCardComunication(string COMNAME)
        {
            byte[] inputAsk = new byte[2];
            SerialPort.SerialDataReciver -= SerialPort_SerialDataReciver;
            inputAsk[0] = (byte)0x49;
            byte[] cardResponse = SystemComunication.GetFrame(new byte[] { 0x49 });
            bool result = await SerialPort.CheckBoardComPort(COMNAME, 9600, inputAsk, cardResponse, 1000, true);
            SerialPort.SerialDataReciver += SerialPort_SerialDataReciver;
            GetInput();
        }

        private void SerialPort_SerialDataReciver(object sender, EventArgs e)
        {
            if (SerialPort.Port.IsOpen)
            {
                List<byte> frame = new List<byte>();
                Task.Delay(50).Wait();
                int size = SerialPort.Port.BytesToRead;
                byte[] bytes = new byte[size];
                try
                {
                    SerialPort.Port.Read(bytes, 0, SerialPort.Port.BytesToRead);
                }
                catch (Exception)
                {
                    return;
                }

                if (bytes.Length >= 3)
                {
                    if (bytes[0] == 0xAA)
                    {
                        if (bytes[1] == 0x03 && bytes[2] == 0xFF)
                        {
                            MachineIO.SubStart = true;
                        }

                        if (bytes[1] == 0x01 && bytes[2] == 0xFF)
                        {
                            MachineIO.SubOk = true;
                        }

                        if (bytes[1] == 0x02 && bytes[2] == 0xFF)
                        {
                            MachineIO.SubNg = true;
                        }
                    }
                }

                if (bytes.Length < 7) return;
                for (int i = 0; i < bytes.Length; i++)
                {
                    byte startByte = bytes[i];
                    if (startByte == SystemComunication.Prefix1)
                    {
                        var secondByte = bytes[i + 1];
                        if (secondByte == SystemComunication.Prefix2)
                        {
                            frame.Clear();
                            frame.Add(startByte);
                            frame.Add(secondByte);
                            frame.Add(bytes[i + 2]);

                            if ((int)bytes[i + 2] + 3 >= bytes.Length) return;

                            for (int j = i + 3; j <= (int)bytes[i + 2] + 3; j++)
                            {
                                frame.Add(bytes[j]);
                            }
                            try
                            {
                                MachineIO.DataToIO(new byte[] { frame[4], frame[5], frame[6], frame[7] });
                            }
                            catch (Exception ex)
                            {
                            }
                            {
                                Console.Write("SYS INPUT:");
                                foreach (var item in frame)
                                {
                                    Console.Write(item.ToString("X2") + " ");
                                }
                                Console.WriteLine(" ");
                                return;
                            }
                        }
                    }
                }
            }
        }

        public void SendControl()
        {
            var data = MachineIO.IOtoData();
            if (SerialPort.Port.IsOpen)
            {
                SerialPort.SendBytes(SystemComunication.GetFrame(data));
            }
        }

        public void SendStartToSubPc()
        {
            if (SerialPort.Port.IsOpen)
            {
                byte[] tx = { 0x55, 0x01, 0xFF };
                SerialPort.SendBytes(tx);
            }
        }

        public void SendOkToMain()
        {
            if (SerialPort.Port.IsOpen)
            {
                byte[] tx = { 0x55, 0x02, 0xFF };
                SerialPort.SendBytes(tx);
            }
        }

        public void SendNgToMain()
        {
            if (SerialPort.Port.IsOpen)
            {
                byte[] tx = { 0x55, 0x03, 0xFF };
                SerialPort.SendBytes(tx);
            }
        }


        public void GetInput()
        {
            byte[] inputAsk = new byte[2];
            inputAsk[0] = (byte)0x49;
            if (SerialPort.Port.IsOpen)
            {
                Console.WriteLine("Query input ");
                SerialPort.SendBytes(SystemComunication.GetFrame(inputAsk, true));
            }
        }

        public void RLYControl(int idxRelay, bool value)
        {
            switch (idxRelay)
            {
                case 1:
                    MachineIO.RLY1 = value;
                    break;
                case 2:
                    MachineIO.RLY2 = value;
                    break;
                case 3:
                    MachineIO.RLY3 = value;
                    break;
                case 4:
                    MachineIO.RLY4 = value;
                    break;
                case 5:
                    MachineIO.RLY5 = value;
                    break;
                case 6:
                    MachineIO.RLY6 = value;
                    break;

                default:
                    // code block
                    break;
            }
            SendControl();
        }       
        public void PowerRelease()
        {
            MachineIO.AC0 = false;
            MachineIO.BC0 = false;
            MachineIO.ADSC = false;
            MachineIO.BDSC = false;
            MachineIO.LPG = false;
            MachineIO.LPY = false;
            MachineIO.LPR = false;
            MachineIO.BUZZER = false;
            SendControl();
        }

        public bool GEN(int value, List<int> Channel)
        {
            SerialPort.SerialDataReciver -= SerialPort_SerialDataReciver;
            byte[] bytes = new byte[13];
            if (MachineIO.GEN_BYTES.Count() == 13)
            {
                bytes = MachineIO.GEN_BYTES.ToArray();
            }
            bytes[0] = 0x47;
            byte[] intBytes = BitConverter.GetBytes(value);
            Array.Reverse(intBytes);
            byte[] result = intBytes;
            foreach (var item in Channel)
            {
                for (int i = 1; i < 4; i++)
                {
                    bytes[(item - 1) * 3 + i] = result[i];
                }
            }

            Console.Write("GEN: ");
            MachineIO.GEN_BYTES = bytes.ToList();
            bool IsOK = SerialPort.SendAndRead(bytes, 0x47, 1000, out _, false);
            SerialPort.SerialDataReciver += SerialPort_SerialDataReciver;
            return IsOK;
        }

       
    }
}