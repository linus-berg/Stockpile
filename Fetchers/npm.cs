using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using RestSharp;
using System;
using ShellProgressBar;

namespace Stockpile.Fetchers {
class Distribution {
  public string shasum {get; set;}
  public string tarball { get; set;}
}

class Manifest {
  public Dictionary<string, string> dependencies {get; set;}
  public Distribution dist {get; set;}
}

class Package {
  public Dictionary<string, Manifest> versions {get; set;}
}

public class Npm : BaseFetcher {
  private const string REGISTRY = "https://registry.npmjs.org/";
  private readonly RestClient client_ = new RestClient(REGISTRY);
  public Npm(Config.Main main_cfg, Config.Fetcher cfg) : base(main_cfg, cfg) {
  }
  

  public override void Get(string id) {
    Depth++;
    Package pkg = GetPackage(id);
    /* Memorize to never visit this node again */
    this.Memorize(id);
    SetText($"{id}");
    if (pkg == null || pkg.versions == null) {
      this.SetError(id);
    } else {
      AddTransient(id, pkg);
    }
    Depth--;
  }

  private void AddTransient(string id, Package pkg) {
    /* For each version, add each versions dependencies! */
    this.AddToVersionCount(pkg.versions.Count);
    foreach(var kv in pkg.versions) {
      /* Should version be filtered? */
      if (!ExecFilters(id, kv.Key, 0, null)) {
        this.AddToVersionCount(-1);
        continue;
      }
      Manifest manifest = kv.Value;
      string version = kv.Key;
      DBPackage db_pkg = db_.GetPackage(id, version);
      bool in_db = db_pkg != null;
      /* If package already in database AND FULLY PROCESSED */
      /* Do not reprocess dependency tree. */
      if (in_db && db_pkg.IsProcessed()) {
        continue;
      } else if (!in_db) {
        string url = manifest.dist.tarball ?? "";
        this.db_.AddPackage(id, version, url);
      }
      
      if (manifest.dependencies != null) {
        foreach(KeyValuePair<string, string> p in manifest.dependencies) {
          if (!this.InMemory(p.Key)) {
            Get(p.Key);
          }
        }
      }
      /* Upon walking back up the tree, set that this packages dependencies has been found. */
      this.db_.SetProcessed(id, version);
    }
  }
  
  public override void ProcessIds() {
    /* Parallel, max 5 concurrent fetchers */
    List<string> ids = (List<string>)db_.GetAllPackages();
    SetVersionCount(this.db_.GetVersionCount());
    SetPackageCount(this.db_.GetPackageCount());
    bar_.MaxTicks = ids.Count;
    SetText($"Downloading");
    Parallel.ForEach(ids, po_, (id) => {
      try {
        bar_.Tick();
        main_bar_.Tick();
        if (this.IsValid(id)) {
          ProcessVersions(id);
        }
      } catch (Exception ex) {
        bar_.WriteErrorLine($"Error [{id}] - {ex}");
      }
    });
    SetText($"Completed");
  }

  private void ProcessVersions(string id) {
    List<DBPackage> pkgs = (List<DBPackage>)this.db_.GetAllToDownload(id);
    using ChildProgressBar bar = bar_.Spawn(pkgs.Count, id, bar_opts_);
    for(int i = 0; i < pkgs.Count; i++) {
      DBPackage pkg = pkgs[i];
      bar.Tick($"{id}@{pkg.version} [{i}/{pkgs.Count}]");
      TryGetTarball(id, pkg.url);
    }
  }

  private void TryGetTarball(string id, string url) {
    try {
      if (url == null || url == "") {
        throw new ArgumentNullException($"{id} tarball is null.");
      }
      this.GetTarball(url);
    } catch (Exception ex) {
      bar_.WriteErrorLine($"Tarball error [{id}] - {ex}");
    }
  }
  
  private void GetTarball(string url) {
    string fp = StripRegistry(url).Replace("/-/", "/");
    string out_fp = this.GetOutFilePath(fp);
    this.CreateFilePath(out_fp);
    bool on_disk = OnDisk(out_fp);
    /* If not on disk and the download succeeded */
    if (!on_disk) {
      if(Download(url, out_fp)) {
        this.CopyToDelta(fp);
      } else {
        bar_.WriteErrorLine($"GetTarball error [{url}]");
      }
    } else {
    }
  }

  private bool Download(string url, string out_fp) {
    try {
      using FileStream fs = File.OpenWrite(out_fp);
      IRestRequest req = CreateRequest(url, DataFormat.None);
      req.ResponseWriter = stream => {
        using (stream) {
          stream.CopyTo(fs);
        }
      };
      client_.DownloadData(req);
      fs.Close();
    } catch (Exception ex) {
      bar_.WriteErrorLine($"Download error [{url}] - {ex}");
      return false;
    }
    return true;
  }
  

  private Package GetPackage(string id) {
    try {
      return client_.Get<Package>(CreateRequest($"{id}/")).Data; 
    } catch (Exception ex) {
      bar_.WriteErrorLine($"Metadata error [{id}] - {ex}");
      return null;
    }
  }

  private string StripRegistry(string url) {
    return url.Replace(REGISTRY, "");
  }

  private IRestRequest CreateRequest(string url, DataFormat fmt = DataFormat.Json) {
    return new RestRequest(url, fmt);
  }
}
}
