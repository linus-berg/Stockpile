using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Data.Sqlite;

namespace Stockpile.Services {
  [Table("packages")]
  public class DBPackage {
    [ExplicitKey]
    public string id { get; set; }
    public string version { get; set; }
    public string url { get; set; }
    public int processed { get; set; }
    public bool IsProcessed() => processed > 0;
  }


  public class DatabaseService {
    private static string db_storage_;
    private readonly string db_path_;
    private readonly SqliteConnection db_;

    public static void SetDatabaseDirs(string db_storage) {
      db_storage_ = db_storage;
      Directory.CreateDirectory(db_storage_);
    }

    public static DatabaseService Open(string type) {
      var db_str = db_storage_ + type + ".sqlite";
      var exists = File.Exists(db_str);
      var db = new DatabaseService(db_str);
      if (!exists) {
        db.Init();
      }
      return db;
    }

    private DatabaseService(string path) {
      db_path_ = path;
      db_ = new SqliteConnection($"Data Source={path}");
      db_.Open();
      using (var command = db_.CreateCommand()) {
        command.CommandText = @"
          PRAGMA journal_mode = WAL;
          PRAGMA synchronous = normal;
          PRAGMA temp_store = memory;
        ";
        command.ExecuteNonQuery();
      }
    }

    ~DatabaseService() {
      db_.Close();
    }
    
    /* init database */
    private const string SQL_INIT_DB = @"
      CREATE TABLE 'packages' (
        'id'	TEXT NOT NULL,
        'version'	TEXT NOT NULL,
        'url'	TEXT NOT NULL,
        'processed'	INTEGER NOT NULL,
        PRIMARY KEY('version', 'id')
      )
    ";
    private void Init() {
      db_.Query(SQL_INIT_DB);
    }

    public void AddPackage(string id, string version, string url) {
      var pkg = new DBPackage {
        id = id,
        version = version,
        url = url,
        processed = 0
      };
      db_.Insert(pkg);
    }

    public void SetProcessed(string id, string version) {
      db_.Query<DBPackage>("UPDATE packages SET processed=1 WHERE id=@id AND version=@version",
          new {
            id,
            version
          });
    }
    public IEnumerable<string> GetAllPackages() {
      var packages = db_.Query<string>("SELECT id FROM packages WHERE processed=1 GROUP BY id");
      return packages;

    }
    public int GetPackageCount() {
      return db_.Query<int>("SELECT COUNT(DISTINCT id) FROM packages").FirstOrDefault();
    }

    public int GetVersionCount() {
      return db_.Query<int>("SELECT COUNT(*) FROM packages").FirstOrDefault();
    }

    public IEnumerable<DBPackage> GetAllToDownload(string id) {
      lock (db_) {
        var packages = db_.Query<DBPackage>("SELECT * FROM packages WHERE id=@id AND processed=1", new { id });
        return packages;
      }
    }

    public DBPackage GetPackage(string id, string version) {
      var package = db_.Query<DBPackage>("SELECT * FROM packages WHERE id=@id AND version=@version", new { id, version });
      return package.FirstOrDefault();
    }

  }
}
