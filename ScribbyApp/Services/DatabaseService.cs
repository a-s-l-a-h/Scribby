
using ScribbyApp.Models;
using SQLite;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

// *** CORRECTED NAMESPACE ***
namespace ScribbyApp.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _database;

        public DatabaseService()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "UserScripts.db3");
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<UserScript>().Wait();
        }

        public Task<List<UserScript>> GetScriptsAsync() => _database.Table<UserScript>().ToListAsync();

        public Task<int> SaveScriptAsync(UserScript script) =>
            script.ID != 0 ? _database.UpdateAsync(script) : _database.InsertAsync(script);

        public Task<int> DeleteScriptAsync(UserScript script) => _database.DeleteAsync(script);
    }
}