namespace Stockpile.Config {
  public class Fetcher {
    public string id { get; set; }
    public string type { get; set; }
    public Threading threading { get; set; }
    public string[] filters { get; set; }
    public string input { get; set; }
    public Output output { get; set; }
  }

  public class Output {
    public string full { get; set; }
    public string delta { get; set; }
  }

  public class Threading {
    public int parallel_pkg { get; set; }
    public int parallel_ver { get; set; }
  }
}
