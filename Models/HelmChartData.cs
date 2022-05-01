namespace Stockpile.Models {
  public class HelmChartData {
    public string version {get; set;}
    public bool prerelease {get; set;}
    public HelmChartDependency[] dependencies {get; set;}
  }
}
