using System.Collections.Generic;

namespace Stockpile.PackageModels.Npm {
  public class Metadata {
    public Dictionary<string, Package> versions { get; set; }
  }
}