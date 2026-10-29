using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Bajoterra.Unity
{
    /// <summary>
    /// The code a friend types to join the game this computer opened: its address on the local network, plus the port
    /// when it is not the usual one. Null when the computer has no network address to share.
    /// </summary>
    public static class LanAddress
    {
        public const int DefaultPort = 7777;

        public static string InviteCode(int port)
        {
            string address = Find();
            if (address == null)
            {
                return null;
            }
            return port == DefaultPort ? address : address + ":" + port;
        }

        /// <summary>Splits a code typed by the player ("192.168.1.20" or "192.168.1.20:7778").</summary>
        public static void Parse(string code, out string host, out int port)
        {
            host = (code ?? string.Empty).Trim();
            port = DefaultPort;
            int colon = host.LastIndexOf(':');
            if (colon > 0 && host.IndexOf(':') == colon && int.TryParse(host.Substring(colon + 1), out int parsed)
                && parsed > 0 && parsed < 65536)
            {
                port = parsed;
                host = host.Substring(0, colon);
            }
        }

        private static string Find()
        {
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up
                        || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }
                    foreach (UnicastIPAddressInformation info in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (info.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            byte[] bytes = info.Address.GetAddressBytes();
                            // Skip link-local addresses (169.254.x.x): nobody else can reach them.
                            if (bytes[0] != 169 || bytes[1] != 254)
                            {
                                return info.Address.ToString();
                            }
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                // Some platforms do not let a game list its network adapters; there is simply no code to show.
            }
            return null;
        }
    }
}
