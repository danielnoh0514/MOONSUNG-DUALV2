using HVT.Controls;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HVT.VTM.Program
{
    [AddINotifyPropertyChangedInterface]

    public class ComPort
    {
        public string PortName { get; set; } = string.Empty;

        public bool Use { get; set; } = false;

        public ComPort(string portName) { 
        
            PortName = portName;
        }

    }


    [AddINotifyPropertyChangedInterface]
    public class Communication
    {

        public Network Network { get; set; } = new Network();

        public bool MainPc { get; set; } = false;


        public ComPort SystemIOPort { get; set; } = new ComPort("COM1");
        public ComPort LevelPort { get; set; } = new ComPort("COM2");
        public ComPort Mux1Port { get; set; } = new ComPort("COM3");
        public ComPort Mux2Port { get; set; } = new ComPort("COM4");
        public ComPort RelayPort { get; set; } = new ComPort("COM5");
        public ComPort SolenoidPort { get; set; } = new ComPort("COM6");
        public ComPort DMM1Port { get; set; } = new ComPort("COM7");
        public ComPort DMM2Port { get; set; } = new ComPort("COM8");
        public ComPort PowerMetterPort { get; set; } = new ComPort("COM9");
        public ComPort CounterTimerPort { get; set; } = new ComPort("COM10");
        public ComPort BoardExtensionPort1 { get; set; } = new ComPort("COM11");
        public ComPort BoardExtensionPort2 { get; set; } = new ComPort("COM12");

        public ComPort UUT1Port { get; set; } = new ComPort("COM13");
        public ComPort UUT2Port { get; set; } = new ComPort("COM14");
        public ComPort UUT3Port { get; set; } = new ComPort("COM15");
        public ComPort UUT4Port { get; set; } = new ComPort("COM16");

        public ComPort ScannerPort { get; set; } = new ComPort("COM17");
        public int Scan_Baudrate { get; set; } = 115200;
        public int Scan_Databit { get; set; } = 8;
        public int Scan_Parity_Index { get; set; } = 0;

        public ComPort PrinterPort { get; set; } = new ComPort("COM18");

        public static List<string> ComPorts = new List<string>()
        {
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "COM10",

            "COM11",
            "COM12",
            "COM13",
            "COM14",
            "COM15",
            "COM16",
            "COM17",
            "COM18",
            "COM19",
            "COM20",

            "COM21",
            "COM22",
            "COM23",
            "COM24",
            "COM25",
            "COM26",
            "COM27",
            "COM28",
            "COM29",
            "COM30",

            "COM31",
            "COM32",
            "COM33",
            "COM34",
            "COM35",
            "COM36",
            "COM37",
            "COM38",
            "COM39",
            "COM40",

            "COM41",
            "COM42",
            "COM43",
            "COM44",
            "COM45",
            "COM46",
            "COM47",
            "COM48",
            "COM49",
            "COM50",

            "COM51",
            "COM52",
            "COM53",
            "COM54",
            "COM55",
            "COM56",
            "COM57",
            "COM58",
            "COM59",
            "COM60",

            "COM71",
            "COM72",
            "COM73",
            "COM74",
            "COM75",
            "COM76",
            "COM77",
            "COM78",
            "COM79",
            "COM80",

            "COM81",
            "COM82",
            "COM83",
            "COM84",
            "COM85",
            "COM86",
            "COM87",
            "COM88",
            "COM89",
            "COM90",

            "COM91",
            "COM92",
            "COM93",
            "COM94",
            "COM95",
            "COM96",
            "COM97",
            "COM98",
            "COM99",
            "COM100",
        };
    }
}