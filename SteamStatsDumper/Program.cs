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

        public static Dictionary<string, AppTypeInfo> AppType = new Dictionary<string, AppTypeInfo>
        {
            ["game"] = new AppTypeInfo
            {
                Name = "Games",
            },
            ["dlc"] = new AppTypeInfo
            {
                Name = "DLC",
            },
            ["demo"] = new AppTypeInfo
            {
                Name = "Demo",
            },
            ["tool"] = new AppTypeInfo
            {
                Name = "Tools",
            },
            ["config"] = new AppTypeInfo
            {
                Name = "Config",
            },
            ["unknown"] = new AppTypeInfo
            {
                Name = "Unknown",
                HelpText = "Unable to determine type. Most likely unreleased apps without store page.",
            },
            ["application"] = new AppTypeInfo
            {
                Name = "Software",
            },
            ["series"] = new AppTypeInfo
            {
                Name = "Series",
                HelpText = "Not meant to be owned. Series are",
            },
            ["video"] = new AppTypeInfo
            {
                Name = "Video",
            },
            ["media"] = new AppTypeInfo
            {
                Name = "Legacy Media (Trailers)",
            },
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

            var uniqueLicenseFlags = new HashSet<string>();

            var data = new AllInfo();

            foreach (var license in conn.Licenses.Where(x => !x.LicenseFlags.HasFlag(ELicenseFlags.Expired)).OrderBy(x => x.TimeCreated))
            {
                var date = license.TimeCreated.Date;

                if (data.CurrentDay == null || data.CurrentDay.Date != date)
                {
                    var nextDay = data.CurrentDay?.Date.AddDays(1) ?? date;

                    // Fill gaps
                    while (nextDay <= date)
                    {
                        data.Days[nextDay] = new TypeDay
                        {
                            Date = nextDay,
                            FreeCumulative = data.CurrentDay?.FreeCumulative ?? 0,
                            PaidCumulative = data.CurrentDay?.PaidCumulative ?? 0,
                        };
                        nextDay = nextDay.AddDays(1);
                    }

                    data.CurrentDay = data.Days[date];
                }

                // Skip Family Sharing
                if (license.PaymentMethod == EPaymentMethod.AuthorizedDevice)
                    continue;

                var fl = (license.LicenseFlags & ~ELicenseFlags.RegionRestrictionExpired).ToString();

                if (!uniqueLicenseFlags.Contains(fl))
                    uniqueLicenseFlags.Add(fl);

                var package = conn.Packages[license.PackageID];

                var typesPackage = new List<string>();

                var isfree = TemporaryTypes.Contains(package.BillingType) || package.ID == 0;

                foreach (var appid in package.Apps)
                {
                    if (!isfree && appsOwned.Contains(appid))
                        continue;
                    else if (isfree && appsOwnedFree.Contains(appid))
                        continue;

                    (isfree ? appsOwnedFree : appsOwned).Add(appid);

                    var app = conn.Apps[appid];

                    var appType = app?.Type ?? "unknown";

                    if (!data.AppType.ContainsKey(appType))
                    {
                        data.AppType[appType] = new TypeInfo
                        {
                            Type = appType,
                        };
                    }

                    var typeInfo = data.AppType[appType];

                    if (typeInfo.CurrentDay == null || typeInfo.CurrentDay.Date != date)
                    {
                        var nextDay = typeInfo.CurrentDay?.Date.AddDays(1) ?? date;

                        // Fill gaps
                        while (nextDay <= date)
                        {
                            typeInfo.Days[nextDay] = new TypeDay
                            {
                                Date = nextDay,
                                FreeCumulative = typeInfo.CurrentDay?.FreeCumulative ?? 0,
                                PaidCumulative = typeInfo.CurrentDay?.PaidCumulative ?? 0,
                            };
                            nextDay = nextDay.AddDays(1);
                        }

                        typeInfo.CurrentDay = typeInfo.Days[date];
                    }

                    if (isfree)
                    {
                        // Global Total
                        data.CurrentDay.Free++;
                        data.CurrentDay.FreeCumulative++;

                        // This Type
                        typeInfo.CurrentDay.Free++;
                        typeInfo.CurrentDay.FreeCumulative++;
                    }
                    else
                    {
                        // Global Total
                        data.CurrentDay.Paid++;
                        data.CurrentDay.PaidCumulative++;

                        // This Type
                        typeInfo.CurrentDay.Paid++;
                        typeInfo.CurrentDay.PaidCumulative++;
                    }
                }
            }

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

            foreach (var key in data.AppType.Keys)
            {
                cums[$"{key}_free"] = 0;
                cums[$"{key}_free_cumulative"] = 0;
                cums[$"{key}_paid"] = 0;
                cums[$"{key}_paid_cumulative"] = 0;

                sb.Append($";{key}_free;{key}_free_cumulative");
                sb.Append($";{key}_paid;{key}_paid_cumulative");
            }

            sb.AppendLine();

            foreach (var day in data.Days)
            {
                sb.Append($"{day.Key.ToShortDateString()}");

                foreach (var kv in data.AppType)
                {
                    if (!kv.Value.Days.ContainsKey(day.Key))
                    {
                        // No activations in current day
                        var cumufree = cums[$"{kv.Key}_free"];
                        var cumupaid = cums[$"{kv.Key}_free"];

                        sb.Append($";0;{cumufree};0;{cumupaid}");

                        continue;
                    }

                    var typeDay = kv.Value.Days[day.Key];

                    sb.Append($";{typeDay.Free};{typeDay.FreeCumulative};{typeDay.Paid};{typeDay.PaidCumulative}");
                }

                sb.AppendLine();
            }

            File.WriteAllText("games.csv", sb.ToString());

            Console.WriteLine("Written to games.csv");
        }

        protected static string ProcessTemplate(string filename, Dictionary<string, string> vars)
        {
            var template = new StringBuilder(File.ReadAllText($"template-{filename}.html"));

            foreach (var item in vars)
                template.Replace($"%{item.Key}%", item.Value);

            template.Replace("%", "&#37;");

            return template.ToString();
        }

        // TODO: Add rest of axis
        protected static string OutputDatapoints(IEnumerable<TypeDay> day)
        {
            return JsonConvert.SerializeObject(new object[] {
                new
                {
                    x = day.Select(x => x.Date.ToString()),
                    y = day.Select(x => x.PaidCumulative),
                    type = "scatter"
                },
            });
        }

        protected static void OutputHtml(AllInfo data)
        {
            var mainFile = "index.html";
            var gamesFile = "games.html";

            var files = new Dictionary<string, string>
            {
                [mainFile] = OutputMain(data),
                [mainFile] = OutputGames(data),
            };

            var vars = new Dictionary<string, string>
            {
                ["time"] = DateTimeOffset.Now.ToString(),
                ["filemain"] = mainFile,
                ["filegames"] = gamesFile
            };

            var dirname = $"output";

            if (!Directory.Exists(dirname))
                Directory.CreateDirectory(dirname);

            foreach (var kv in files)
                File.WriteAllText(Path.Combine(dirname, kv.Key), kv.Value);

            Console.WriteLine("HTML-output done");
        }

        protected static string OutputMain(AllInfo data)
        {
            var vars = new Dictionary<string, string>
            {
            };

            var sbTypeTable = new StringBuilder();
            var sbAllTypes = new StringBuilder();

            foreach (var item in data.AppType.Values)
            {
                var typeExists = AppType.ContainsKey(item.Type);

                var varsType = new Dictionary<string, string>
                {
                    ["type"] = item.Type,
                    ["name"] = typeExists ? AppType[item.Type].Name : item.Type,
                    ["help"] = (typeExists ? AppType[item.Type].HelpText : null) ?? "",
                    ["total"] = item.Total.ToString("N0"),
                    ["free"] = item.Free.ToString("N0"),
                    ["paid"] = item.Paid.ToString("N0")
                };

                varsType["json"] = OutputDatapoints(item.Days.Values);

                sbTypeTable.AppendLine(ProcessTemplate("type-total", varsType));

                sbAllTypes.AppendLine(ProcessTemplate("type", varsType));
            }

            vars["typeTable"] = sbTypeTable.ToString();
            vars["typeSections"] = sbAllTypes.ToString();

            return ProcessTemplate("main", vars);
        }

        protected static string OutputGames(AllInfo data)
        {
            var vars = new Dictionary<string, string>
            {
            };

            return ProcessTemplate("games", vars);
        }
    }
}
