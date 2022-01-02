
namespace Stockpile.Database {
  public class ArtifactVersion {
    public string Version { get; set; }
    public string Url { get; set; }
    public string ArtifactId { get; set; }
    public Artifact Artifact { get; set; }
    public ArtifactVersionStatus Status { get; set; }

    public void SetStatus(ArtifactVersionStatus status) {
      this.Status = status;
    }
    
    public bool IsProcessed() {
      return this.Status == ArtifactVersionStatus.PROCESSED;
    }
  }
}