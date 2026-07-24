using HVT.Controls.DevicesControl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace HVT.Controls
{
    public class SolenoidCard
    {
        public ObservableCollection<SolenoidChannel> Chanels { get; set; } = new ObservableCollection<SolenoidChannel>()
        {
            new SolenoidChannel { Channel_P = 1 },
            new SolenoidChannel { Channel_P = 2 },
            new SolenoidChannel { Channel_P = 3 },
            new SolenoidChannel { Channel_P = 4 },
            new SolenoidChannel { Channel_P = 5 },
            new SolenoidChannel { Channel_P = 6 },
            new SolenoidChannel { Channel_P = 7 },
            new SolenoidChannel { Channel_P = 8 },
            new SolenoidChannel { Channel_P = 9 },
            new SolenoidChannel { Channel_P = 10 },
            new SolenoidChannel { Channel_P = 11 },
            new SolenoidChannel { Channel_P = 12 },
            new SolenoidChannel { Channel_P = 13 },
            new SolenoidChannel { Channel_P = 14 },
            new SolenoidChannel { Channel_P = 15 },
            new SolenoidChannel { Channel_P = 16 },
            new SolenoidChannel { Channel_P = 17 },
            new SolenoidChannel { Channel_P = 18 },
            new SolenoidChannel { Channel_P = 19 },
            new SolenoidChannel { Channel_P = 20 },
            new SolenoidChannel { Channel_P = 21 },
            new SolenoidChannel { Channel_P = 22 },
            new SolenoidChannel { Channel_P = 23 },
            new SolenoidChannel { Channel_P = 24 }
        };
        private SerialPortDisplay _SerialPort;
        public SerialPortDisplay SerialPort
        {
            get { return _SerialPort; }
            set
            {
                if (value != null || value != _SerialPort)
                    _SerialPort = value;
            }
        }
        public SolenoidCard(WrapPanel panelSelect)
        {
            panelSelect.Children.Clear();
            foreach (var item in Chanels)
            {
                panelSelect.Children.Add(item.CbUse);
            }
        }
        public SolenoidCard()
        {
            foreach (var item in Chanels)
            {
                item.ManualStateChange += Item_ManualStateChange;
            }
        }

        public void Update()
        {
            foreach (var item in Chanels)
            {
                item.ManualStateChange -= Item_ManualStateChange; 
                item.ManualStateChange += Item_ManualStateChange;
            }
        }

        private void Item_ManualStateChange(object sender, EventArgs e)
        {
            (sender as SolenoidChannel).ManualStateChange -= Item_ManualStateChange;
            bool changeOk = SendCardStatus2();
            (sender as SolenoidChannel).IsON = (sender as SolenoidChannel).IsON ? changeOk : !changeOk;
            (sender as SolenoidChannel).ManualStateChange += Item_ManualStateChange;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {

        }

        public void SelectAll()
        {
            foreach (var item in Chanels)
            {
                item.isUse = true;
            }
        }

        public void ClearAll()
        {
            foreach (var item in Chanels)
            {
                item.isUse = false;
            }
        }

        public void SetPort()
        { 

        }

        public void Release()
        {
            foreach (var item in Chanels)
            {
                item.isOn = false;
            }
            SendCardStatus2();
        }



        public bool SendCardStatus()
        {
            byte[] cardChannel = new byte[5];

            cardChannel[0] = (byte)0x53;

            UInt32 data = 0;
            for (int i = Chanels.Count - 1; i >= 0; i--)
            {
                data = data << 1;
                if (Chanels[i].IsON)
                {
                    Console.WriteLine("on channel : " + i);
                    data |= 1;
                }
            }
            Console.WriteLine("- {0}- ", Convert.ToString(data, 2).PadLeft(24,'0'));
            var bytes = BitConverter.GetBytes(data);
            for (int i = cardChannel.Length - 1; i >= 1; i--)
            {
                cardChannel[i] = bytes[cardChannel.Length - 1 - i];
                Console.WriteLine("byte {0} : {1}", i, Convert.ToString(cardChannel[i], 2));
            }

            return SerialPort.SendToControls(cardChannel, 500, new byte[] { 0x53, 0x00 });
        }

        // Selects the data-byte order for SendCardStatus2, because the target boards read the
        // frame differently:
        //   true  -> dedicated Soleinoid_Atmel board: [4]=S01-08 [5]=S09-16 [6]=S17-24 [7]=reserved
        //   false -> SystemDUAL fallback (always available): [4]=reserved [5]=S17-24 [6]=S09-16 [7]=S01-08
        // Set by ProgramDevices from Communication.SolenoidPort.Use (dedicated port connected = Atmel).
        public bool UseAtmelFrame { get; set; } = false;

        // Builds the fixed 10-byte solenoid frame explicitly and sends it raw (already framed;
        // no GetFrame wrapping), with DiscardInBuffer + up to 3 retries.
        //   [0]=0x44 'D' [1]=0x45 'E' [2]=0x06 len [3]=0x53 'S' | data(4) | [8]=XOR of [0..7] [9]=0x56 'V'
        // Data-byte order depends on UseAtmelFrame (the firmwares are NOT changed).
        public bool SendCardStatus2()
        {
            byte s01_08 = 0, s09_16 = 0, s17_24 = 0;
            foreach (var ch in Chanels)
            {
                if (!ch.IsON) continue;
                int idx = (ch.Channel_P - 1) % 24; // 0..23
                int bit = idx % 8;
                switch (idx / 8)
                {
                    case 0: s01_08 |= (byte)(1 << bit); break; // S01-S08
                    case 1: s09_16 |= (byte)(1 << bit); break; // S09-S16
                    case 2: s17_24 |= (byte)(1 << bit); break; // S17-S24
                }
            }

            byte[] frame = new byte[10];
            frame[0] = 0x44; // 'D'
            frame[1] = 0x45; // 'E'
            frame[2] = 0x06; // payload length
            frame[3] = 0x53; // 'S'
            if (UseAtmelFrame)
            {
                // Soleinoid_Atmel: process_frame() reads output 1-8/9-16/17-24 from frame[4]/[5]/[6].
                frame[4] = s01_08;
                frame[5] = s09_16;
                frame[6] = s17_24;
                frame[7] = 0x00;     // reserved
            }
            else
            {
                // SystemDUAL fallback: KEY/outputs read from the low byte (frame[7]).
                frame[4] = 0x00;     // reserved
                frame[5] = s17_24;
                frame[6] = s09_16;
                frame[7] = s01_08;
            }
            byte chk = 0x00;
            for (int i = 0; i <= 7; i++) chk ^= frame[i];
            frame[8] = chk;
            frame[9] = 0x56; // 'V'

            if (SerialPort == null || SerialPort.Port == null || !SerialPort.Port.IsOpen)
                return false;

            for (int i = 0; i < 3; i++)
            {
                SerialPort.Port.DiscardInBuffer();
                if (SerialPort.SendRawFrame(frame, 500))
                    return true;
            }
            return false;
        }
    }
}
