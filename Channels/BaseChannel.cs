using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using Stockpile.Services;

namespace Stockpile.Channels {
  public abstract class BaseChannel {
    protected readonly Config.Main main_cfg_;
    protected readonly Config.Fetcher cfg_;
    private int depth_ = 0;
    private int max_depth_ = 0;

    /* Stockpile services */
    protected readonly DatabaseService db_;
    protected readonly IDisplayService ds_;
    protected readonly FileService fs_;
    protected readonly FilterService fi_;
    protected readonly MemoryService ms_;

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


    /* List of found package ids */
    protected ILogger logger_ = NullLogger.Instance;
    protected CancellationToken ct_ = CancellationToken.None;
    protected readonly string[] package_list_;

    protected BaseChannel(Config.Main main_cfg, Config.Fetcher cfg) {
      package_list_ = File.ReadAllLines(cfg.input);
      main_cfg_ = main_cfg;
      cfg_ = cfg;
      /* Services */
      db_ = DatabaseService.Open(cfg.id);
      ds_ = main_cfg.progress_bars ? new BarDisplayService(cfg.id) : 
                                     new ConsoleDisplayService(cfg.id);
      fs_ = new FileService(main_cfg, cfg);
      fi_ = new FilterService(main_cfg, cfg);
      ms_ = new MemoryService();
    }
    
    public async Task Start() {
      foreach (var id in package_list_) {
        if (!string.IsNullOrEmpty(id)) {
          await TryGet(id);
        }
      }
      ProcessIds();
    }
    
    public async Task TryGet(string id) {
      try {
        await Get(id);
      } catch (Exception e) {
        ds_.Error($"Could not fetch {id}.");
        ds_.Error(e.ToString());
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
    
    protected void Update(string msg, string op) {
      ds_.Update(new DisplayInfo () {
        Message = msg,
        Operation = op,
        Packages = ms_.GetCount(), 
        Versions = versions_, 
        Depth = depth_, 
        Max_Depth = max_depth_
      });
    }

    public virtual void ProcessIds() {
      /* Parallel, max 5 concurrent fetchers */
      List<string> ids = (List<string>)db_.GetAllPackages();
      
      /* Set the counts based on what is in database. */
      int v_c = db_.GetVersionCount();
      int p_c = db_.GetPackageCount();
      versions_ = v_c;
      ms_.SetCount(p_c);
      ds_.AddToCount(v_c);
      ds_.SetChannelCount(v_c);
      /* Process all IDs in parallel based on configuration */
      Parallel.ForEach(ids, new ParallelOptions {
          MaxDegreeOfParallelism = cfg_.threading.parallel_pkg
        }, (id) => {
        TryProcessId(id).Wait();
      });
      Update("", "COMPLETED");
    }

    private async Task TryProcessId(string id) {
      try {
        if (ms_.IsValid(id)) {
          await ProcessVersions(id);
        }
      } catch (Exception ex) {
        ds_.Error($"Error [{id}] - {ex}");
      }
    }

    /* Process all versions for a package. */
    private async Task ProcessVersions(string id) {
      List<DBPackage> pkgs = (List<DBPackage>)db_.GetAllToDownload(id);
      for (int i = 0; i < pkgs.Count; i++) {
        DBPackage pkg = pkgs[i];
        ds_.UpdatePackage(id, pkg.version, i, pkgs.Count);
        await TryDownload(pkg, GetFilePath(pkg));
      }
    }
    
    /* Try download a remote file */
    protected async Task TryDownload(DBPackage pkg, string path) {
      try {
        if (pkg.url == null || pkg.url == "") {
          throw new ArgumentNullException($"{pkg.id} tarball is null.");
        }
        await Download(pkg, path);
      } catch (Exception ex) {
        ds_.Error($"[ERROR][TryDownload][{cfg_.id}][{pkg.id}] - {ex}");
      }
    }

    /* Download remote file. */
    protected virtual async Task Download(DBPackage pkg, string path) {
      string out_fp = fs_.GetMainFilePath(path);
      FileService.CreateDirectory(out_fp);
      bool on_disk = FileService.OnDisk(out_fp);
      /* If not on disk and the download succeeded */
      if (!on_disk) {
        RemoteFile file = new RemoteFile(pkg.url, ds_);
        if (await file.Get(out_fp)) {
          fs_.CopyToDelta(path);
        } else {
          ds_.Error($"[ERROR][Download][{cfg_.id}][{pkg.url}]");
        }
      }
    }

    public abstract Task Get(string id);
    protected abstract string GetFilePath(DBPackage pkg);
  }
}
