using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamStatsDumper
{
    public interface IWithNumbers
    {
        int Paid { get; }

        int Free { get; }

        int PaidCumulative { get; }

        int FreeCumulative { get; }
    }
}
