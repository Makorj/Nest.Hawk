using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hawk.Database;
using SQLite;

namespace Hawk
{
    class DatabaseManager
    {
        public static DatabaseManager Instance { get; private set; }

        private readonly string databaseFile = @"testDatabase.db";

        private SQLiteConnection db;

        private DatabaseManager()
        {
            Instance = this;

            db = new SQLiteConnection(databaseFile);

            db.CreateTable<User>();
            db.CreateTable<QueleaWorker>();
        }

        public static DatabaseManager GetDatabaseManager()
        {
            if(Instance == null)
            {
                Instance = new DatabaseManager();
            }
            return Instance;
        }

        internal bool VerifyUser(string v1, string v2)
        {
            var queryResult = db.Table<User>().Where(x => x.Login == v1 && x.Password == v2);
            if(queryResult.Count() > 0)
            {
                return true;
            }
            return false;
        }

        internal void Add<T>(T toAdd)
        {
            int value = db.Insert(toAdd);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[DATABASE] Added {value} new row(s) to {typeof(T)} table");
            Console.ResetColor();
        }

        internal void Remove<T>(T toRemove)
        {
            int value = db.Delete(toRemove);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[DATABASE] Removed {value} new row(s) to {typeof(T)} table");
            Console.ResetColor();
        }

        internal List<User> GetAllUser()
        {
            return db.Table<User>().ToList();
        }
    }
}
