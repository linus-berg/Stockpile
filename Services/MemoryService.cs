using System.Collections.Generic;
using Stockpile.Config;

namespace Stockpile.Services {
  public class MemoryService {
    protected readonly ChannelConfig cfg_;
    protected readonly DatabaseService db_;
    private readonly HashSet<string> error_;

    /* List of found package ids */
    private readonly HashSet<string> found_;
    protected readonly MainConfig main_config;
    private int packages_;

    public MemoryService() {
      found_ = new HashSet<string>();
      error_ = new HashSet<string>();
    }

    public bool Exists(string id) {
      return found_.Contains(id);
    }

    public void Add(string id) {
      packages_++;
      found_.Add(id);
    }

    public int GetCount() {
      return packages_;
    }

    public void SetCount(int c) {
      packages_ = c;
    }

    public void SetError(string id) {
      error_.Add(id);
    }

    public bool IsValid(string id) {
      return !error_.Contains(id);
    }

    public HashSet<string> GetMemory() {
      return found_;
    }
  }
}