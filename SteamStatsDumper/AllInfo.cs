using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamStatsDumper
{
    public class AllInfo : IWithNumbers
    {
        public TypeDay CurrentDay { get; set; }

        public Dictionary<string, TypeInfo> AppType = new Dictionary<string, TypeInfo>();

        public int Paid => AppType.Values.Sum(x => x.Paid);

        public int Free => AppType.Values.Sum(x => x.Free);

        public int PaidCumulative => AppType.Values.Sum(x => x.Paid);

        public int FreeCumulative => AppType.Values.Sum(x => x.Free);

        public Dictionary<DateTime, TypeDay> Days { get; } = new Dictionary<DateTime, TypeDay>();
    }
}
