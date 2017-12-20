using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamStatsDumper
{
    public class TypeDay : IWithNumbers
    {
        public DateTime Date { get; set; }

        public int Paid { get; set; }

        public int Free { get; set; }

        public int PaidCumulative { get; set; }

        public int FreeCumulative { get; set; }
    }
}
