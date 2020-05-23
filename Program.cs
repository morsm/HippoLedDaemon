using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Web.Http;
using System.Net.Http.Formatting;

using Microsoft.Owin.Hosting;
using Owin;

using Termors.Nuget.MDNSServiceDirectory;


namespace Termors.Serivces.HippotronicsLedDaemon
{
    class Daemon
    {
        public static readonly string HIPPO_HOST_ID = "HippoLed-";
        public static readonly string HIPPO_SVC_ID = "._hippohttp";

        private ServiceDirectory _serviceDirectory = new ServiceDirectory(filterfunc: FilterHippoServices);
        private ManualResetEvent _endEvent = new ManualResetEvent(false);


        public static async Task Main(string[] args)
        {
            await new Daemon().Run(args);
        }

        public async Task Run(string[] args)
        { 
            Logger.Log("HippotronicsLedDaemon started");

            // Set up REST services in OWIN web server
            var webapp = WebApp.Start("http://*:9000/", new Action<IAppBuilder>(Configuration));

            Console.CancelKeyPress += (sender, e) =>
            {
                Logger.Log("HippotronicsLedDaemon stopped");
                webapp.Dispose();
            };

            // Set up ServiceDirectory to look for MDNS lamps on the network
            _serviceDirectory.KeepaliveCheckInterval = 60;
            _serviceDirectory.KeepalivePing = _serviceDirectory.KeepaliveTcp = true;

            _serviceDirectory.HostDiscovered += async (dir, entry) => { await HostDiscovered(dir, entry); };
            _serviceDirectory.HostUpdated += async (dir, entry) => { await HostUpdated(dir, entry); };
            _serviceDirectory.HostRemoved += HostRemoved;

            _serviceDirectory.Init();

            Logger.Log("HippotronicsLedDaemon running");

            // Schedule purge of records that have not been updated
            await ScheduleNextUpdate();
            

            // Run until Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                _endEvent.Set();
            };

            // Start watchdog
            Watchdog.Dog.ScheduleDog();

            // Wait for normal termination
            _endEvent.WaitOne();

            Logger.Log("HippotronicsLedDaemon ending");
            Environment.Exit(0);        // Normal exit
        }

        private static bool FilterHippoServices(string arg)
        {
            return arg.ToLowerInvariant().Contains("hippohttp");
        }

        private async Task ScheduleNextUpdate()
        {
            await Task.Run(async () => await UpdateLampDatabase());
        }

        private async Task UpdateLampDatabase()
        {
            // Wait one minute before updating the database
            bool quit = _endEvent.WaitOne(60000);
            if (quit) return;

            // Trigger Watchdog so it doesn't kick us out.
            // If this loop hangs, the watchdog will exit the process
            // after five minutes, so that systemd (or similar)
            // can restart it
            Watchdog.Dog.Wake();
            Logger.Log("Updating lamp database");

            // Update the status for all the lamps that are still in there
            using (var client = new DatabaseClient())
            {
                var lamps = client.GetAll();

                var tasks = new List<Task>();
                foreach (var lamp in lamps)
                {
                    tasks.Add(UpdateSingleLamp(client, lamp));
                }
                Task.WaitAll(tasks.ToArray());

                // Remove old records
                client.PurgeExpired();

                // Log current lamp count
                int lampCount = 0;
                var entries = client.GetAll().GetEnumerator();
                while (entries.MoveNext()) ++lampCount;
                Logger.Log("Lamp database updated, {0} entries", lampCount);
            }


            if (!quit)
            {
                await ScheduleNextUpdate();
            }
        }

        private async Task UpdateSingleLamp(DatabaseClient db, LampNode lamp)
        {
            var lampClient = new LampClient(lamp);

            try
            {
                await lampClient.GetState();

                db.AddOrUpdate(lampClient.Node);
            }
            catch
            {
                // Error is ok; if the lamp is not seen, it will be removed
            }
        }

        private void HostRemoved(ServiceDirectory directory, ServiceEntry entry)
        {
            using (var client = new DatabaseClient())
                client.RemoveByName(ServiceEntryToName(entry));
            Logger.Log("Host removed: {0}", entry.ToShortString());
        }

        private async Task HostDiscovered(ServiceDirectory directory, ServiceEntry entry)
        {
            Logger.Log("Host discovered: {0}", entry.ToShortString());

            await HostDiscoveredOrUpdated(directory, entry);
        }

        private async Task HostUpdated(ServiceDirectory directory, ServiceEntry entry)
        {
            await HostDiscoveredOrUpdated(directory, entry);
        }


        private async Task HostDiscoveredOrUpdated(ServiceDirectory directory, ServiceEntry entry)
        {
            LampClient newClient = new LampClient(ServiceEntryToUrl(entry), ServiceEntryToName(entry));

            await GetLampStatus(newClient);
            UpdateDb(newClient);
        }

        private static string ServiceEntryToName(ServiceEntry entry)
        {
            int idxTrailer = entry.Service.IndexOf(HIPPO_SVC_ID);
            int lenHeader = HIPPO_HOST_ID.Length;
            string name = entry.Service.Substring(lenHeader, idxTrailer - lenHeader);

            return name;
        }

        private static string ServiceEntryToUrl(ServiceEntry entry)
        {
            StringBuilder sb = new StringBuilder("http://");

            if (entry.IPAddresses.Count > 0) sb.Append(entry.IPAddresses[0]); else sb.Append(entry.Host);
            sb.Append(":").Append(entry.Port);

            return sb.ToString();
        }

        // This code configures Web API using Owin
        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            HttpConfiguration config = new HttpConfiguration();

            // Format to JSON by default
            config.Formatters.Clear();
            config.Formatters.Add(new JsonMediaTypeFormatter());

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.EnsureInitialized();

            appBuilder.UseWebApi(config);
        }

        protected void UpdateDb(LampClient lamp)
        {
            // Add new client to database
            if (lamp.StateSet)
            {
                using (var client = new DatabaseClient())
                    client.AddOrUpdate(lamp.Node);
            }
        }

        protected async Task GetLampStatus(LampClient lamp)
        {
            // Assume the lamp is online and get its state
            lamp.Online = true;

            try
            {
                await lamp.GetState();
            }
            catch (Exception)
            {
                lamp.Online = false;
            }
        }
    }
}
