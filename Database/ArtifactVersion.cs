namespace Stockpile.Database {
  public class ArtifactVersion {
    public string Version { get; set; }
    public string Url { get; set; }
    public int ArtifactId { get; set; }
    public Artifact Artifact { get; set; }
    public ArtifactVersionStatus Status { get; set; }

    public void SetStatus(ArtifactVersionStatus status) {
      Status = status;
    }

    public bool ShouldProcess() {
      return !IsProcessed();
    }

    public bool IsProcessed() {
      return Status is ArtifactVersionStatus.PROCESSED
        or ArtifactVersionStatus.BLACKLISTED;
    }
  }
}