using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stockpile.Config;
using Stockpile.Infrastructure.Entities;
using RestSharp;
using Stockpile.Models;
using System.IO;

namespace Stockpile.Channels {
  public class Helm : Channel {
    private const string API_ = "https://artifacthub.io/api/v1/packages/helm";
    private readonly RestClient client_ = new(API_);
    private readonly HashSet<string> containers_ = new HashSet<string>();
    private readonly string containers_file_;
    private bool write_containers_file_ = false;

    public Helm(MainConfig main_config, ChannelConfig cfg) : base(main_config,
      cfg) {
      if (cfg.options != null && cfg.options.ContainsKey("containers_list")) {
        containers_file_ = cfg.options["containers_list"];
        write_containers_file_ = true;
      }
    }

    protected override string GetDepositPath(Artifact artifact,
        ArtifactVersion version) {
      return $"{artifact.Name}/{Path.GetFileName(version.Url)}";
    }

    protected override async Task InspectArtifact(Artifact artifact) {
      HelmChartMetadata metadata = await GetMetadata(artifact.Name);
      await ProcessVersions(artifact, metadata);
    }

    protected override async Task OnComplete() {
      if (!write_containers_file_) {
        return;
      }
      Directory.CreateDirectory(Path.GetDirectoryName(containers_file_));
      StreamWriter f = new($"{containers_file_}");
      foreach(string image in containers_) {
        await f.WriteLineAsync(image);
      }
      f.Close();
    }

    private async Task ProcessVersions(Artifact artifact,
      HelmChartMetadata metadata) {
      foreach (HelmChartVersion hv in metadata.available_versions) {
        HelmChartMetadata vm = await GetMetadata(artifact.Name, hv.version);
        ArtifactVersion version =
          artifact.AddVersionIfNotExists(vm.version, vm.content_url);
        
        /* Add required containers */
        AddContainers(vm.containers_images);

        if (version.IsProcessed()) continue;
        await GetDependencies(vm.data);

        /* Set version to processed */
        version.SetStatus(ArtifactVersionStatus.PROCESSED);
      }
    }

    private void AddContainers(IEnumerable<HelmChartContainerImage> images) {
      foreach(HelmChartContainerImage image in images) {
        containers_.Add(image.image);
      }
    }

    private async Task GetDependencies(HelmChartData data) {
      if (data.dependencies == null) return;
      foreach (HelmChartDependency chart in data.dependencies)
        await TryInspectArtifact($"{chart.artifacthub_repository_name}/{chart.name}");
    }

    private async Task<HelmChartMetadata> GetMetadata(string id) {
      try {
        return await client_.GetAsync<HelmChartMetadata>(
          new RestRequest($"/{id}", DataFormat.Json));
      }
      catch (Exception ex) {
        ds_.PostError($"Metadata error -> {id} - {ex}");
        return null;
      }
    }
    
    private async Task<HelmChartMetadata> GetMetadata(string id, string version) {
      return await GetMetadata($"{id}/{version}");
    }
  }
}
