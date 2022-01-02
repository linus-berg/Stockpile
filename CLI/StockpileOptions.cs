﻿using CommandLine;

namespace Stockpile.CLI {
  [Verb("stockpile", true, HelpText = "Run the stockpiler")]
  public class StockpileOptions : CommonOptions {
    [Option('s', "staging", Required = false,
      HelpText = "Write packages to delta folder")]
    public bool staging { get; set; }
  }
}