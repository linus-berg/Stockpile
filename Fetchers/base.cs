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
    protected Utils utils_;    
    private const string MSG_FMT_ = "{0} - {8}, {1,-6} - [T/D={2:F2}/{3:F2}mb] Packages:{4, -5} Versions:{5, -5} Depth={6,-5} {7}";
    private readonly string SYSTEM_;
    private readonly DateTime RUNTIME_;
    private int pkg_count_ = 0;
    private long bytes_delta_ = 0;
    private long bytes_total_ = 0;
    private HashSet<string> found_;
    private HashSet<string> error_;

    protected ILogger logger_ = NullLogger.Instance;
    protected CancellationToken ct_ = CancellationToken.None; 
    protected readonly Database db_;
    protected BaseFetcher(
      Database db,
      Config.Fetcher cfg,
      DateTime runtime,
      bool seeding = false) {
      this.db_ = db;
      this.cfg_ = cfg;
      this.po_ = new ParallelOptions {
        MaxDegreeOfParallelism = cfg.threading.parallel_pkg
      };
      this.SYSTEM_ = cfg.id;
      this.RUNTIME_ = runtime;
      this.seeding_ = seeding;
      this.found_ = new();
      this.error_ = new();
      utils_ = new Utils(RUNTIME_, SYSTEM_);
    }

    protected void AddPkgCount(int c) {
      pkg_count_ += c;
    }

    private string GetFilePath(string dir, string filename) {
      return Path.Combine(Path.GetFullPath(dir), filename);
    }

    protected void AddBytes(string file_path) {
      Interlocked.Add(ref bytes_total_, GetBytes(file_path));
    }

    protected long GetBytes(string file_path) {
      return new FileInfo(file_path).Length;
    }

    
    protected void SetStatus(string id, Status status) {
      string status_msg = status switch {
        Status.CHECK => $"Metadata {id}",
        Status.PARSE => $"Scanning {id}",
        Status.FETCH => $"Download {id}",
        Status.ERROR => $"!!ERROR!! -- {id}",
        Status.COMPLETE => "Complete!",
        _ => "Unknown"
      };
      
      utils_.Message(new Message {
        message = status_msg,
        bytes_total = bytes_total_ / (1024.0 * 1024.0),
        bytes_delta = bytes_delta_ / (1024.0 * 1024.0),
        packages = found_.Count,
        versions = pkg_count_,
        depth = depth_
      });
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

    protected string GetDeltaFilePath(string filepath) {
      return GetFilePath(this.cfg_.output.delta, filepath);
    }

    protected void CopyToDelta(string fp) {
      string out_fp = GetOutFilePath(fp);
      Interlocked.Add(ref this.bytes_delta_, GetBytes(out_fp));
      if (!this.seeding_) {
        string delta_fp = GetDeltaFilePath(fp);
        CreateFilePath(delta_fp);
        File.Copy(out_fp, delta_fp); 
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
