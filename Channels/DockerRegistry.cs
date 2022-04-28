using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Stockpile.Config;
using Stockpile.Infrastructure.Entities;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.IO;

namespace Stockpile.Channels {
  public class DockerRegistry : Channel {
    private readonly string REGISTRY_;
    private readonly DockerClient client_;

    public DockerRegistry(MainConfig main_config, ChannelConfig cfg) : base(main_config,
      cfg) {
      if (cfg.options == null)
        throw new NoNullAllowedException("Config options was null.");
      string[] options = cfg.options.Replace(" ", "").Split(';');
      REGISTRY_ = TryExtractRegistry(options);
      client_ = new DockerClientConfiguration().CreateClient();   
      ds_.Post($"Registry={REGISTRY_}", Constants.Operation.INFO);
    }

    private static string TryExtractRegistry(string[] options) {
      foreach(string option in options) {
        string[] opt_arr = option.Split("=");
        if (opt_arr.Length <= 0) {
          continue;
        }
        if (opt_arr[0] == "registry") {
          return opt_arr[1];
        }
      }
      return null;
    }

    protected override string GetDepositPath(Artifact artifact,
      ArtifactVersion version) {
      return "";
    }

    protected override Task InspectArtifact(Artifact artifact) {
      return Task.CompletedTask;
    }

    protected override async Task DownloadArtifactsToDisk() {
      HashSet<string> ids = GetAllArtifactsInMemory();
      foreach (string id in ids) await ProcessImage(id);
    }
    
    private async Task ProcessImage(string id) {
      string[] id_split = id.Split(":");
      string image = id_split[0];
      string tag = id_split[1];
      string abs_path = Path.GetFullPath(cfg_.deposits.main);
      string tar_name = GetTarName(image, tag);
      string path = Path.Join(abs_path, tar_name);
      if (!File.Exists(path)) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await TryPullImage(image, tag);
        await TrySaveImage(id, path);
      }
    }

    private async Task TryPullImage(string image, string tag) {
      try {
        await PullImage(image, tag);
      } catch (Exception e) {
        ds_.PostError(e.ToString());
      }
    }
    
    private async Task TrySaveImage(string id, string path) {
      try {
        await SaveImage(id, path);
      } catch (Exception e) {
        ds_.PostError(e.ToString());
      }
    }

    private async Task PullImage(string image, string tag) {
      ds_.Post($"{image}:{tag}", Constants.Operation.DOWNLOAD);
      ImagesCreateParameters p = new ImagesCreateParameters(){
        Repo = REGISTRY_,
        FromImage = image,
        Tag = tag
      };
      await client_.Images.CreateImageAsync(p, null, new Progress<JSONMessage>());
    }

    private async Task SaveImage(string id, string path) {
      using Stream s = await client_.Images.SaveImageAsync(id);
      using FileStream f = File.OpenWrite(path);
      await s.CopyToAsync(f);
      f.Close();
    }
    
    private static string GetTarName(string image, string tag) {
      return $"{ReplaceInvalidChars(image)}_{ReplaceInvalidChars(tag)}.tgz";
    }

    private static string ReplaceInvalidChars(string input) {
      return input.Replace("/", "_").Replace(".", "_");
    }
  }
}
