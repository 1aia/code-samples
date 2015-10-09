using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NHibernate;

namespace QoSme.Core.DataAccess.SQLite.Impl
{
    public class SQLitePerOrderProvider : ISQLitePerOrderProvider
    {
        public Config Config { get; set; }

        class SessionData
        {
            public ISessionFactory SessionFactory { get; set; }

            public DateTime LastUsed { get; set; }
        }

        private readonly Dictionary<long, SessionData> _orderSessionsDict = new Dictionary<long, SessionData>();
        
        public ISessionFactory GetSessionFactory(long orderId)
        {
            lock (_orderSessionsDict)
            {
                ISessionFactory sessionFactory = null;

                if (_orderSessionsDict.ContainsKey(orderId))
                {
                    var sessiondata = _orderSessionsDict[orderId];

                    sessiondata.LastUsed = DateTime.Now;

                    sessionFactory = sessiondata.SessionFactory;
                }

                if (sessionFactory == null)
                {
                    sessionFactory = BuildSessionFactory(orderId);

                    _orderSessionsDict[orderId] = new SessionData
                    {
                        LastUsed = DateTime.Now,
                        SessionFactory = sessionFactory
                    };
                }

                var sessionFactoriesToDelete = _orderSessionsDict.Where(x => x.Value.LastUsed < DateTime.Now.AddHours(-1)).Select(x => x.Key).ToList();

                sessionFactoriesToDelete.ForEach(x => _orderSessionsDict.Remove(x));

                return sessionFactory;
            }
        }

        private ISessionFactory BuildSessionFactory(long orderId)
        {
            var reportsDir = Config.ReportsDirectory;

            var databaseDir = string.Format("{0}/{1}", reportsDir, orderId);

            if (!Directory.Exists(databaseDir))
            {
                Directory.CreateDirectory(databaseDir);
            }

            var dataBaseFile = string.Format("{0}/db", databaseDir);

            var connectionString = string.Format("Data Source={0};FailIfMissing=false;Version=3", dataBaseFile);

            var sessionFactory = new NHibernateSQLiteConfiguration(connectionString).BuildSessionFactory();

            return sessionFactory;
        }
    }
}