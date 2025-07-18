
using SQLite;

namespace ScribbyApp.Models
{
    public class UserScript
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Icon { get; set; }
    }
}