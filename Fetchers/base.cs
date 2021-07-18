using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using ShellProgressBar;

namespace Stockpile.Fetchers {
  public abstract class BaseFetcher {
    protected readonly Config.Main main_cfg_;
    protected readonly Config.Fetcher cfg_;
    protected readonly bool seeding_;
    protected readonly ParallelOptions po_;
    private int max_depth_ = 0;
    private int depth_ = 0;
    protected int Depth {
      get => depth_;
      set {
        if (value > max_depth_) {
          max_depth_ = value;
        }
        depth_ = value;
      }
    }
    private int packages_ = 0;
    private int versions_ = 0;

    protected static readonly ProgressBarOptions bar_opts_ = new ProgressBarOptions {
      CollapseWhenFinished = true,
      ProgressCharacter = '─'
    };

    public void Start() {
      foreach (var line in package_list_) {
        Get(line);
        main_bar_.Tick();
      }
      ProcessIds();
    }

    protected static readonly ProgressBar main_bar_ = new ProgressBar(0, "Stockpiling...", bar_opts_);
    protected readonly ChildProgressBar bar_;

    /* List of found package ids */
    private HashSet<string> found_;
    private HashSet<string> error_;
    private Dictionary<string, Config.Filter> filters_;
    protected ILogger logger_ = NullLogger.Instance;
    protected CancellationToken ct_ = CancellationToken.None;
    protected readonly string[] package_list_;
    protected readonly int package_count_;
    protected readonly Database db_;

    protected BaseFetcher(Config.Main main_cfg, Config.Fetcher cfg) {
      package_list_ = File.ReadAllLines(cfg.input);
      package_count_ = package_list_.Length;
      main_bar_.MaxTicks = main_bar_.MaxTicks + package_count_;

      main_cfg_ = main_cfg;
      cfg_ = cfg;
      po_ = new ParallelOptions {
        MaxDegreeOfParallelism = cfg.threading.parallel_pkg
      };
      seeding_ = main_cfg_.staging;
      filters_ = new();
      found_ = new();
      error_ = new();
      db_ = Database.Open(cfg.id);
      bar_ = main_bar_.Spawn(0, cfg.id, bar_opts_);
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

    protected void AddToVersionCount(int c) {
      versions_ += c;
    }

    protected void SetVersionCount(int c) {
      versions_ = c;
    }

    protected void SetPackageCount(int c) {
      main_bar_.MaxTicks = main_bar_.MaxTicks + c;
      packages_ = c;
    }

    private static string GetFilePath(string dir, string filename) {
      return Path.Combine(Path.GetFullPath(dir), filename);
    }

    ~BaseFetcher() {
      main_bar_.WriteLine($"{cfg_.id} done.");
      bar_.Dispose();
    }

    protected bool ExecFilters(string id, string version, int downloads, string date) {
      /* Package specific filter */
      if (filters_.ContainsKey(id)) {
        if (ExecFilter(filters_[id], id, version, downloads, date)) {
          return false;
        }
      }

      /* Global filters */
      if (filters_.ContainsKey("*")) {
        if (ExecFilter(filters_["*"], id, version, downloads, date)) {
          return false;
        }
      }
      return true;
    }

    private static bool ExecFilter(Config.Filter filter, string id, string version, int downloads, string date) {
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


    protected static void CreateFilePath(string file_path) {
      Directory.CreateDirectory(Path.GetDirectoryName(file_path));
    }

    protected string GetOutFilePath(string filename) {
      return GetFilePath(cfg_.output.full, filename);
    }

    protected string GetDeltaFilePath(string filepath) {
      return GetFilePath(cfg_.output.delta, filepath);
    }

    protected void CopyToDelta(string fp) {
      var out_fp = GetOutFilePath(fp);
      if (!seeding_) {
        var delta_fp = GetDeltaFilePath(fp);
        CreateFilePath(delta_fp);
        File.Copy(out_fp, delta_fp);
      }
    }

    protected bool InMemory(string id) {
      return found_.Contains(id);
    }

    protected void Memorize(string id) {
      packages_++;
      found_.Add(id);
    }

    protected void SetError(string id) {
      error_.Add(id);
    }

    protected bool IsValid(string id) {
      return !error_.Contains(id);
    }

    protected HashSet<string> GetMemory() {
      return found_;
    }

    protected static bool OnDisk(string path) {
      return File.Exists(path);
    }

    protected ChildProgressBar GetBar(int c) {
      return main_bar_.Spawn(c, cfg_.id, bar_opts_);
    }

    private string AddWrapperText(string text) {
      var prefix = "";
      prefix += $"{cfg_.id,-6}";
      prefix += $"Packages[{packages_}] Versions[{versions_}] ";
      prefix += $"Depth[{depth_}/{max_depth_}] {text}";
      return prefix;
    }

    protected void SetText(string text, bool wrap = true) {
      bar_.Message = wrap ? AddWrapperText(text) : $"{cfg_.id,-6} {text}";
    }

    public abstract void Get(string id);
    public abstract void ProcessIds();
  }
}
