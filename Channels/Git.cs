using System;
using System.IO;
using LibGit2Sharp;
using System.Threading.Tasks;
using System.Collections.Generic;
using Stockpile.Services;

namespace Stockpile.Channels {

  class Git : BaseChannel {

    public Git(Config.Main main_cfg, Config.Fetcher cfg) : base(main_cfg, cfg) {
    }

    protected override string GetFilePath(DBPackage pkg) {
      return "";
    }

    public override Task Get(string id) {
      Update(id, Operation.DOWNLOAD);
      ms_.Add(id);
      return Task.CompletedTask;
    }

    public override void ProcessIds() {
      HashSet<string> ids = ms_.GetMemory();
      foreach (string id in ids) {
        ProcessRepo(id);
      }
    }

    private void ProcessRepo(string id) {
      var uri = new Uri(id);
      var abs_path = Path.GetFullPath(cfg_.output.full);
      var path = Path.Join(abs_path + uri.Host.Replace("www", ""), uri.AbsolutePath);

      /* Just delete old things then download again... easiest solution */
      if (Directory.Exists(path)) {
        Directory.Delete(path, true);
      }
      Directory.CreateDirectory(path);
      var co = new CloneOptions();
      co.IsBare = true;
      Repository.Clone(id, path, co);
    }
  }
}
