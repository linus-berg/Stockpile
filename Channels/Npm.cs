using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stockpile.Services;
using RestSharp;

namespace Stockpile.Channels {
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

  public class Npm : BaseChannel {
    private const string API_ = "https://registry.npmjs.org/";
    private readonly RestClient client_ = new RestClient(API_);
    public Npm(Config.Main main_cfg, Config.Fetcher cfg) : base(main_cfg, cfg) {
    }


    protected override string GetFilePath(DBPackage pkg) {
      return StripRegistry(pkg.url).Replace("/-/", "/");
    }

    public override async Task Get(string id) {
      Depth++;
      var pkg = await GetPackage(id);
      /* Memorize to never visit this node again */
      ms_.Add(id);
      Update(id, "INSPECT");
      if (pkg == null || pkg.versions == null) {
        ms_.SetError(id);
      } else {
        await AddTransient(id, pkg);
      }
      Depth--;
    }

    private async Task AddTransient(string id, Package pkg) {
      /* For each version, add each versions dependencies! */
      foreach (var kv in pkg.versions) {
        /* Should version be filtered? */
        if (!fi_.Exec(id, kv.Key, 0, null)) {
          continue;
        }
        var manifest = kv.Value;
        string version = kv.Key;
        string url = manifest.dist.tarball ?? "";
        /* If package already in database AND FULLY PROCESSED */
        /* Do not reprocess dependency tree. */
        if (IsProcessed(id, version, url)) {
          continue;
        }
        if (manifest.dependencies != null) {
          foreach (var p in manifest.dependencies) {
            if (!ms_.Exists(p.Key)) {
              await Get(p.Key);
            }
          }
        }
        /* Upon walking back up the tree, set that this packages dependencies has been found. */
        db_.SetProcessed(id, version);
      }
    }

    private async Task<Package> GetPackage(string id) {
      try {
        return await client_.GetAsync<Package>(CreateRequest($"{id}/"));
      } catch (Exception ex) {
        ds_.Error($"[ERROR][NPM]Metadata][{id}] - {ex}");
        return null;
      }
    }

    private static string StripRegistry(string url) {
      return url.Replace(API_, "");
    }

    private static IRestRequest CreateRequest(string url, DataFormat fmt = DataFormat.Json) {
      return new RestRequest(url, fmt);
    }
  }
}
