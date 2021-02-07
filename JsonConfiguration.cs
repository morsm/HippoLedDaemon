using System;


namespace Termors.Serivces.HippotronicsLedDaemon
{
    /// <summary>
    /// Configuration object for HippoLedDeamon
    /// </summary>
    public class JsonConfiguration
    {
        public ushort Port { get; set; } = 9000;
        public ushort KeepaliveIntervalMinutes { get; set; } = 60;
        public ushort KeepaliveGrace { get; set; } = 3;
        public string LocalNetwork { get; set; } = "192.168.0.0/16";

    }
}
