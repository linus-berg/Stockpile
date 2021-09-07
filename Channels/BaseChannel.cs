using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using ShellProgressBar;

namespace Stockpile.Channels {
  public abstract class BaseChannel {
    protected readonly Config.Main main_cfg_;
    protected readonly Config.Fetcher cfg_;
    protected readonly bool seeding_;
    protected readonly ParallelOptions po_;
    private int max_depth_ = 0;
    private int depth_ = 0;
    protected Memory memory_;
    protected Filter filter_;
    protected delegate string FileFormatter(DBPackage pkg);
    private FileFormatter file_fmt_;

    protected int Depth {
      get => depth_;
      set {
        if (value > max_depth_) {
          max_depth_ = value;
        }
        depth_ = value;
      }
    }
    private int versions_ = 0;

    protected static readonly ProgressBarOptions bar_opts_ = new ProgressBarOptions {
      CollapseWhenFinished = true,
      ProgressCharacter = '─'
    };


    protected static readonly IProgressBar main_bar_ = new ProgressBar(0, "Stockpiling...", bar_opts_);
    protected readonly IProgressBar bar_;

    /* List of found package ids */
    protected ILogger logger_ = NullLogger.Instance;
    protected CancellationToken ct_ = CancellationToken.None;
    protected readonly string[] package_list_;
    protected readonly int package_count_;
    protected readonly Database db_;

    protected BaseChannel(Config.Main main_cfg, Config.Fetcher cfg, FileFormatter fmt) {
      package_list_ = File.ReadAllLines(cfg.input);
      package_count_ = package_list_.Length;
      main_bar_.MaxTicks = main_bar_.MaxTicks + package_count_;
      main_cfg_ = main_cfg;
      cfg_ = cfg;
      po_ = new ParallelOptions {
        MaxDegreeOfParallelism = cfg.threading.parallel_pkg
      };
      seeding_ = main_cfg_.staging;
      memory_ = new Memory();
      filter_ = new Filter(main_cfg, cfg);
      file_fmt_ = fmt;
      db_ = Database.Open(cfg.id);
      bar_ = main_bar_.Spawn(0, cfg.id, bar_opts_);
    }
    
    public async Task Start() {
      foreach (var line in package_list_) {
        await Get(line);
        main_bar_.Tick();
      }
      ProcessIds();
    }

    protected void AddToVersionCount(int c) {
      versions_ += c;
    }

    protected void SetVersionCount(int c) {
      versions_ = c;
    }

    protected void SetPackageCount(int c) {
      main_bar_.MaxTicks = main_bar_.MaxTicks + c;
      memory_.SetCount(c);
    }

    private static string GetFilePath(string dir, string filename) {
      return Path.Combine(Path.GetFullPath(dir), filename);
    }

    ~BaseChannel() {
      main_bar_.WriteLine($"{cfg_.id} done.");
      bar_.Dispose();
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
    
    protected bool IsProcessed(string id, string version, string url) {
      DBPackage db_pkg = db_.GetPackage(id, version);
      if (db_pkg != null && db_pkg.IsProcessed()) {
        return true;
      } else if (db_pkg == null) {
        db_.AddPackage(id, version, url);
      }
      return false;
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
      prefix += $"Packages[{memory_.GetCount()}] Versions[{versions_}] ";
      prefix += $"Depth[{depth_}/{max_depth_}] {text}";
      return prefix;
    }

    protected void SetText(string text, bool wrap = true) {
      bar_.Message = wrap ? AddWrapperText(text) : $"{cfg_.id,-6} {text}";
    }
    
    public virtual void ProcessIds() {
      /* Parallel, max 5 concurrent fetchers */
      var ids = (List<string>)db_.GetAllPackages();
      SetVersionCount(db_.GetVersionCount());
      SetPackageCount(db_.GetPackageCount());
      bar_.MaxTicks = ids.Count;
      SetText($"Downloading");
      Parallel.ForEach(ids, po_, (id) => {
        try {
          bar_.Tick();
          main_bar_.Tick();
          if (memory_.IsValid(id)) {
            ProcessVersions(id).Wait();
          }
        } catch (Exception ex) {
          bar_.WriteErrorLine($"Error [{id}] - {ex}");
        }
      });
      SetText($"Completed");
    }

    /* Process all versions for a package. */
    private async Task ProcessVersions(string id) {
      var pkgs = (List<DBPackage>)db_.GetAllToDownload(id);

      using var bar = bar_.Spawn(pkgs.Count, id, bar_opts_);
      for (var i = 0; i < pkgs.Count; i++) {
        var pkg = pkgs[i];
        bar.Tick($"{id}@{pkg.version} [{i}/{pkgs.Count}]");
        await TryDownload(pkg, file_fmt_(pkg), bar);
      }
    }
    
    /* Try download a remote file */
    protected async Task TryDownload(DBPackage pkg, string path, ChildProgressBar bar) {
      try {
        if (pkg.url == null || pkg.url == "") {
          throw new ArgumentNullException($"{pkg.id} tarball is null.");
        }
        await Download(pkg, path, bar);
      } catch (Exception ex) {
        bar_.WriteErrorLine($"Tarball error [{pkg.id}] - {ex}");
      }
    }

    /* Download remote file. */
    private async Task Download(DBPackage pkg, string path, IProgressBar bar) {
      var out_fp = GetOutFilePath(path);
      CreateFilePath(out_fp);
      var on_disk = OnDisk(out_fp);
      /* If not on disk and the download succeeded */
      if (!on_disk) {
        RemoteFile file = new RemoteFile(pkg.url, bar);
        if (await file.Get(out_fp)) {
          CopyToDelta(path);
        } else {
          bar_.WriteErrorLine($"GetTarball error [{pkg.url}]");
        }
      } else {
      }
    }

    public abstract Task Get(string id);
  }
}
