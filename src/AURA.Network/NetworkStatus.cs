namespace AURA.Network
{
    /// <summary>
    /// Describes the current local network / Internet connection state.
    /// </summary>
    public class NetworkStatus
    {
        public bool IsConnected { get; set; }

        public bool HasInternetAccess { get; set; }

        public string LocalIpAddress { get; set; }

        public int? LatencyMilliseconds { get; set; }

        public string Message { get; set; }
    }
}
