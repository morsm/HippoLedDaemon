using System;
using System.Reflection;


namespace Termors.Serivces.HippotronicsLedDaemon
{
    public class RuntimeStatistics
    {
        public RuntimeStatistics()
        {
        }

        public RuntimeStatistics Copy()
        {
            // Lazy code: return the negative of the negative of other, which is a copy
            var negOther = -this;
            return -negOther;
        }

        public long LampGet { get; set; }
        public long LampSet { get; set; }
        public long LampsGetAll { get; set; }
        public long HostDiscovered { get; set; }
        public long HostUpdated { get; set; }
        public long HostRemoved { get; set; }
        public long RequestScheduled { get; set; }
        public long RequestServed { get; set; }
        public long UpdateDb { get; set; }
        public long GetLampStatus { get; set; }
        public long UpdateSingleLamp { get; set; }

        public override string ToString()
        {
            return String.Format("Http Get Lamp: {0}, Http Set Lamp: {1}, Http Get All: {2}, " +
                "HostDiscovered: {3}, HostUpdated: {4}, HostRemoved: {5}, " +
                "RequestScheduled: {6}, RequestServed: {7}, " +
                "UpdateDb: {8}, GetLampStatus: {9}, UpdateSingleLamp: {10}",
                LampGet ,
                LampSet ,
                LampsGetAll ,
                HostDiscovered ,
                HostUpdated ,
                HostRemoved ,
                RequestScheduled ,
                RequestServed,
                UpdateDb,
                GetLampStatus,
                UpdateSingleLamp
                );
        }

        public static RuntimeStatistics operator-(RuntimeStatistics s)
        {
            return new RuntimeStatistics
            {
                LampGet = -s.LampGet,
                LampSet = -s.LampSet,
                LampsGetAll = -s.LampsGetAll,
                HostDiscovered = -s.HostDiscovered,
                HostUpdated = -s.HostUpdated,
                HostRemoved = -s.HostRemoved,
                RequestScheduled = -s.RequestScheduled,
                RequestServed = -s.RequestServed,
                UpdateDb = -s.UpdateDb,
                GetLampStatus = -s.GetLampStatus,
                UpdateSingleLamp = -s.UpdateSingleLamp
            };
        }

        public static RuntimeStatistics operator-(RuntimeStatistics a, RuntimeStatistics b)
        {
            return a + (-b);
        }

        public static RuntimeStatistics operator+(RuntimeStatistics a, RuntimeStatistics b)
        {
            return new RuntimeStatistics
            {
                LampGet = a.LampGet + b.LampGet,
                LampSet = a.LampSet + b.LampSet,
                LampsGetAll = a.LampsGetAll + b.LampsGetAll,
                HostDiscovered = a.HostDiscovered + b.HostDiscovered,
                HostUpdated = a.HostUpdated + b.HostUpdated,
                HostRemoved = a.HostRemoved + b.HostRemoved,
                RequestScheduled = a.RequestScheduled + b.RequestScheduled,
                RequestServed = a.RequestServed + b.RequestServed,
                UpdateDb = a.UpdateDb + b.UpdateDb,
                GetLampStatus = a.GetLampStatus + b.GetLampStatus,
                UpdateSingleLamp = a.UpdateSingleLamp + b.UpdateSingleLamp

            };
        }
    }


    public sealed class RuntimeMonitor
    {
        // Singleton that cannot be created outside
        private RuntimeMonitor()
        {
        }

        private static RuntimeMonitor _monitor = new RuntimeMonitor();
        public static RuntimeMonitor Monitor {  get { return _monitor; } }

        private RuntimeStatistics _statistics = new RuntimeStatistics();
        public RuntimeStatistics Statistics { get { return _statistics; } }

        public void RegisterCall(string propertyName)
        {
            var method = _statistics.GetType().GetProperty(propertyName);
            if (method == null)
            {
                Logger.LogError("Code error: invalid action {0} attempted to register in RuntimeMonitor, stacktrace {1}",
                    propertyName,
                    Environment.StackTrace
                    );
                return;
            }

            // Synchronize access to the statistics object and increase the value of the property
            lock (_statistics)
            {
                long currentValue = (long)method.GetValue(_statistics);
                method.SetValue(_statistics, ++currentValue);
            }
        }
    }
}
