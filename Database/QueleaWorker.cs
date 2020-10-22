using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hawk.Database
{
    [Table("QueleaWorker")]
    class QueleaWorker
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public bool IsBusy { get; set; }
        public int AssignedTaskID { get; set; }
    }
}
