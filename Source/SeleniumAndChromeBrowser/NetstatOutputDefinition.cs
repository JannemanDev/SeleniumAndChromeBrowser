using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeleniumAndChromeBrowser
{
    internal class NetstatOutputDefinition
    {
        public string Command { get; set; }
        public string Arguments { get; set; }
        public int NumColumns { get; set; }
        public int ProtoColumnIndex { get; set; }
        public int LocalAddressColumnIndex { get; set; }
        public int ForeignAddressColumnIndex { get; set; }
        public int StateColumnIndex { get; set; }
        public int PidColumnIndex { get; set; }
        public string State { get; set; }
    }
}
