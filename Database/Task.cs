using System;
using System.Collections.Generic;
using System.Text;
using SQLite;
using Jackdaw;

namespace Hawk.Database
{
    [Table("Task")]
    class Task
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public RequestType RequestType { get; set; }
        public RequestPlatform RequestPlatform { get; set; }
        public PublishPatform PublishPatform { get; set; }
        public TestType TestType { get; set; }

        [NotNull]
        public int OwnerID { get; set; }
        [NotNull]
        public int WorkerID { get; set; }
    }
}
