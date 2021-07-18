using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RestSharp;

namespace Stockpile.Fetchers {
  class Distribution {
    public string shasum { get; set; }
    public string tarball { get; set; }
  }

  class Manifest {
    public Dictionary<string, string> dependencies { get; set; }
    public Distribution dist { get; set; }
  }

  class Package {
    public Dictionary<string, Manifest> versions { get; set; }
  }

  public class Npm : BaseFetcher {
    private const string REGISTRY = "https://registry.npmjs.org/";
    private readonly RestClient client_ = new RestClient(REGISTRY);
    public Npm(Config.Main main_cfg, Config.Fetcher cfg) : base(main_cfg, cfg) {
    }


    public override void Get(string id) {
      Depth++;
      var pkg = GetPackage(id);
      /* Memorize to never visit this node again */
      Memorize(id);
      SetText($"{id}");
      if (pkg == null || pkg.versions == null) {
        SetError(id);
      } else {
        AddTransient(id, pkg);
      }
      Depth--;
    }

    private void AddTransient(string id, Package pkg) {
      /* For each version, add each versions dependencies! */
      AddToVersionCount(pkg.versions.Count);
      foreach (var kv in pkg.versions) {
        /* Should version be filtered? */
        if (!ExecFilters(id, kv.Key, 0, null)) {
          AddToVersionCount(-1);
          continue;
        }
        var manifest = kv.Value;
        var version = kv.Key;
        var db_pkg = db_.GetPackage(id, version);
        var in_db = db_pkg != null;
        /* If package already in database AND FULLY PROCESSED */
        /* Do not reprocess dependency tree. */
        if (in_db && db_pkg.IsProcessed()) {
          continue;
        } else if (!in_db) {
          var url = manifest.dist.tarball ?? "";
          db_.AddPackage(id, version, url);
        }

        if (manifest.dependencies != null) {
          foreach (var p in manifest.dependencies) {
            if (!InMemory(p.Key)) {
              Get(p.Key);
            }
          }
        }
        /* Upon walking back up the tree, set that this packages dependencies has been found. */
        db_.SetProcessed(id, version);
      }
    }

    public override void ProcessIds() {
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
          if (IsValid(id)) {
            ProcessVersions(id);
          }
        } catch (Exception ex) {
          bar_.WriteErrorLine($"Error [{id}] - {ex}");
        }
      });
      SetText($"Completed");
    }

    private void ProcessVersions(string id) {
      var pkgs = (List<DBPackage>)db_.GetAllToDownload(id);
      using var bar = bar_.Spawn(pkgs.Count, id, bar_opts_);
      for (var i = 0; i < pkgs.Count; i++) {
        var pkg = pkgs[i];
        bar.Tick($"{id}@{pkg.version} [{i}/{pkgs.Count}]");
        TryGetTarball(id, pkg.url);
      }
    }

    private void TryGetTarball(string id, string url) {
      try {
        if (url == null || url == "") {
          throw new ArgumentNullException($"{id} tarball is null.");
        }
        GetTarball(url);
      } catch (Exception ex) {
        bar_.WriteErrorLine($"Tarball error [{id}] - {ex}");
      }
    }

    private void GetTarball(string url) {
      var fp = StripRegistry(url).Replace("/-/", "/");
      var out_fp = GetOutFilePath(fp);
      CreateFilePath(out_fp);
      var on_disk = OnDisk(out_fp);
      /* If not on disk and the download succeeded */
      if (!on_disk) {
        if (Download(url, out_fp)) {
          CopyToDelta(fp);
        } else {
          bar_.WriteErrorLine($"GetTarball error [{url}]");
        }
      } else {
      }
    }

    private bool Download(string url, string out_fp) {
      try {
        using var fs = File.OpenWrite(out_fp);
        var req = CreateRequest(url, DataFormat.None);
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

    private static string StripRegistry(string url) {
      return url.Replace(REGISTRY, "");
    }

    private static IRestRequest CreateRequest(string url, DataFormat fmt = DataFormat.Json) {
      return new RestRequest(url, fmt);
    }
  }
}
