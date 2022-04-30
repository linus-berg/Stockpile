using System.Collections.Generic;

namespace Stockpile.Config {
  public class ChannelConfig {
    public string id { get; set; }
    public string type { get; set; }
    public bool force { get; set; }
    public ChannelThreads threads { get; set; }
    public string[] filters { get; set; }
    public string input { get; set; }
    public ChannelDeposit deposits { get; set; }
    public Dictionary<string, string> options { get; set; }
  }
}
