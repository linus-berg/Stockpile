using System;
using System.IO;
using LibGit2Sharp;

namespace Stockpile.Fetchers {

  class Git : BaseFetcher {

    public Git(Config.Main main_cfg, Config.Fetcher cfg) : base(main_cfg, cfg) {
      bar_.MaxTicks = package_count_;
    }

    public override void Get(string id) {
      SetText($"{id}");
      var uri = new Uri(id);
      bar_.Tick();
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

    public override void ProcessIds() {
    }
  }
}
