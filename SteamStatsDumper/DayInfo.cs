using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamStatsDumper
{
    public class AllInfo
    {
        public Dictionary<string, int> Totals = new Dictionary<string, int>();

        public Dictionary<DateTime, DayInfo> Days = new Dictionary<DateTime, DayInfo>();
    }

    public class DayInfo
    {
        public Dictionary<string, int> Entries = new Dictionary<string, int>();
    }
}
