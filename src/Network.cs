using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TVFollow
{
    static class Discovery
    {
        // Every address on the PC's own networks (the a.b.c.1-254 around each of its addresses)
        // that accepts a connection on the port. Only outgoing connections, so no firewall prompt.
        public static List<string> Sweep(int port, int waitMs)
        {
            var addresses = new List<string>();
            var clients = new List<TcpClient>();
            var connects = new List<Task>();
            foreach (string prefix in LocalPrefixes())
            {
                for (int i = 1; i < 255; i++)
                {
                    var client = new TcpClient();
                    addresses.Add(prefix + i);
                    clients.Add(client);
                    Task connect = null;
                    try { connect = client.ConnectAsync(prefix + i, port); }
                    catch { }
                    connects.Add(connect);
                }
            }
            Thread.Sleep(waitMs);
            var open = new List<string>();
            for (int i = 0; i < clients.Count; i++)
            {
                if (connects[i] != null && connects[i].Status == TaskStatus.RanToCompletion) open.Add(addresses[i]);
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

        // "a.b.c." for each IPv4 network the PC is on (not loopback or self-assigned 169.254).
        public static List<string> LocalPrefixes()
        {
            var prefixes = new List<string>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(addr.Address)) continue;
                    byte[] b = addr.Address.GetAddressBytes();
                    if (b[0] == 169 && b[1] == 254) continue;
                    string prefix = b[0] + "." + b[1] + "." + b[2] + ".";
                    if (!prefixes.Contains(prefix)) prefixes.Add(prefix);
                }
            }
            return prefixes;
        }
    }

    static class Wake
    {
        // Wake-on-LAN "magic packet" to every local network the PC is on. A TV with Roku built in
        // that has gone too deep into standby to answer will still wake up for this.
        public static void Send(string mac)
        {
            byte[] address = ParseMac(mac);
            if (address == null) return;
            var packet = new byte[102];
            for (int i = 0; i < 6; i++) packet[i] = 0xFF;
            for (int rep = 0; rep < 16; rep++) Array.Copy(address, 0, packet, 6 + rep * 6, 6);
            using (var udp = new UdpClient())
            {
                udp.EnableBroadcast = true;
                foreach (string prefix in Discovery.LocalPrefixes())
                {
                    try { udp.Send(packet, packet.Length, prefix + "255", 9); }
                    catch { }
                }
            }
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
