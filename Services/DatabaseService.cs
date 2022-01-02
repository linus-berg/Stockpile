using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Stockpile.Database;

namespace Stockpile.Services {
  public class DatabaseService {
    private static string db_storage_;
    private readonly StockpileContext ctx_;

    private DatabaseService(string path) {
      ctx_ = new StockpileContext(path);
      ctx_.Database.EnsureCreated();
    }


    public static void SetDatabaseDirs(string db_storage) {
      db_storage_ = db_storage;
      Directory.CreateDirectory(db_storage_);
    }

    public static DatabaseService Open(string type) {
      string db_str = db_storage_ + type + ".sqlite";
      DatabaseService db = new(db_str);
      return db;
    }

    public async Task<Artifact> AddArtifact(string name) {
      Artifact artifact = await GetArtifactByName(name);
      if (artifact != null) return artifact;
      artifact = new Artifact {
        Name = name,
        Status = ArtifactStatus.UNPROCESSED,
        Versions = new List<ArtifactVersion>()
      };
      await ctx_.Artifacts.AddAsync(artifact);
      await ctx_.SaveChangesAsync();
      return artifact;
    }

    public async Task SaveArtifact(Artifact artifact) {
      ctx_.Artifacts.Update(artifact);
      await ctx_.SaveChangesAsync();
    }

    public async Task<int> GetArtifactCount() {
      return await ctx_.Artifacts.CountAsync();
    }

    public async Task<int> GetArtifactVersionCount() {
      return await ctx_.ArtifactVersions.CountAsync();
    }

    public async Task<IEnumerable<Artifact>> GetArtifacts() {
      return await ctx_.Artifacts.Include(a => a.Versions).ToListAsync();
    }

    public async Task<IEnumerable<Artifact>> GetUnprocessedArtifacts() {
      return await ctx_.Artifacts
        .Where(a => a.Status == ArtifactStatus.UNPROCESSED)
        .Include(a => a.Versions).ToListAsync();
    }

    public async Task<Artifact> GetArtifactByName(string name) {
      return await ctx_.Artifacts.Where(a => a.Name == name)
        .Include(a => a.Versions).FirstOrDefaultAsync();
    }
  }
}