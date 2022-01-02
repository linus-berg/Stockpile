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

    public async Task<Artifact> AddArtifact(string id) {
      Artifact artifact = await GetArtifact(id);
      if (artifact != null) return artifact;
      artifact  = new() {
        Id = id,
        Status = ArtifactStatus.OK,
        Versions = new List<ArtifactVersion>()
      };
      await ctx_.Artifacts.AddAsync(artifact);
      await ctx_.SaveChangesAsync();
      return artifact;
    }

    public void SaveArtifact(Artifact artifact) { 
      ctx_.Artifacts.Update(artifact);
    }

    public async Task AddArtifactVersion(string id, string version, string url) {
      Artifact artifact = await GetArtifact(id);
      artifact.Versions.Add(new ArtifactVersion {
        Status = ArtifactVersionStatus.UNPROCESSED,
        Url = url,
        Version = version
      });
      await ctx_.SaveChangesAsync();
    }

    public async Task SetProcessed(string artifact_id, string version) {
      ArtifactVersion artifact_version = await GetArtifactVersion(artifact_id, version);
      if (artifact_version != null) {
        artifact_version.Status = ArtifactVersionStatus.PROCESSED;
        await ctx_.SaveChangesAsync();
      }
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

    public async Task<ArtifactVersion> GetArtifactVersion(string artifact_id,
      string version) {
      return await ctx_.ArtifactVersions.Where(av =>
          av.ArtifactId == artifact_id && av.Version == version)
        .FirstOrDefaultAsync();
    }
    
    public async Task<Artifact> GetArtifact(string id) {
      return await ctx_.Artifacts.Where(a => a.Id == id)
        .Include(a => a.Versions).FirstOrDefaultAsync();
    }
  }
}