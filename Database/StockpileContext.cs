using Microsoft.EntityFrameworkCore;

namespace Stockpile.Database {
  public class StockpileContext : DbContext {
    private readonly string db_path_;

    public StockpileContext(string path) {
      db_path_ = path;
    }

    public DbSet<Artifact> Artifacts { get; set; }
    public DbSet<ArtifactVersion> ArtifactVersions { get; set; }

    protected override void OnModelCreating(ModelBuilder model_builder) {
      model_builder.Entity<ArtifactVersion>()
        .HasKey(a => new {a.ArtifactId, a.Version});
      model_builder.Entity<Artifact>()
        .HasIndex(a => a.Name);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options) {
      options.UseSqlite($"Data Source={db_path_}");
    }
  }
}