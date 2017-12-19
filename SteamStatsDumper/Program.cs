using Newtonsoft.Json;
using SteamKit2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamStatsDumper
{
    class Program
    {

        public static EBillingType[] TemporaryTypes = new EBillingType[]
        {
            EBillingType.FreeOnDemand,
        };

        static PICSConnection conn;

        static Dictionary<string, Action<AllInfo>> Outputs = new Dictionary<string, Action<AllInfo>>();

        static async Task Main(string[] args)
        {
            var outputsTodo = new List<string>();

            if (args.Length == 1)
                outputsTodo.AddRange(args[0].Split(','));
            else if (args.Length != 0)
            {
                Console.WriteLine("Unknown number of arguments");
                return;
            }
            else
                outputsTodo.Add("csv");

            Outputs.Add("csv", OutputCSV);
            Outputs.Add("html", OutputHtml);

            if (outputsTodo.Any(x => !Outputs.ContainsKey(x)))
            {
                Console.WriteLine("Unknown output of arguments");
                return;
            }

            Console.Write("Username: ");
            var username = Console.ReadLine().Trim();
            Console.Write("Password: ");
            var password = Console.ReadLine().Trim();

            conn = new PICSConnection(username, password)
            {
                LoginID = 1337,
            };

            Console.WriteLine("Connecting ");
            await conn.ConnectAsync();
            Console.WriteLine("Connected ");

            var res = await conn.PICSReady;

            if (!res)
            {
                Console.WriteLine("Error. Failed to get metainfo.");
                Environment.Exit(1);
            }

            var appsOwned = new List<uint>();
            var appsOwnedFree = new List<uint>();

            var showTypes = new List<string>();

            var days = new Dictionary<DateTime, DayInfo>();

            var totals = new Dictionary<string, int>();

            var uniqueLicenseFlags = new HashSet<string>();

            foreach (var license in conn.Licenses.Where(x => !x.LicenseFlags.HasFlag(ELicenseFlags.Expired)).OrderBy(x => x.TimeCreated))
            {
                // Skip Family Sharing
                if (license.PaymentMethod == EPaymentMethod.AuthorizedDevice)
                    continue;

                if (!days.ContainsKey(license.TimeCreated.Date))
                    days[license.TimeCreated.Date] = new DayInfo();

                var fl = (license.LicenseFlags & ~ELicenseFlags.RegionRestrictionExpired).ToString();

                if (!uniqueLicenseFlags.Contains(fl))
                    uniqueLicenseFlags.Add(fl);

                var di = days[license.TimeCreated.Date];

                var package = conn.Packages[license.PackageID];

                var typesPackage = new List<string>();

                var isfree = TemporaryTypes.Contains(package.BillingType) || package.ID == 0;

                var paymentType = isfree ? "free" : "paid";
                typesPackage.Add(paymentType);

                foreach (var appid in package.Apps)
                {
                    if (!isfree && appsOwned.Contains(appid))
                        continue;
                    else if (isfree && appsOwnedFree.Contains(appid))
                        continue;

                    (isfree ? appsOwnedFree : appsOwned).Add(appid);

                    var types = new List<string>();
                    types.AddRange(typesPackage);

                    var app = conn.Apps[appid];

                    var appType = app?.Type ?? "unknown";

                    types.Add($"{appType}_{paymentType}");

                    foreach (var type in types)
                    {
                        if (!di.Entries.ContainsKey(type))
                            di.Entries[type] = 1;
                        else
                            di.Entries[type]++;

                        if (!totals.ContainsKey(type))
                            totals[type] = 1;
                        else
                            totals[type]++;
                    }
                }
            }

            var data = new AllInfo
            {
                Totals = totals,
                Days = days,
            };

            foreach (var format in outputsTodo)
            {
                Outputs[format](data);
            }

            Environment.Exit(0);
        }

        protected static void OutputCSV(AllInfo data)
        {
            var sb = new StringBuilder();

            var cums = new Dictionary<string, int>();

            sb.Append($"DAY");

            foreach (var key in data.Totals.Keys)
            {
                cums[key] = 0;
                sb.Append($";{key};{key}_cumulative");
            }

            sb.AppendLine();

            foreach (var day in data.Days)
            {
                sb.Append($"{day.Key.ToShortDateString()}");

                foreach (var key in data.Totals.Keys)
                {
                    var t = day.Value.Entries.ContainsKey(key) ? day.Value.Entries[key] : 0;

                    cums[key] += t;

                    sb.Append($";{t};{cums[key]}");
                }

                sb.AppendLine();
            }

            File.WriteAllText("games.csv", sb.ToString());

            Console.WriteLine("Written to games.csv");
        }

        protected static void OutputHtml(AllInfo data)
        {
            var template = new StringBuilder(File.ReadAllText("template.html"));

            var vars = new Dictionary<string, string>
            {
                ["time"] = DateTimeOffset.Now.ToString(),
                ["packagesJson"] = JsonConvert.SerializeObject(conn.Packages, Formatting.Indented),
                ["appsJson"] = JsonConvert.SerializeObject(conn.Apps, Formatting.Indented),
            };

            foreach (var item in vars)
                template.Replace($"%{item.Key}%", item.Value);

            File.WriteAllText("games.html", template.ToString());
        }
    }
}
