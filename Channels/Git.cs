using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LibGit2Sharp;
using Stockpile.Config;
using Stockpile.Infrastructure.Entities;

namespace Stockpile.Channels {
  internal class Git : Channel {
    public Git(MainConfig main_config, ChannelConfig cfg) : base(main_config,
      cfg) {
    }

    protected override string GetDepositPath(Artifact artifact,
      ArtifactVersion version) {
      return "";
    }

    protected override Task InspectArtifact(Artifact artifact) {
      return Task.CompletedTask;
    }

    protected override Task DownloadArtifactsToDisk() {
      HashSet<string> ids = GetAllArtifactsInMemory();
      foreach (string id in ids) ProcessRepo(id);
      return Task.CompletedTask;
    }

    private void ProcessRepo(string id) {
      Uri uri = new(id);
      string abs_path = Path.GetFullPath(cfg_.deposits.main);
      string path = Path.Join(abs_path + uri.Host.Replace("www", ""),
        uri.AbsolutePath);

      /* Just delete old things then download again... easiest solution */
      if (Directory.Exists(path)) Directory.Delete(path, true);
      Directory.CreateDirectory(path);
      CloneOptions co = new();
      co.IsBare = true;
      Repository.Clone(id, path, co);
    }
  }
}
