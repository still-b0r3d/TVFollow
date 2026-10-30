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
        // false for a TV set up before that didn't answer this time: setup lists it from the
        // settings (Info then holds only its serial and name), so finishing doesn't drop it unseen
        public bool Answering = true;

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
        // Every Roku on the PC's networks, TVs first, plus the saved addresses: a TV that had to be
        // typed in (on another subnet, say) is found again when setup is reopened.
        public static List<FoundTv> FindAll(int port, IEnumerable<string> savedIps)
        {
            return Probe(WithSaved(Discovery.SweepAll(port, 2000), savedIps), port);
        }

        public static List<string> WithSaved(List<string> swept, IEnumerable<string> savedIps)
        {
            var all = new List<string>(swept);
            if (savedIps != null)
                foreach (string ip in savedIps)
                    if (!string.IsNullOrEmpty(ip) && !all.Contains(ip)) all.Add(ip);
            return all;
        }

        // Asks each address what it is. The same TV twice (the saved address and a new one) is
        // listed once.
        public static List<FoundTv> Probe(IEnumerable<string> addresses, int port)
        {
            Task<FoundTv>[] asks = addresses.Select(ip => Task.Run(() =>
            {
                DeviceInfo info = new RokuClient(ip, port).GetDeviceInfo(3000);
                return info == null ? null : new FoundTv { Ip = ip, Info = info };
            })).ToArray();
            Task.WaitAll(asks);
            return asks.Select(t => t.Result).Where(f => f != null)
                .GroupBy(f => f.Info.Serial.Length > 0 ? f.Info.Serial : f.Ip).Select(g => g.First())
                .OrderByDescending(f => f.Info.IsTv).ThenBy(f => f.Title).ToList();
        }
    }
}
