using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using RestSharp;
using Stockpile.Config;
using Stockpile.Database;
using Stockpile.PackageModels.Npm;

namespace Stockpile.Channels {
  public class Npm : BaseChannel {
    private const string API_ = "https://registry.npmjs.org/";
    private readonly RestClient client_ = new(API_);
    private readonly bool get_dev_deps_;
    private readonly bool get_peer_deps_;

    public Npm(MainConfig main_config, ChannelConfig cfg) : base(main_config, cfg) {
      if (cfg.options == null)
        throw new NoNullAllowedException("Config options was null.");
      ;
      string[] options = cfg.options.Replace(" ", "").Split(';');
      if (options.Contains("get_peers")) get_peer_deps_ = true;
      if (options.Contains("get_dev")) get_dev_deps_ = true;
    }

    protected override string GetFilePath(Artifact artifact,
      ArtifactVersion version) {
      return StripRegistry(version.Url).Replace("/-/", "/");
    }

    protected override async Task InspectArtifact(Artifact artifact) {
      Metadata metadata = await GetMetadata(artifact.Name);
      if (metadata?.versions == null)
        SetArtifactError(artifact);
      else
        await ProcessArtifactVersions(artifact, metadata);
    }

    private async Task ProcessArtifactVersions(Artifact artifact,
      Metadata metadata) {
      foreach (KeyValuePair<string, Package> kv in metadata.versions) {
        Package package = kv.Value;
        string v = kv.Key;
        string u = package.dist.tarball ?? "";
        ArtifactVersion version = artifact.AddVersionIfNotExists(v, u);
        if (!version.ShouldProcess()) continue;
        await ProcessArtifactVersionDependencies(package);
        /* Set version to processed */
        version.SetStatus(ArtifactVersionStatus.PROCESSED);
      }
    }

    private async Task ProcessArtifactVersionDependencies(Package package) {
      /* Get primary dependencies. */
      await GetDependencies(package.dependencies);
      /* Get Peer dependencies */
      if (get_peer_deps_) await GetDependencies(package.peerDependencies);
      /* Get Dev dependencies */
      if (get_dev_deps_) await GetDependencies(package.devDependencies);
    }

    private async Task
      GetDependencies(Dictionary<string, string> dependencies) {
      if (dependencies == null) return;
      foreach (KeyValuePair<string, string> p in dependencies)
        await TryInspectArtifact(p.Key);
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