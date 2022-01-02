using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using LibGit2Sharp;

namespace Stockpile.Database {
  public class Artifact {
    public string Id { get; set; }
    public ArtifactStatus Status { get; set; }
    public List<ArtifactVersion> Versions { get; set; }

    public ArtifactVersion AddVersion(string version, string url) {
      ArtifactVersion a_v = new ArtifactVersion {
        Url = url,
        Version = version,
        Status = ArtifactVersionStatus.UNPROCESSED
      };
      Versions.Add(a_v);
      return a_v;
    }

    public void SetVersionAsProcessed(string version, string url) {
      ArtifactVersion av = GetVersion(version) ?? AddVersion(version, url);
      av.SetStatus(ArtifactVersionStatus.PROCESSED);
    }
    
    public bool IsVersionProcessed(string version) {
      ArtifactVersion a_v = GetVersion(version);
      return a_v != null && a_v.IsProcessed();
    }
    
    public ArtifactVersion GetVersion(string version) {
      return Versions.FirstOrDefault(v => v.Version == version);
    }
  }
}