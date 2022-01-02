using System.Collections.Generic;

namespace Stockpile.PackageModels.Npm {
  public class Package {
    public Dictionary<string, string> dependencies { get; set; }
    public Dictionary<string, string> peerDependencies { get; set; }
    public Dictionary<string, string> devDependencies { get; set; }
    public Dist dist { get; set; }
  }
}