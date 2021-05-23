using System;
using System.IO;
using System.Threading;
using NuGet.Common;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Stockpile.Fetchers {
  public abstract class BaseFetcher {
    protected enum Status {
      CHECK = 0,
      PARSE = 1,
      FETCH = 2,
      COMPLETE = 3,
      ERROR = 4
    };
    protected readonly Config.Fetcher cfg_;
    protected readonly bool seeding_;
    protected readonly ParallelOptions po_;
    protected int depth_ = 0; 
    
    private const string MSG_FMT_ = "{0}, {1,-6} - [T/D={2:F2}/{3:F2}mb] Packages:{4, -5} Versions:{5, -5} Depth={6,-5} {7}";
    private readonly string SYSTEM_;
    private readonly DateTime RUNTIME_;
    private int pkg_count_ = 0;
    private long bytes_delta_ = 0;
    private long bytes_total_ = 0;
    private HashSet<string> found_;
    private HashSet<string> error_;

    protected ILogger logger_ = NullLogger.Instance;
    protected CancellationToken ct_ = CancellationToken.None; 
    protected BaseFetcher(
      Config.Fetcher cfg,
      DateTime runtime,
      bool seeding = false) {
      this.cfg_ = cfg;
      this.po_ = new ParallelOptions {
        MaxDegreeOfParallelism = cfg.threading.parallel_pkg
      };
      this.SYSTEM_ = cfg.id;
      this.RUNTIME_ = runtime;
      this.seeding_ = seeding;
      this.found_ = new();
      this.error_ = new();
    }

    protected void AddPkgCount(int c) {
      pkg_count_ += c;
    }

    private string GetFilePath(string dir, string filename) {
      return Path.Combine(Path.GetFullPath(dir), filename);
    }

    protected void AddBytes(string file_path) {
      this.bytes_total_ += GetBytes(file_path);
    }

    protected long GetBytes(string file_path) {
      return new FileInfo(file_path).Length;
    }


    protected void SetStatus(string id, Status status) {
      string status_msg = status switch {
        Status.CHECK => $"Checking {id}",
        Status.PARSE => $"Finding dependencies {id}",
        Status.FETCH => $"Fetching {id}",
        Status.ERROR => $"ERROR->{id}",
        Status.COMPLETE => "Complete!",
        _ => "Unknown"
      };
      string msg = string.Format(
        MSG_FMT_,
        RUNTIME_,
        SYSTEM_,
        bytes_total_ / (1024.0 * 1024.0),
        bytes_delta_ / (1024.0 * 1024.0),
        found_.Count,
        pkg_count_,
        depth_,
        status_msg
      );
      Console.WriteLine(msg);
    }

    ~BaseFetcher() {
      SetStatus("", Status.COMPLETE);
    }

    protected void CreateFilePath(string file_path) {
      string dir =  Path.GetDirectoryName(file_path);
      if (!Directory.Exists(dir)) {
        Directory.CreateDirectory(dir);
      }
    }

    protected string GetOutFilePath(string filename) {
      return GetFilePath(this.cfg_.output.full, filename);
    }

    protected string GetDeltaFilePath(string filename) {
      return GetFilePath(this.cfg_.output.delta, filename);
    }

    protected void CopyToDelta(string out_fp) {
      if (!this.seeding_) {
        string filename = Path.GetFileName(out_fp);
        string delta_fp = GetDeltaFilePath(filename);
        this.bytes_delta_ += GetBytes(out_fp);
        CreateFilePath(delta_fp);
        if (!File.Exists(filename)) {
          File.Copy(out_fp, delta_fp); 
        }
      }
    }

    protected bool InMemory(string id) {
      return this.found_.Contains(id);
    }
    
    protected void Memorize(string id) {
      this.found_.Add(id); 
    }

    protected void SetError(string id) {
      this.error_.Add(id);
    }

    protected bool IsValid(string id) {
      return !this.error_.Contains(id);
    }

    protected HashSet<string> GetMemory() {
      return this.found_;
    }

    protected bool OnDisk(string path) {
      return File.Exists(path);
    }

    public abstract void Get(string id);
    public abstract void ProcessIds();
  }
}
