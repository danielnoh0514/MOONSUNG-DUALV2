using HVT.Controls.DevicesControl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HVT.Controls.DeviceControl
{
    public class SevenSegment
    {
        public List<bool> LogicLevel { get; set; }

        public int PinNumber = 0;

        public BoardExtension BoardExtension { get; set; }

        public SevenSegment(BoardExtension pBoardExtension)
        {
            BoardExtension = pBoardExtension;
        }

        public List<bool> Digit0;
        public List<bool> Digit1;
        public List<bool> Sign;
        public List<bool> Digit2;
        public List<bool> Digit3;
        public List<bool> Icons;
        public List<bool> LEDs;

        public void Parse()
        {
            if (PinNumber == 14)
            {
                Digit0 = LogicLevel.Skip(0).Take(7 * 16).ToList();
                Digit1 = LogicLevel.Skip(7 * 16).Take(7 * 16).ToList();
            }

            if (PinNumber == 13)
            {
                Digit2 = LogicLevel.Skip(0).Take(7 * 16).ToList();
                Digit3 = LogicLevel.Skip(7 * 16).Take(2 * 16).ToList();
                Sign = LogicLevel.Skip(9 * 16).Take(2 * 16).ToList();
                Icons = LogicLevel.Skip(11 * 16).Take(2 * 16).ToList();
            }        
           
            return;
        }


        public static int BoolListToDecimal(bool[] boolList)
        {
            if (boolList.Length != 16)
            {
                throw new ArgumentException("The list must contain exactly 16 boolean values.");
            }

            // Build the binary string from the boolean array
            string binaryStr = "";
            foreach (bool bit in boolList)
            {
                binaryStr += bit ? '1' : '0';
            }

            // Convert the binary string to a decimal integer
            int decimalValue = Convert.ToInt32(binaryStr, 2);
            return decimalValue;
        }        
        public void SignalRead()
        {
            LogicLevel = Enumerable.Repeat(false, PinNumber * 16).ToList();

            byte[] Response;

            if (BoardExtension.SerialPort.Port.IsOpen)
            {
                if (BoardExtension.SerialPort.SendAndRead(new byte[] { 0x51 }, 0x51, 1500, out Response))
                {
                    if (Response.Length == PinNumber * 2 + 6)
                    {
                        for (int bitIdx = 0; bitIdx < PinNumber * 16; bitIdx++)
                        {
                            // Determine which byte contains the bit
                            int byteIdx = bitIdx / 8;
                            // Determine the position of the bit within that byte
                            int bitOffset = bitIdx % 8;

                            byte byteValue = Response[4 + byteIdx];

                            // Mask to retrieve the specific bit
                            // Create a bitmask to isolate the specific bit
                            byte mask = (byte)(1 << (7 - bitOffset));
                            // Check if the bit is set (true) or clear (false)
                            bool bitValue = (byteValue & mask) != 0;
                            LogicLevel[bitIdx] = bitValue;
                        }
                    }
                }
            }
        }


        public void SignalRead(int pinNumber)
        {
            LogicLevel = Enumerable.Repeat(false, pinNumber * 16).ToList();

            byte[] Response;

            if (BoardExtension.SerialPort.Port.IsOpen)
            {
                if (BoardExtension.SerialPort.SendAndRead(new byte[] { 0x52 }, 0x52, 1500, out Response))
                {
                    if (Response.Length == pinNumber * 2 + 6)
                    {
                        for (int bitIdx = 0; bitIdx < pinNumber * 16; bitIdx++)
                        {
                            // Determine which byte contains the bit
                            int byteIdx = bitIdx / 8;
                            // Determine the position of the bit within that byte
                            int bitOffset = bitIdx % 8;

                            byte byteValue = Response[4 + byteIdx];

                            // Mask to retrieve the specific bit
                            // Create a bitmask to isolate the specific bit
                            byte mask = (byte)(1 << (7 - bitOffset));
                            // Check if the bit is set (true) or clear (false)
                            bool bitValue = (byteValue & mask) != 0;
                            LogicLevel[bitIdx] = bitValue;
                        }
                    }
                }
            }
        }
    }
}
