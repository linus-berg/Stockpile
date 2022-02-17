using CommandLine;

namespace Stockpile.Parameters {
  public class CommonOptions {
    [Option('c', "config", Required = false, Default = "./config.json")]
    public string config { get; set; }
  }
}