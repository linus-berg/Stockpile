using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using RestSharp;
using System;

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
  private static readonly string[] VERSION_OUTLAWS = new string[] {
    "alpha",
    "beta",
    "nightly",
    "dev"
  };
  private const string REGISTRY = "https://registry.npmjs.org/";
  private readonly RestClient client_ = new RestClient(REGISTRY);
  public Npm(Config.Main main_cfg, Config.Fetcher cfg) : base(main_cfg, cfg) {
  }

  public override void Get(string id) {
    depth_++;
    SetStatus(id, Status.CHECK);
    Package pkg = GetPackage(id);
    /* Memorize to never visit this node again */
    this.Memorize(id);
    if (pkg == null || pkg.versions == null) {
      SetStatus(id, Status.ERROR);
      this.SetError(id);
    } else {
      AddTransient(id, pkg);
    }
    depth_--;
  }

  private void AddTransient(string id, Package pkg) {
    /* For each version, add each versions dependencies! */
    foreach(var kv in pkg.versions) {
      /* Should version be filtered? */
      if (!ExecFilters(id, kv.Key, 0, null)) {
        continue;
      }
      Manifest manifest = kv.Value;
      string version = kv.Key;
      DBPackage db_pkg = db_.GetPackage(id, version);
      bool in_db = db_pkg != null;
      bool is_processed = in_db && db_pkg.IsProcessed(); 
      this.AddToVersionCount(1);
      /* If package already in database AND FULLY PROCESSED */
      /* Do not reprocess dependency tree. */
      if (is_processed) {
        continue;
      }

      if (!in_db) {
        string url = manifest.dist.tarball ?? "";
        this.db_.AddPackage(id, version, url);
      }

      if (manifest.dependencies != null) {
        SetStatus($"{id}@{kv.Key} ({manifest.dependencies.Count})", Status.PARSE);
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
    Parallel.ForEach(ids, po_, (id) => {
      try {
        if (this.IsValid(id)) {
          ProcessVersions(id);
        }
      } catch (Exception ex) {
        Console.WriteLine($"Error processing {id} - {ex}");
      }
    });
  }

  private void ProcessVersions(string id) {
    IEnumerable<DBPackage> pkgs = this.db_.GetAllToDownload(id);
    foreach(DBPackage pkg in pkgs) {
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
      Console.WriteLine($"Error getting tarball {id} - {ex}");
    }
  }
  
  private void GetTarball(string url) {
    string fp = StripRegistry(url).Replace("/-/", "/");
    string out_fp = this.GetOutFilePath(fp);
    this.CreateFilePath(out_fp);
    bool on_disk = OnDisk(out_fp);
    /* If not on disk and the download succeeded */
    if (!on_disk) {
      SetStatus($"{fp}", Status.FETCH);
      if(Download(url, out_fp)) {
        this.CopyToDelta(fp);
      } else {
        Console.WriteLine($"{url} download failed");
      }
    } else {
      //SetStatus($"{fp}", Status.EXISTS);
    }
    this.AddBytes(out_fp);
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
      Console.WriteLine($"Error downloading {url} - {ex}");
      return false;
    }
    return true;
  }
  

  private Package GetPackage(string id) {
    try {
      return client_.Get<Package>(CreateRequest($"{id}/")).Data; 
    } catch (Exception ex) {
      Console.WriteLine($"Error getting metadata for {id} - {ex}");
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
