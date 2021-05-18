using System;
using System.IO;
using System.Threading;
using NuGet.Common;

namespace CloneX.Fetchers {
  public class BaseFetcher {
    protected string out_dir_;
    protected string delta_dir_;
    protected ILogger logger_ = NullLogger.Instance;
    protected CancellationToken ct_ = CancellationToken.None; 
    protected BaseFetcher(string out_dir, string delta_dir) {
      this.out_dir_ = out_dir;
      this.delta_dir_ = delta_dir;
    }
    
    protected string GetOutPath(string filename) {
      string full_path = Path.GetFullPath(this.out_dir_);
      return Path.Combine(full_path, filename);
    }

    protected string GetDeltaPath(string filename) {
      string full_path = Path.GetFullPath(this.delta_dir_);
      return Path.Combine(full_path, filename);
    }

    protected void CopyToDelta(string filename) {
      File.Copy(GetOutPath(filename), GetDeltaPath(filename)); 
    }
  }
}
