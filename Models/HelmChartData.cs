using System.Collections.Generic;
namespace Stockpile.Models {
  public class HelmChartData {
    public string version {get; set;}
    public bool prerelease {get; set;}
    public IEnumerable<HelmChartDependency> dependencies {get; set;}
  }
}
