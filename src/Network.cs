using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TVFollow
{
    // One IPv4 network the PC is on: its address and mask.
    class LocalNetwork
    {
        public uint Address;
        public uint Mask;

        public bool Contains(string ip)
        {
            uint a;
            return TryParse(ip, out a) && (a & Mask) == (Address & Mask);
        }

        public string Broadcast { get { return ToText((Address & Mask) | ~Mask); } }

        // The addresses to try when looking for a TV. Networks up to 1,022 addresses (a /22) are
        // tried whole; on bigger ones only the 254 around "near" (the TV's last address, or the PC).
        public List<string> Hosts(string near)
        {
            var hosts = new List<string>();
            uint first, last;
            if (~Mask <= 1023)
            {
                first = (Address & Mask) + 1;
                last = (Address & Mask) | ~Mask;
            }
            else
            {
                uint centre;
                if (!TryParse(near, out centre) || !Contains(near)) centre = Address;
                first = (centre & 0xFFFFFF00) + 1;
                last = (centre & 0xFFFFFF00) + 255;
            }
            for (uint a = first; a < last; a++) hosts.Add(ToText(a));
            return hosts;
        }

        public static bool TryParse(string ip, out uint value)
        {
            value = 0;
            IPAddress parsed;
            if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out parsed) || parsed.AddressFamily != AddressFamily.InterNetwork) return false;
            byte[] b = parsed.GetAddressBytes();
            value = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
            return true;
        }

        public static string ToText(uint a)
        {
            return (a >> 24) + "." + ((a >> 16) & 255) + "." + ((a >> 8) & 255) + "." + (a & 255);
        }
    }

    static class Discovery
    {
        // The IPv4 networks the PC is on right now (not loopback or self-assigned 169.254).
        public static List<LocalNetwork> LocalNetworks()
        {
            var networks = new List<LocalNetwork>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (LocalNetwork net in NetworksOf(nic.GetIPProperties()))
                    if (!networks.Exists(n => (n.Address & n.Mask) == (net.Address & net.Mask) && n.Mask == net.Mask)) networks.Add(net);
            }
            return networks;
        }

        // the IPv4 networks of one adapter (not loopback or self-assigned 169.254)
        static List<LocalNetwork> NetworksOf(IPInterfaceProperties adapter)
        {
            var networks = new List<LocalNetwork>();
            foreach (var addr in adapter.UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(addr.Address)) continue;
                var net = new LocalNetwork();
                LocalNetwork.TryParse(addr.Address.ToString(), out net.Address);
                if ((net.Address >> 16) == 0xA9FE) continue; // 169.254.x.x
                uint mask;
                net.Mask = addr.IPv4Mask != null && LocalNetwork.TryParse(addr.IPv4Mask.ToString(), out mask) && mask != 0 ? mask : 0xFFFFFF00;
                networks.Add(net);
            }
            return networks;
        }

        // Is this address on one of the PC's networks?
        public static bool IsLocal(string ip)
        {
            return LocalNetworks().Exists(n => n.Contains(ip));
        }

        // Is the PC on the network the TV was last seen on? The same numbers aren't enough: a
        // laptop can join a hotel's or an office's network that also uses 192.168.1.x. Once the
        // home router is known it has to be the same router; before that, the numbers are all
        // there is to go on.
        public static bool OnNetworkOf(bool sameNumbers, string savedGatewayMac, string gatewayMac)
        {
            if (!sameNumbers) return false;
            if (string.IsNullOrEmpty(savedGatewayMac)) return true;
            return string.Equals(savedGatewayMac, gatewayMac, StringComparison.OrdinalIgnoreCase);
        }

        // The hardware address of the router (default gateway) of the network this address is
        // on, like "AA:BB:CC:DD:EE:FF", or "" when there is no router or it doesn't answer.
        public static string GatewayMacFor(string ip)
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    IPInterfaceProperties adapter = nic.GetIPProperties();
                    if (!NetworksOf(adapter).Exists(n => n.Contains(ip))) continue;
                    foreach (GatewayIPAddressInformation gateway in adapter.GatewayAddresses)
                    {
                        if (gateway.Address.AddressFamily != AddressFamily.InterNetwork || gateway.Address.Equals(IPAddress.Any)) continue;
                        string mac = MacOf(gateway.Address);
                        if (mac.Length > 0) return mac;
                    }
                }
            }
            catch { } // an adapter going away mid-walk
            return "";
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        static extern int SendARP(uint destination, uint source, byte[] mac, ref uint macLength);

        static string MacOf(IPAddress address)
        {
            var mac = new byte[6];
            uint length = 6;
            // the address bytes as they are on the wire, not the number LocalNetwork uses
            uint destination = BitConverter.ToUInt32(address.GetAddressBytes(), 0);
            // a failed lookup can come back as "no error" with no address, so the length counts too
            if (SendARP(destination, 0, mac, ref length) != 0 || length != 6) return "";
            return BitConverter.ToString(mac).Replace('-', ':');
        }

        // Every address on the PC's own networks that accepts a connection on the port
        // (setup's search). Only outgoing connections, so no firewall prompt.
        public static List<string> SweepAll(int port, int waitMs)
        {
            var hosts = new List<string>();
            foreach (LocalNetwork net in LocalNetworks()) hosts.AddRange(net.Hosts(null));
            return Sweep(hosts, port, waitMs);
        }

        // Looking for a TV that moved: only the network its last address is on, and only if
        // the PC is on that network.
        public static List<string> SweepAround(string lastIp, int port, int waitMs)
        {
            LocalNetwork net = LocalNetworks().Find(n => n.Contains(lastIp));
            return net == null ? new List<string>() : Sweep(net.Hosts(lastIp), port, waitMs);
        }

        public static List<string> Sweep(List<string> hosts, int port, int waitMs)
        {
            var clients = new List<TcpClient>();
            var connects = new List<Task>();
            foreach (string host in hosts)
            {
                var client = new TcpClient();
                clients.Add(client);
                Task connect = null;
                try { connect = client.ConnectAsync(host, port); }
                catch { }
                connects.Add(connect);
            }
            Thread.Sleep(waitMs);
            var open = new List<string>();
            for (int i = 0; i < clients.Count; i++)
            {
                if (connects[i] != null && connects[i].Status == TaskStatus.RanToCompletion) open.Add(hosts[i]);
                clients[i].Close();
            }
            return open;
        }

        // The TV with this serial number among the addresses, or null.
        public static string FindBySerial(IEnumerable<string> addresses, int port, string serial)
        {
            foreach (string ip in addresses)
            {
                DeviceInfo info = new RokuClient(ip, port).GetDeviceInfo(3000);
                if (info != null && info.Serial == serial) return ip;
            }
            return null;
        }
    }

    static class Wake
    {
        // Wake-on-LAN "magic packet" to the broadcast address of every network the PC is on,
        // and to 255.255.255.255. A TV with Roku built in that has gone too deep into standby to
        // answer will still wake up for this. Returns how many packets went out (0: no network).
        public static int Send(string mac)
        {
            byte[] address = ParseMac(mac);
            if (address == null) return 0;
            var packet = new byte[102];
            for (int i = 0; i < 6; i++) packet[i] = 0xFF;
            for (int rep = 0; rep < 16; rep++) Array.Copy(address, 0, packet, 6 + rep * 6, 6);
            var targets = new List<string>();
            foreach (LocalNetwork net in Discovery.LocalNetworks()) targets.Add(net.Broadcast);
            if (targets.Count == 0) return 0;
            targets.Add("255.255.255.255");
            int sent = 0;
            using (var udp = new UdpClient())
            {
                udp.EnableBroadcast = true;
                foreach (string target in targets)
                {
                    try { udp.Send(packet, packet.Length, target, 9); sent++; }
                    catch { }
                }
            }
            return sent;
        }

        public static byte[] ParseMac(string mac)
        {
            string hex = (mac ?? "").Replace(":", "").Replace("-", "").Trim();
            if (hex.Length != 12) return null;
            var bytes = new byte[6];
            try
            {
                for (int i = 0; i < 6; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            catch { return null; }
            return bytes;
        }
    }
}
