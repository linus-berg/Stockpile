using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using RestSharp;
using Stockpile.Config;
using Stockpile.Infrastructure.Entities;
using Stockpile.Models.Npm;

namespace Stockpile.Channels {
  public class Npm : Channel {
    private const string API_ = "https://registry.npmjs.org/";
    private readonly RestClient client_ = new(API_);
    private readonly bool get_dev_deps_;
    private readonly bool get_peer_deps_;

    public Npm(MainConfig main_config, ChannelConfig cfg) : base(main_config,
      cfg) {
      if (cfg.options == null)
        throw new NoNullAllowedException("Config options is null.");
      if (cfg.options.ContainsKey("get_peers") && cfg.options["get_peers"] == "true") 
        get_peer_deps_ = true;
      if (cfg.options.ContainsKey("get_dev") && cfg.options["get_dev"] == "true") 
        get_dev_deps_ = true;
    }

    protected override string GetDepositPath(Artifact artifact,
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
        ArtifactVersion version =
          artifact.AddVersionIfNotExists(kv.Key, package.dist.tarball);
        if (version.IsProcessed()) continue;
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
      foreach (KeyValuePair<string, string> package in dependencies)
        await TryInspectArtifact(package.Key);
    }

    private async Task<Metadata> GetMetadata(string id) {
      try {
        return await client_.GetAsync<Metadata>(
          new RestRequest($"{id}/", DataFormat.Json));
      }
      catch (Exception ex) {
        ds_.PostError($"Metadata error -> {id} - {ex}");
        return null;
      }
    }

    private static string StripRegistry(string url) {
      return url.Replace(API_, "");
    }
  }
}
