using System.Collections.Generic;
using System.Linq;

namespace Stockpile.Database {
  public class Artifact {
    public int Id { get; set; }
    public string Name { get; set; }
    public ArtifactStatus Status { get; set; }
    public List<ArtifactVersion> Versions { get; set; }

    public ArtifactVersion AddVersionIfNotExists(string version, string url) {
      if (HasVersion(version)) return GetVersion(version);
      ArtifactVersion a_v = new() {
        Url = url,
        Version = version,
        Status = ArtifactVersionStatus.UNPROCESSED
      };
      Versions.Add(a_v);
      return a_v;
    }

    public void SetVersionToProcessed(string version) {
      ArtifactVersion av = GetVersion(version);
      av.SetStatus(ArtifactVersionStatus.PROCESSED);
    }

    private bool HasVersion(string version) {
      return Versions.Exists(v => v.Version == version);
    }

    private ArtifactVersion GetVersion(string version) {
      return Versions.FirstOrDefault(v => v.Version == version);
    }
  }
}