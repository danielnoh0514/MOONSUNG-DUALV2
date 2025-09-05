using HVT.VTM.Base;
using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Linq;
using System.Collections.Generic;
using HVT.Utility;
using System.Windows;
using HVT.Controls;
using Camera;
using HVT.Controls.DevicesControl;
using System.Threading;

namespace HVT.VTM.Program
{
    public partial class Program
    {
        // Device list
        public GWIN_TECH_DMM _DMM { get; set; }
        public MuxCardControl MuxCard { get; set; }
        public RelayControls Relay { get; set; }
        public LevelDataViewer Level { get; set; }
        public EncoderViewer EncoderViewer { get; set; }
        public MicrophoneViewer MicrophoneViewer { get; set; }

        public CDSViewer CDSViewer { get; set; }
        public SolenoidControls Solenoid { get; set; }
        public VisionTester VisionTester { get; set; }
        public SysIOControl System { get; set; }
        public PowerMetter PowerMetter { get; set; }
        public CounterTimer CounterTimer { get; set; }
        public BoardExtension BoardExtension1 { get; set; }
        public BoardExtension BoardExtension2 { get; set; }
        public CameraControl Capture { get; set; }
        public SerialPortDisplay BarcodeReader { get; set; }
        public List<UUTPort> UUTs { get; set; }
        public Printer_QR Printer { get; set; }
        public Program()
        {
            System = new SysIOControl();
            PowerMetter = new PowerMetter();
            CounterTimer = new CounterTimer();
            VisionTester = new VisionTester();
            Level = new LevelDataViewer();
            EncoderViewer = new EncoderViewer();
            MicrophoneViewer = new MicrophoneViewer();
            CDSViewer = new CDSViewer();

            Relay = new RelayControls();
            MuxCard = new MuxCardControl();
            _DMM = new GWIN_TECH_DMM();
            BarcodeReader = new SerialPortDisplay();

            BoardExtension1 = new BoardExtension("COM13");
            BoardExtension2 = new BoardExtension("COM15");

            BoardExtension1.SevenSegment.PinNumber = 14;
            BoardExtension2.SevenSegment.PinNumber = 13;
            Solenoid = new SolenoidControls();

            UUTs = new List<UUTPort>(){
                new UUTPort(){
                    serial = new SerialPortDisplay()
                    {
                        DeviceName = "UUT 1",
                    }
                },
                new UUTPort(){
                    serial = new SerialPortDisplay()
                    {
                        DeviceName = "UUT 2",
                    }
                },
                new UUTPort(){
                    serial = new SerialPortDisplay()
                    {
                        DeviceName = "UUT 3",
                    }
                },
                new UUTPort(){
                    serial = new SerialPortDisplay()
                    {
                        DeviceName = "UUT 4",
                    }
                }
            };

            Printer = new Printer_QR();

            System.System_Board.MachineIO.OnStartRequest += MachineIO_OnStartRequest;
            System.System_Board.MachineIO.OnCancleRequest += MachineIO_OnCancleRequest;
        }



        // Check device comunications

        public async void CheckComnunication()
        {

            //System board checking
            if (AppSetting.Communication.SystemIOPort.Use)
            {
                System.System_Board.SerialPort.Port?.Close();
                await Task.Delay(50);

                System.System_Board.CheckCardComunication(AppSetting.Communication.SystemIOPort.PortName);
                System.System_Board.MachineIO.OnStartRequest += MachineIO_OnStartRequest;
                System.System_Board.MachineIO.OnCancleRequest += MachineIO_OnCancleRequest;
                System.System_Board.MachineIO.OnDoorStateChange += MachineIO_OnDoorStateChange;
                System.System_Board.MachineIO.OnUpDown += MachineIO_OnUpDown;
                await Task.Delay(50);
            }

            // Analog Extension port checking 
            if (AppSetting.Communication.BoardExtensionPort1.Use)
            {
                BoardExtension1.CheckCommunication(AppSetting.Communication.BoardExtensionPort1.PortName);
                await Task.Delay(100);

            }

            if (AppSetting.Communication.SolenoidPort.Use)
            {
                Solenoid.CheckCardComunication(AppSetting.Communication.SolenoidPort.PortName);
                await Task.Delay(100);
            }
            else
            {
                Solenoid.SerialPort.Port = System.System_Board.SerialPort.Port;
            }


            if (AppSetting.Communication.BoardExtensionPort2.Use)
            {
                BoardExtension2.CheckCommunication(AppSetting.Communication.BoardExtensionPort2.PortName);
                await Task.Delay(50);
            }


            if (AppSetting.Communication.Mux1Port.Use)
            {
                MuxCard.SerialPort1.Port?.Close();
                await Task.Delay(50);

                MuxCard.CheckCard1Comunication(AppSetting.Communication.Mux1Port.PortName);
                await Task.Delay(50);

            }
            if (AppSetting.Communication.Mux2Port.Use)
            {
                MuxCard.SerialPort2.Port?.Close();
                await Task.Delay(50);

                MuxCard.CheckCard1Comunication(AppSetting.Communication.Mux2Port.PortName);
                await Task.Delay(50);

            }


            //if (appSetting.Communication.DMM1Port.Use)
            //{
            //    _DMM.DMM1.SerialPort.Port?.Close();
            //    await Task.Delay(50);
            //    _DMM.DMM1.CheckCommunication(appSetting.Communication.DMM1Port.PortName);
            //    await Task.Delay(50);
            //}

            //if (appSetting.Communication.DMM2Port.Use)
            //{
            //    _DMM.DMM2.SerialPort.Port?.Close();
            //    await Task.Delay(50);
            //    _DMM.DMM2.CheckCommunication(appSetting.Communication.DMM2Port.PortName);
            //    await Task.Delay(50);
            //}

            if (AppSetting.Communication.RelayPort.Use)
            {
                Relay.SerialPort.Port?.Close();
                await Task.Delay(50);


                Relay.CheckCardComunication(AppSetting.Communication.RelayPort.PortName);
                await Task.Delay(50);
            }
            if (AppSetting.Communication.LevelPort.Use)
            {

                Level.SerialPort.Port?.Close();
                await Task.Delay(50);


                Level.CheckCardComunication(AppSetting.Communication.LevelPort.PortName);
                await Task.Delay(50);
            }
            if (AppSetting.Communication.PowerMetterPort.Use)
            {
                PowerMetter.SerialPort.Port?.Close();
                await Task.Delay(50);

                PowerMetter.CheckCommunication(AppSetting.Communication.PowerMetterPort.PortName);
                await Task.Delay(50);
            }
            if (AppSetting.Communication.CounterTimerPort.Use)
            {
                CounterTimer.SerialPort.Port?.Close();
                await Task.Delay(50);

                CounterTimer.CheckCommunication(AppSetting.Communication.CounterTimerPort.PortName);
                await Task.Delay(50);
            }

            if (AppSetting.Communication.ScannerPort.Use)
            {
                BarcodeReader.Port?.Close();
                await Task.Delay(50);

                CheckBarcodeReader(AppSetting.Communication.ScannerPort.PortName);
                await Task.Delay(50);
            }

            //if (appSetting.Communication.UUT1Port.Use)
            //{
            //    UUTs[0].serial.Port?.Close();
            //    await Task.Delay(50);   
            //    UUTs[0].CheckPort(appSetting.Communication.UUT1Port.PortName);
            //    await Task.Delay(50);
            //}
            //if (appSetting.Communication.UUT2Port.Use)
            //{
            //    UUTs[1].serial.Port?.Close();
            //    await Task.Delay(50);

            //    UUTs[1].CheckPort(appSetting.Communication.UUT2Port.PortName);
            //    await Task.Delay(50);
            //}
            //if (appSetting.Communication.UUT3Port.Use)
            //{
            //    UUTs[2].serial.Port?.Close();
            //    await Task.Delay(50);

            //    UUTs[2].CheckPort(appSetting.Communication.UUT3Port.PortName);
            //    await Task.Delay(50);
            //}
            //if (appSetting.Communication.UUT4Port.Use)
            //{
            //    UUTs[3].serial.Port?.Close();
            //    await Task.Delay(50);

            //    UUTs[3].CheckPort(appSetting.Communication.UUT4Port.PortName);
            //    await Task.Delay(50);
            //}

        }

        private void MachineIO_OnUpDown(object sender, EventArgs e)
        {
            string warningMess = "";
            if (!System.System_Board.MachineIO.SS_BL)
            {
                warningMess += "Bottom JIG non lock \n";
            }

            if (!System.System_Board.MachineIO.SS_TL)
            {
                warningMess += "Top JIG non lock \n";
            }

            if (System.System_Board.MachineIO.SS_BR)
            {
                warningMess += "Bottom Card non lock \n";
            }

            if (System.System_Board.MachineIO.SS_TR)
            {
                warningMess += "Top Card non lock \n";
            }

            if (warningMess != "")
            {
                Debug.Write(String.Format("VTM warning: {0}", warningMess), Debug.ContentType.Warning);
            }
        }

        private void MachineIO_OnDoorStateChange(object sender, EventArgs e)
        {
            if (System.System_Board.MachineIO.IsDoorOpen)
            {
                Debug.Write("Machine door open.", Debug.ContentType.Warning);
            }
        }

        private void MachineIO_OnCancleRequest(object sender, EventArgs e)
        {
            if (IsTestting)
            {
                TestState = RunTestState.STOP;
                IsTestting = false;
            }
        }

        private void MachineIO_OnStartRequest(object sender, EventArgs e)
        {

            ResultPanel.Dispatcher.Invoke(new Action(() =>

            ResultPanel.Visibility = Visibility.Hidden));
            if (!IsTestting && IsloadModel)
            {
                if (pageActive == PageActive.AutoPage)
                {
                    IsTestting = true;
                }
            }
        }
    }
}