using Microsoft.Data.Sqlite;
using Dapper;
using Dapper.Contrib.Extensions;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Stockpile {
  [Table("packages")]
  public class DBPackage {
    [ExplicitKey]
    public string id {get; set;}
    public string version {get; set;}
    public string url {get; set;}
    public int processed {get; set;}
    public bool IsProcessed() => processed > 0;
  }
  
  
  public class Database {
    private static string db_storage_;
    private readonly string db_path_;
    private readonly SqliteConnection db_;
    
    public static void SetDatabaseDir(string db_storage) {
      db_storage_ = db_storage;
      Directory.CreateDirectory(db_storage_);
    }

    public static Database Open(string type) {
      string db_str = db_storage_ + type + ".sqlite";
      bool exists = File.Exists(db_str);
      Database db = new Database(db_str);
      if (!exists) {
        db.Init();
      }
      return db;
    }

    private Database(string path) {
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
    
    ~Database() {
      db_.Close();
    }

    private void Init() {
      string init_sql_path = db_storage_ + "create_db.sql";
      if (!File.Exists(init_sql_path)) {
        throw new FileNotFoundException("create_db.sql");
      }
      this.db_.Query(File.ReadAllText(init_sql_path));
    }

    public void AddPackage(string id, string version, string url) {
      DBPackage pkg = new DBPackage {
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
      IEnumerable<string> packages = db_.Query<string>("SELECT id FROM packages WHERE processed=1 GROUP BY id");
      return packages;

    }
    public int GetPackageCount() {
      return db_.Query<int>("SELECT COUNT(DISTINCT id) FROM packages").FirstOrDefault();
    }
    
    public int GetVersionCount() {
      return db_.Query<int>("SELECT COUNT(*) FROM packages").FirstOrDefault();
    }

    public IEnumerable<DBPackage> GetAllToDownload(string id) {
      IEnumerable<DBPackage> packages = db_.Query<DBPackage>("SELECT * FROM packages WHERE id=@id AND processed=1", new { id });
      return packages;
    }

    public DBPackage GetPackage(string id, string version) {
      IEnumerable<DBPackage> package = db_.Query<DBPackage>("SELECT * FROM packages WHERE id=@id AND version=@version", new { id, version});
      return package.FirstOrDefault();
    }

  }
}
