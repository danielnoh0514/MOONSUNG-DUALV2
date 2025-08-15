using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HVT.Controls
{
    public enum MachineStatus
    {
        UNKOWN,
        CLDOWN,
        READY,
        WAIT,
        TESTING,
        OK_TESTING,
        CLUP,
        HEARTBEAT  // Added for network connection monitoring
    }

    public enum SourceMachine
    {
        MAIN,
        SUB,
    }
    
    public class Message
    {
        public SourceMachine SourceMachine { get; set; }
        public MachineStatus MachineStatus { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

