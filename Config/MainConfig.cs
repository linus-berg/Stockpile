using System.Collections.Generic;
using System.Linq;

namespace Stockpile.Config {
  public class MainConfig {
    public string delta_format { get; set; }
    public string db_path { get; set; }
    public bool staging { get; set; }
    public Dictionary<string, Dictionary<string, Filter>> filters { get; set; }
    public ChannelConfig[] channels { get; set; }

    public ChannelConfig GetChannelConfig(string channel_id) {
      return channels.FirstOrDefault(ch => ch.id == channel_id);
    }
  }
}