using SQLite;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Hawk.Database
{
    [Table("User")]
    class User
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        [Unique]
        public string Login { get; set; }
        public string Password { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return String.Format("{0,5} {1,20} {2,50} {3,20}", Id, Login, Password, Name);
        }
    }
}
