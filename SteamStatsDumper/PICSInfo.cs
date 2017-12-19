using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamStatsDumper
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
}
