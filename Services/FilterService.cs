using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Stockpile.Config;

namespace Stockpile.Services {
  public class FilterService {
    private readonly ChannelConfig cfg_;

    /* List of found package ids */
    private readonly Dictionary<string, ChannelFilter> filters_;
    private readonly MainConfig main_config_;

    public FilterService(MainConfig main_config, ChannelConfig cfg) {
      main_config_ = main_config;
      cfg_ = cfg;
      filters_ = new Dictionary<string, ChannelFilter>();
      LoadFilters();
    }

    private void LoadFilters() {
      if (cfg_.filters == null) return;
      foreach (string group_id in cfg_.filters) {
        Dictionary<string, ChannelFilter>
          filter_group = main_config_.filters[group_id];
        /* Add all active filter groups. */
        foreach (KeyValuePair<string, ChannelFilter> filter in filter_group)
          filters_[filter.Key] = filter.Value;
      }
    }

    public bool Exec(string id, string version, int downloads, string date) {
      /* Package specific filter */
      if (filters_.ContainsKey(id))
        if (Exec(filters_[id], id, version, downloads, date))
          return false;

      /* Global filters */
      if (filters_.ContainsKey("*"))
        if (Exec(filters_["*"], id, version, downloads, date))
          return false;
      return true;
    }

    private static bool Exec(ChannelFilter channel_filter, string id, string version,
      int downloads, string date) {
      if (channel_filter.version != null)
        if (Regex.IsMatch(version, channel_filter.version))
          return true;

      if (channel_filter.min_downloads > 0 && downloads < channel_filter.min_downloads)
        return true;

      if (channel_filter.min_date != null) {
        DateTime filter_date = DateTime.Parse(channel_filter.min_date);
        DateTime package_date = DateTime.Parse(date);
        if (filter_date > package_date) return true;
      }

      return false;
    }
  }
}