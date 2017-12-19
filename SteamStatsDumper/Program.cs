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
        public abstract class PICSInfo
        {
            public uint ID { get; set; }

            public uint ChangeNumber { get; set; }

            protected PICSInfo()
            {
            }

            public PICSInfo(SteamApps.PICSProductInfoCallback.PICSProductInfo info)
            {
                ID = info.ID;
                ChangeNumber = info.ChangeNumber;
            }
        }

        public class PICSPackageInfo : PICSInfo
        {
            public List<uint> Apps { get; set; }

            public EBillingType BillingType { get; set; }

            public bool ReleaseStateOverride { get; set; }

            public PICSPackageInfo()
            {
            }

            public PICSPackageInfo(SteamApps.PICSProductInfoCallback.PICSProductInfo info) : base(info)
            {
                Apps = info.KeyValues["appids"].Children.Select(x => x.AsUnsignedInteger()).ToList();
                BillingType = (EBillingType)info.KeyValues["billingtype"].AsInteger();
                ReleaseStateOverride = info.KeyValues["extended"]["releasestateoverride"] != KeyValue.Invalid;
            }
        }

        public class PICSAppInfo : PICSInfo
        {
            public string Name { get; set; }

            public string Type { get; set; }

            public string ReleaseState { get; set; }

            public PICSAppInfo()
            {
            }

            public PICSAppInfo(SteamApps.PICSProductInfoCallback.PICSProductInfo info) : base(info)
            {
                Name = info.KeyValues["common"]["name"].AsString();
                Type = info.KeyValues["common"]["type"].AsString()?.ToLower();
                ReleaseState = info.KeyValues["common"]["releasestate"].AsString() ?? (info.KeyValues["common"] != KeyValue.Invalid ? "released" : "unavailable");
            }
        }

        public class DayInfo
        {
            public Dictionary<string, int> Entries = new Dictionary<string, int>();
        }

        public static EBillingType[] TemporaryTypes = new EBillingType[]
        {
            EBillingType.FreeOnDemand,
        };

        static async void OnLicenseList(SteamApps.LicenseListCallback callback)
        {
            Console.WriteLine("Got License List");

            var newPackages = callback.LicenseList.Select(x => x.PackageID).ToList();

            if (!newPackages.Any())
                return;

            try
            {
                Console.WriteLine("Getting metadata");

                await UpdatePackages(newPackages);

                Console.WriteLine("Got metadata");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error!");
                Console.Write(ex);
                throw;
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

                var package = Packages[license.PackageID];

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

                    var app = Apps[appid];

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

            var sb = new StringBuilder();

            var cums = new Dictionary<string, int>();

            foreach (var key in totals.Keys)
                cums[key] = 0;

            sb.Append($"DAY");

            foreach (var key in totals.Keys)
                sb.Append($";{key};{key}_cumulative");

            sb.AppendLine();

            foreach (var day in days)
            {
                sb.Append($"{day.Key.ToShortDateString()}");

                foreach (var key in totals.Keys)
                {
                    var t = day.Value.Entries.ContainsKey(key) ? day.Value.Entries[key] : 0;

                    cums[key] += t;

                    sb.Append($";{t};{cums[key]}");
                }

                sb.AppendLine();
            }

            File.WriteAllText("games.csv", sb.ToString());

            Console.WriteLine("Written to games.csv");

            Environment.Exit(0);
        }

        protected static ConcurrentDictionary<uint, PICSAppInfo> Apps { get; set; } = new ConcurrentDictionary<uint, PICSAppInfo>();

        protected static ConcurrentDictionary<uint, PICSPackageInfo> Packages { get; set; } = new ConcurrentDictionary<uint, PICSPackageInfo>();

        protected static  ConcurrentDictionary<uint, ulong> Tokens { get; set; } = new ConcurrentDictionary<uint, ulong>();

        protected static async Task UpdatePackages(IEnumerable<uint> newPackages)
        {
            // Request info for missing packages
            var info = await conn.SteamApps.PICSGetProductInfo(Enumerable.Empty<SteamApps.PICSRequest>(), newPackages.Select(x => new SteamApps.PICSRequest
            {
                ID = x,
                Public = false
            }));

            await UpdatePackages(info.Results.SelectMany(x => x.Packages.Values).Select(x => new PICSPackageInfo(x)));
        }

        protected static async Task UpdatePackages(IEnumerable<PICSPackageInfo> packages)
        {
            var allapps = packages.SelectMany(x => x.Apps).Distinct().ToList();
            var missingapps = allapps.Where(x => !Apps.ContainsKey(x));

            foreach (var package in packages)
                Packages[package.ID] = package;

            if (!missingapps.Any())
                return;

            await UpdateApps(missingapps);
        }

        protected static async Task UpdateApps(IEnumerable<uint> newApps)
        {
            // Request info for missing packages
            var info = await conn.SteamApps.PICSGetProductInfo(newApps.Select(x => new SteamApps.PICSRequest
            {
                ID = x,
                Public = false,
                AccessToken = Tokens.ContainsKey(x) ? Tokens[x] : 0,
            }), Enumerable.Empty<SteamApps.PICSRequest>());

            await UpdateApps(info.Results.SelectMany(x => x.Apps.Values).Select(x => new PICSAppInfo(x)));

            var missingTokens = info.Results.SelectMany(x => x.Apps.Values).Where(x => x.MissingToken).Select(x => x.ID).ToList();

            if (missingTokens.Any())
                await GetTokens(missingTokens);
        }

        protected static async Task<SteamApps.PICSTokensCallback> GetTokens(IEnumerable<uint> apps)
        {
            var tokens = await conn.SteamApps.PICSGetAccessTokens(apps, Enumerable.Empty<uint>());

            foreach (var token in tokens.AppTokens)
            {
                if (token.Value == 0)
                    continue;

                Tokens[token.Key] = token.Value;
            }

            if (tokens.AppTokens.Any())
                await UpdateApps(tokens.AppTokens.Keys);

            return tokens;
        }

        protected static async Task UpdateApps(IEnumerable<PICSAppInfo> apps)
        {
            foreach (var app in apps)
                Apps[app.ID] = app;
        }

        static SteamKitHelper.SteamConnection conn;

        static async Task Main(string[] args)
        {
            Console.Write("Username: ");
            var username = Console.ReadLine().Trim();
            Console.Write("Password: ");
            var password = Console.ReadLine().Trim();

            conn = new SteamKitHelper.SteamConnection(username, password);

            conn.CallbackManager.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);

            Console.WriteLine("Connecting ");
            await conn.ConnectAsync();
            Console.WriteLine("Connected ");
        }
    }
}
