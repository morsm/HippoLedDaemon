using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;


using LiteDB;

namespace Termors.Serivces.HippotronicsLedDaemon
{

    public class DatabaseClient : IDisposable
    {
        public static readonly string LAMPS_TABLE = "lamps";
        public static ulong DEFAULT_PURGE_TIMEOUT = 5;     // 5 minutes

        public static ulong PurgeTimeout { get; set; } = DEFAULT_PURGE_TIMEOUT;

        public static readonly string DATABASE_PATH;

        protected LiteDatabase Database { get; set; }
        public readonly object SyncRoot = new object();

        static DatabaseClient()
        {
            // Database name depends on version of this assembly
            string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            int lastPoint = assemblyVersion.LastIndexOf('.');
            assemblyVersion = assemblyVersion.Substring(0, lastPoint);

            // Open database
            DATABASE_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "hippoled" + assemblyVersion + ".db");
        }

        public DatabaseClient()
        {
            var connectionString = String.Format("Filename={0};Connection=shared", DATABASE_PATH);
            Database = new LiteDatabase(connectionString);

            var collection = Database.GetCollection<LampNode>(LAMPS_TABLE);
            collection.EnsureIndex("Name");
        }


        public void AddOrUpdate(LampNode node)
        {
            lock (SyncRoot)
            {
                var table = GetTable();

                // Try to insert record. If that fails with a duplicate key, then try update
                // Old code checked for existence first and then decided, but this was
                // race condition sensitive.
                try
                {
                    InsertRecord(table, node);
                }
                catch (LiteException lE)
                {
                    if (lE.ErrorCode == LiteException.INDEX_DUPLICATE_KEY)
                        UpdateRecord(table, node);
                    else
                        throw lE;       // Some other error
                }
            }
        }

        private void InsertRecord(ILiteCollection<LampNode> table, LampNode node)
        {
            table.Insert(node);
        }

        private void UpdateRecord(ILiteCollection<LampNode> table, LampNode node)
        {
            var currentLamp = table.FindOne(rec => rec.Name.ToLower() == node.Name.ToLower());
            if (currentLamp != null)
            {
                // Update
                currentLamp.Url = node.Url;
                currentLamp.On = node.On;
                currentLamp.Online = node.Online;
                currentLamp.LastSeen = node.LastSeen;
                currentLamp.Red = node.Red;
                currentLamp.Green = node.Green;
                currentLamp.Blue = node.Blue;
                currentLamp.NodeType = node.NodeType;

                table.Update(currentLamp);
            }
        }

        public IEnumerable<LampNode> GetAll()
        {
            lock (SyncRoot)
            {
                var table = GetTable();
                var list = new List<LampNode>(table.FindAll());

                return list;
            }
        }

        public LampNode GetOne(string id)
        {
            lock (SyncRoot)
            {
                var table = GetTable();
                var record = table.FindOne(x => x.Name.ToLower() == id.ToLower());

                return record;
            }
        }

        public void PurgeExpired()
        {
            lock (SyncRoot)
            {
                var table = GetTable();
                var oldest = DateTime.Now.Subtract(TimeSpan.FromMinutes(PurgeTimeout));

                table.DeleteMany(x => x.LastSeen < oldest);
            }
        }

        public void RemoveByName(string id)
        {
            lock (SyncRoot)
            {
                GetTable().DeleteMany(x => x.Name.ToLower() == id.ToLower());
            }
        }

        private ILiteCollection<LampNode> GetTable()
        {
            return Database.GetCollection<LampNode>(LAMPS_TABLE);
        }

        public void Dispose()
        {
            Database.Dispose();
        }
    }
}
