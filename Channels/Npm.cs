using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using RestSharp;
using Stockpile.Config;
using Stockpile.Database;
using Stockpile.PackageModels.Npm;
using Stockpile.Services;

namespace Stockpile.Channels {
  public class Npm : BaseChannel {
    private const string API_ = "https://registry.npmjs.org/";
    private readonly RestClient client_ = new(API_);
    private readonly bool get_dev_deps_;
    private readonly bool get_peer_deps_;

    public Npm(Main main_cfg, Fetcher cfg) : base(main_cfg, cfg) {
      if (cfg.options == null) {
        throw new NoNullAllowedException("Config options was null.");
      };
      string[] options = cfg.options.Replace(" ", "").Split(';');
      if (options.Contains("get_peers")) get_peer_deps_ = true;
      if (options.Contains("get_dev")) get_dev_deps_ = true;
    }

    protected override string GetFilePath(ArtifactVersion version) {
      return StripRegistry(version.Url).Replace("/-/", "/");
    }

    protected override async Task Get(string id) {
      Depth++;
      Metadata metadata = await GetMetadata(id);
      /* Memorize to never visit this node again */
      ms_.Add(id);
      Update(id, Operation.INSPECT);
      if (metadata == null || metadata.versions == null)
        ms_.SetError(id);
      else
        await AddTransient(id, metadata);
      Depth--;
    }

    private async Task AddTransient(string id, Metadata metadata) {
      /* Add the artifact if it does not exist */
      Artifact artifact = await db_.AddArtifact(id);
      
      /* For each version, add each versions dependencies! */
      foreach (KeyValuePair<string, PackageModels.Npm.Package> kv in metadata.versions) {
        /* Should version be filtered? */
        if (!fi_.Exec(id, kv.Key, 0, null)) continue;
        PackageModels.Npm.Package package = kv.Value;
        string version = kv.Key;
        string url = package.dist.tarball ?? "";
        /* If package already in database AND FULLY PROCESSED */
        /* Do not reprocess dependency tree. */
        if (artifact.IsVersionProcessed(version)) {
          continue;
        }
        /* Get all types of dependencies. */
        await GetDependencies(package.dependencies);
        if (get_peer_deps_) await GetDependencies(package.peerDependencies);
        if (get_dev_deps_) await GetDependencies(package.devDependencies);
        /* Upon walking back up the tree, set that this packages dependencies has been found. */
        artifact.SetVersionAsProcessed(version, url);
      }
      await db_.SaveArtifact(artifact);
    }

    private async Task
      GetDependencies(Dictionary<string, string> dependencies) {
      if (dependencies == null) return;
      foreach (KeyValuePair<string, string> p in dependencies)
        if (!ms_.Exists(p.Key))
          await Get(p.Key);
    }

    private async Task<Metadata> GetMetadata(string id) {
      try {
        return await client_.GetAsync<Metadata>(CreateRequest($"{id}/"));
      }
      catch (Exception ex) {
        ds_.PostError($"Metadata error -> {id} - {ex}");
        return null;
      }
    }

    private static string StripRegistry(string url) {
      return url.Replace(API_, "");
    }

    private static IRestRequest CreateRequest(string url,
      DataFormat fmt = DataFormat.Json) {
      return new RestRequest(url, fmt);
    }
  }
}