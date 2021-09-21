using System.Collections.Generic;

namespace Stockpile.Services {
  public class MemoryService {
    protected readonly Config.Main main_cfg_;
    protected readonly Config.Fetcher cfg_;
    private int packages_ = 0;

    /* List of found package ids */
    private HashSet<string> found_;
    private HashSet<string> error_;
    protected readonly DatabaseService db_;

    public MemoryService() {
      found_ = new();
      error_ = new();
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
