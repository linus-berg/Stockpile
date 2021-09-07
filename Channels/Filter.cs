using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Stockpile.Channels {
  public class Filter {
    protected readonly Config.Main main_cfg_;
    protected readonly Config.Fetcher cfg_;

    /* List of found package ids */
    private Dictionary<string, Config.Filter> filters_;

    public Filter(Config.Main main_cfg, Config.Fetcher cfg) {

      main_cfg_ = main_cfg;
      cfg_ = cfg;
      filters_ = new();
      LoadFilters();
    }

    protected void LoadFilters() {
      if (cfg_.filters == null) {
        return;
      }
      foreach (var group_id in cfg_.filters) {
        var filter_group = main_cfg_.filters[group_id];
        /* Add all active filter groups. */
        foreach (var filter in filter_group) {
          filters_[filter.Key] = filter.Value;
        }
      }
    }

    public bool Exec(string id, string version, int downloads, string date) {
      /* Package specific filter */
      if (filters_.ContainsKey(id)) {
        if (Exec(filters_[id], id, version, downloads, date)) {
          return false;
        }
      }

      /* Global filters */
      if (filters_.ContainsKey("*")) {
        if (Exec(filters_["*"], id, version, downloads, date)) {
          return false;
        }
      }
      return true;
    }

    private static bool Exec(Config.Filter filter, string id, string version, int downloads, string date) {
      if (filter.version != null) {
        if (Regex.IsMatch(version, filter.version)) {
          return true;
        }
      }

      if (filter.min_downloads > 0 && downloads < filter.min_downloads) {
        return true;
      }

      if (filter.min_date != null) {
        var filter_date = DateTime.Parse(filter.min_date);
        var package_date = DateTime.Parse(date);
        if (filter_date > package_date) {
          return true;
        }
      }
      return false;
    }
  }
}
