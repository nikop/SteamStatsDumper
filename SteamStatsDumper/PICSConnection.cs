using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;
using SteamKitHelper;

namespace SteamStatsDumper
{
    public class PICSConnection : SteamConnection
    {
        TaskCompletionSource<bool> picsReady = new TaskCompletionSource<bool>();

        public Task<bool> PICSReady => picsReady.Task;

        public PICSConnection(string username, string password, ISteamGuardProvider steamGuard = null, ILoginkeyProvider loginkey = null) : base(username, password, steamGuard, loginkey)
        {
            CallbackManager.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);
        }

        async void OnLicenseList(SteamApps.LicenseListCallback callback)
        {
            Console.WriteLine("Got License List");

            var newPackages = callback.LicenseList.Select(x => x.PackageID).ToList();

            if (!newPackages.Any())
                return;

            try
            {
                await UpdatePackages(newPackages);

                picsReady.SetResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error!");
                Console.Write(ex);
                picsReady.SetResult(false);
            }
        }

        public ConcurrentDictionary<uint, PICSAppInfo> Apps { get; set; } = new ConcurrentDictionary<uint, PICSAppInfo>();

        public ConcurrentDictionary<uint, PICSPackageInfo> Packages { get; set; } = new ConcurrentDictionary<uint, PICSPackageInfo>();

        public ConcurrentDictionary<uint, ulong> Tokens { get; set; } = new ConcurrentDictionary<uint, ulong>();

        protected async Task UpdatePackages(IEnumerable<uint> newPackages)
        {
            // Request info for missing packages
            var info = await SteamApps.PICSGetProductInfo(Enumerable.Empty<SteamApps.PICSRequest>(), newPackages.Select(x => new SteamApps.PICSRequest
            {
                ID = x,
                Public = false
            }));

            await UpdatePackages(info.Results.SelectMany(x => x.Packages.Values).Select(x => new PICSPackageInfo(x)));
        }

        protected async Task UpdatePackages(IEnumerable<PICSPackageInfo> packages)
        {
            var allapps = packages.SelectMany(x => x.Apps).Distinct().ToList();
            var missingapps = allapps.Where(x => !Apps.ContainsKey(x));

            foreach (var package in packages)
                Packages[package.ID] = package;

            if (!missingapps.Any())
                return;

            await UpdateApps(missingapps);
        }

        protected async Task UpdateApps(IEnumerable<uint> newApps)
        {
            // Request info for missing packages
            var info = await SteamApps.PICSGetProductInfo(newApps.Select(x => new SteamApps.PICSRequest
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

        protected async Task<SteamApps.PICSTokensCallback> GetTokens(IEnumerable<uint> apps)
        {
            var tokens = await SteamApps.PICSGetAccessTokens(apps, Enumerable.Empty<uint>());

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

        protected async Task UpdateApps(IEnumerable<PICSAppInfo> apps)
        {
            foreach (var app in apps)
                Apps[app.ID] = app;
        }
    }
}
