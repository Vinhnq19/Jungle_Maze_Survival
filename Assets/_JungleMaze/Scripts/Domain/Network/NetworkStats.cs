namespace JungleMaze.Domain.Network
{
    /// <summary>Snapshot số đo mạng cho overlay/log. Hai nguồn RTT: Ping/Pong của protocol và RTT của LiteNetLib.</summary>
    public struct NetworkStats
    {
        public double LastRttMs;
        public double AverageRttMs;
        public double P95RttMs;
        public int PeerRttMs;
        public int PingsSent;
        public int PongsReceived;
        public int PongsLost;
        public long PacketsSent;
        public long PacketsReceived;
        public long BytesSent;
        public long BytesReceived;
        public int InvalidPackets;
    }
}
