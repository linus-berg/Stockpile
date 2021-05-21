using System;
using System.IO;
using System.Threading;
using NuGet.Common;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CloneX.Fetchers {
  public abstract class BaseFetcher {
    protected enum Status {
      CHECK = 0,
      PARSE = 1,
      FETCH = 2,
      COMPLETE = 3,
      ERROR = 4
    };
    protected readonly string out_dir_;
    protected readonly string delta_dir_;
    protected readonly bool seeding_;
    protected readonly ParallelOptions po = new ParallelOptions {
      MaxDegreeOfParallelism = 5
    };
    protected int depth_ = 0; 
    
    private const string MSG_FMT_ = "{0,-6} - [T/D={1:F2}/{2:F2}mb] Packages:{4, -5} Versions:{3, -5} Depth={5,-5} {6}";
    private readonly string system_;
    private int pkg_count_ = 0;
    private long bytes_delta_ = 0;
    private long bytes_total_ = 0;
    private HashSet<string> found_;
    private HashSet<string> error_;

    protected ILogger logger_ = NullLogger.Instance;
    protected CancellationToken ct_ = CancellationToken.None; 
    protected BaseFetcher(
      string out_dir, 
      string delta_dir,
      string system,
      bool seeding = false) {
      this.out_dir_ = out_dir;
      this.delta_dir_ = delta_dir;
      this.seeding_ = seeding;
      this.found_ = new();
      this.error_ = new();
      this.system_ = system;
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
        system_,
        bytes_total_ / (1024.0 * 1024.0),
        bytes_delta_ / (1024.0 * 1024.0),
        pkg_count_,
        found_.Count,
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
      return GetFilePath(this.out_dir_, filename);
    }

    protected string GetDeltaFilePath(string filename) {
      return GetFilePath(this.delta_dir_, filename);
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
  }
}
