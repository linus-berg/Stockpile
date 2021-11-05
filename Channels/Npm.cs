using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Stockpile.Services;
using RestSharp;
using Stockpile.PackageModels.Npm;

namespace Stockpile.Channels {
  public class Npm : BaseChannel {
    private const string API_ = "https://registry.npmjs.org/";
    private readonly RestClient client_ = new RestClient(API_);
    private readonly bool get_peer_deps_ = false;
    private readonly bool get_dev_deps_ = false;

    public Npm(Config.Main main_cfg, Config.Fetcher cfg) : base(main_cfg, cfg) {
      if (cfg.options != null) {
        string[] options = cfg.options.Replace(" ", "").Split(';');
        if (options.Contains("get_peers")) {
          get_peer_deps_ = true;
        }
        if (options.Contains("get_dev")) {
          get_dev_deps_ = true;
        }
      }
    }

    protected override string GetFilePath(DBPackage pkg) {
      return StripRegistry(pkg.url).Replace("/-/", "/");
    }

    public override async Task Get(string id) {
      Depth++;
      Metadata metadata = await GetMetadata(id);
      /* Memorize to never visit this node again */
      ms_.Add(id);
      Update(id, "INSPECT");
      if (metadata == null || metadata.versions == null) {
        ms_.SetError(id);
      } else {
        await AddTransient(id, metadata);
      }
      Depth--;
    }

    private async Task AddTransient(string id, Metadata metadata) {
      /* For each version, add each versions dependencies! */
      foreach (KeyValuePair<string, Package> kv in metadata.versions) {
        /* Should version be filtered? */
        if (!fi_.Exec(id, kv.Key, 0, null)) {
          continue;
        }
        Package package = kv.Value;
        string version = kv.Key;
        string url = package.dist.tarball ?? "";
        /* If package already in database AND FULLY PROCESSED */
        /* Do not reprocess dependency tree. */
        if (IsProcessed(id, version, url)) {
          continue;
        }

        /* Get all types of dependencies. */
        await GetDependencies(package.dependencies);
        if (get_peer_deps_) {
          await GetDependencies(package.peerDependencies);
        }
        if (get_dev_deps_) {
          await GetDependencies(package.devDependencies);
        }
        /* Upon walking back up the tree, set that this packages dependencies has been found. */
        db_.SetProcessed(id, version);
      }
    }

    private async Task GetDependencies(Dictionary<string, string> dependencies) {
      if (dependencies == null) {
        return;
      }
      foreach (KeyValuePair<string, string> p in dependencies) {
        if (!ms_.Exists(p.Key)) {
          await Get(p.Key);
        }
      }
    }

    private async Task<Metadata> GetMetadata(string id) {
      try {
        return await client_.GetAsync<Metadata>(CreateRequest($"{id}/"));
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
