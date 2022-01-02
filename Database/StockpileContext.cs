using Microsoft.EntityFrameworkCore;

namespace Stockpile.Database {
  public class StockpileContext : DbContext {
    private readonly string db_path_;

    public StockpileContext(string path) {
      db_path_ = path;
    }

    public DbSet<Artifact> Artifacts { get; set; }
    public DbSet<ArtifactVersion> ArtifactVersions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
      modelBuilder.Entity<ArtifactVersion>()
        .HasKey(a => new {a.ArtifactId, a.Version});
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options) {
      options.UseSqlite($"Data Source={db_path_}");
    }
  }
}