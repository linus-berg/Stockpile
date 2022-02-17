using System.Collections.Generic;

namespace Stockpile.Models.Npm {
  public class Metadata {
    public Dictionary<string, Package> versions { get; set; }
  }
}