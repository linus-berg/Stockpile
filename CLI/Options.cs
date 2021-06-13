using System;
using CommandLine;

namespace Stockpile.CLI {
  public class Options {
    [Option('s', "staging", Required=false, HelpText="Write packages to delta folder")]
    public bool staging {get; set;}
  }
}
