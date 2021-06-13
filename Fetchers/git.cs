using System;
using System.IO;
using LibGit2Sharp;

namespace Stockpile.Fetchers {

class Git : BaseFetcher  {

  public Git(Config.Fetcher cfg, DateTime runtime, bool seeding = false) : base(cfg, runtime, seeding) {
  }

  public override void Get(string id) {
    Uri uri = new Uri(id);
    
    string abs_path = Path.GetFullPath(this.cfg_.output.full);
    string path = Path.Join(abs_path + uri.Host.Replace("www", ""), uri.AbsolutePath);

    /* Just delete old things then download again... easiest solution */ 
    if (Directory.Exists(path)) {
      Directory.Delete(path);
    }
    Directory.CreateDirectory(path);

    CloneOptions co = new CloneOptions();
    co.IsBare = true;
    Repository.Clone(id, path, co);
  }

  public override void ProcessIds() {
  }
} 
}
