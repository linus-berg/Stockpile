using System.Collections.Generic;
using System.Linq;

namespace Stockpile.Config {
  public class Main {
    public string delta_format { get; set; }
    public string db_path { get; set; }
    public bool staging { get; set; }
    public Dictionary<string, Dictionary<string, Filter>> filters { get; set; }
    public Fetcher[] fetchers { get; set; }

    public Fetcher GetChannelConfig(string channel_id) {
      return fetchers.FirstOrDefault(ch => ch.id == channel_id);
    }
  }
}