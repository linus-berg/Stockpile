using System;
using System.Threading;
using NuGet.Common;

namespace Fetchers {
  class BaseFetcher {
    protected string out_dir_;
    protected ILogger logger_ = NullLogger.Instance;
    protected CancellationToken ct_ = CancellationToken.None; 
    protected BaseFetcher(string out_dir) {
      this.out_dir_ = out_dir;
    }
  }
}