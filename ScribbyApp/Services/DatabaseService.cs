using ScribbyApp.Models;
using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScribbyApp.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;

        public DatabaseService()
        {
            // The path to the database file will be in a platform-specific secure location.
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "UserScripts.db3");
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<UserScript>().Wait();
        }

        public Task<List<UserScript>> GetScriptsAsync()
        {
            return _database.Table<UserScript>().ToListAsync();
        }

        public Task<int> SaveScriptAsync(UserScript script)
        {
            if (script.ID != 0)
            {
                return _database.UpdateAsync(script);
            }
            else
            {
                return _database.InsertAsync(script);
            }
        }

        public Task<int> DeleteScriptAsync(UserScript script)
        {
            return _database.DeleteAsync(script);
        }
    }
}