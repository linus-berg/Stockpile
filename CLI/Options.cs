using System;
using CommandLine;

namespace Stockpile.CLI {
  public class Options {
    [Option('c', "config", Required = false, Default = "./config.json")]
    public string config { get; set; }
    [Option('s', "staging", Required = false, HelpText = "Write packages to delta folder")]
    public bool staging { get; set; }
  }
}
