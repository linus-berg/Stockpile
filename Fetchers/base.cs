using System;

namespace Fetchers {
  class BaseFetcher {
    protected string out_dir_;
    protected BaseFetcher(string out_dir) {
      this.out_dir_ = out_dir;
    }
  }
}