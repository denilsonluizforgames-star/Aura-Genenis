using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AURA.Network
{
    /// <summary>
    /// Checks whether the machine is on a local network and has Internet
    /// access (via a ping to a public IP).
    /// </summary>
    public sealed class NetworkManager
    {
        private const string PingTarget = "8.8.8.8";

        private const int TimeoutMilliseconds = 3000;

        public NetworkStatus CheckConnection()
        {
            var status = new NetworkStatus
            {
                IsConnected = NetworkInterface.GetIsNetworkAvailable(),
                LocalIpAddress = GetLocalIpAddress()
            };

            int? latency = PingOnce();
            status.LatencyMilliseconds = latency;
            status.HasInternetAccess = latency.HasValue;
            status.Message = latency.HasValue
                ? "Conexão ativa."
                : "Sem acesso à Internet (ping falhou).";

            return status;
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                IPAddress address = Dns.GetHostAddresses(Dns.GetHostName())
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

                return address == null ? "-" : address.ToString();
            }
            catch
            {
                return "-";
            }
        }

        private static int? PingOnce()
        {
            try
            {
                using (var ping = new Ping())
                {
                    PingReply reply = ping.Send(PingTarget, TimeoutMilliseconds);
                    return reply.Status == IPStatus.Success ? (int?)reply.RoundtripTime : null;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
