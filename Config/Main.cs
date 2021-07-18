using System.Collections.Generic;

namespace Stockpile.Config {
  public class Main {
    public string db_path { get; set; }
    public bool staging { get; set; }
    public Dictionary<string, Dictionary<string, Filter>> filters { get; set; }
    public Fetcher[] fetchers { get; set; }
  }
}


