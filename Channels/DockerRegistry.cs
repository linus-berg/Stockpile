using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stockpile.Config;
using Stockpile.Infrastructure.Entities;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.IO;

namespace Stockpile.Channels {
  public class DockerRegistry : Channel {
    private readonly DockerClient client_;

    public DockerRegistry(MainConfig main_config, ChannelConfig cfg) : base(main_config,
      cfg) {
      client_ = new DockerClientConfiguration().CreateClient();   
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
      ds_.Post("Completed", Constants.Operation.COMPLETED);
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
        FromImage = image,
        Tag = tag
      };
      Progress<JSONMessage> progress = new Progress<JSONMessage>((message) => {
        if (message.Progress != null && message.Progress.Total != 0) {
          long total = message.Progress.Total;
          long current = message.Progress.Current;
          double percent = 100 * ((double)current / total);
          ds_.Post($"{image}:{tag}:{message.ID} {percent:F2}%", Constants.Operation.DOWNLOAD);
        }
      });
      await client_.Images.CreateImageAsync(p, null, progress);
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
