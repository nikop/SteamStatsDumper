using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamStatsDumper
{
    public class TypeInfo : IWithNumbers
    {
        public string Type { get; set; }

        public int Total => Paid + Free;

        public int Paid => Days.Values.Sum(x => x.Paid);

        public int Free => Days.Values.Sum(x => x.Free);

        public int PaidCumulative => Days.Values.Sum(x => x.Paid);

        public int FreeCumulative => Days.Values.Sum(x => x.Free);

        public TypeDay CurrentDay { get; set; }

        public Dictionary<DateTime, TypeDay> Days { get; } = new Dictionary<DateTime, TypeDay>();
    }
}
