using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TVFollow
{
    // A Roku found on the network.
    class FoundTv
    {
        public string Ip;
        public DeviceInfo Info;

        // the address as shown in the setup window (the screenshot tool shows a home-network one)
        public virtual string Address { get { return Ip; } }

        public string Title { get { return Info.Name.Length > 0 ? Info.Name : "Roku at " + Address; } }

        public string Details
        {
            get
            {
                var parts = new List<string>();
                if (Info.Location.Length > 0) parts.Add(Info.Location);
                string model = (Info.Vendor + " " + Info.Model).Trim();
                if (model.Length > 0) parts.Add(model);
                parts.Add(Address);
                return string.Join("  ·  ", parts);
            }
        }
    }

    static class TvScanner
    {
        // Every Roku on the PC's networks, TVs first.
        public static List<FoundTv> FindAll(int port)
        {
            return Probe(Discovery.Sweep(port, 2000), port);
        }

        public static List<FoundTv> Probe(IEnumerable<string> addresses, int port)
        {
            Task<FoundTv>[] asks = addresses.Select(ip => Task.Run(() =>
            {
                DeviceInfo info = new RokuClient(ip, port).GetDeviceInfo(3000);
                return info == null ? null : new FoundTv { Ip = ip, Info = info };
            })).ToArray();
            Task.WaitAll(asks);
            return asks.Select(t => t.Result).Where(f => f != null)
                .OrderByDescending(f => f.Info.IsTv).ThenBy(f => f.Title).ToList();
        }
    }
}
