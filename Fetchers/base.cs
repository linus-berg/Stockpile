using System;
using System.IO;
using System.Threading;
using NuGet.Common;
using ShellProgressBar;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CloneX.Fetchers {
  public abstract class BaseFetcher {
    private int pkg_count_ = 0;
    protected string out_dir_;
    protected string delta_dir_;
    private ProgressBar pb_;
    private ChildProgressBar cb_;
    protected bool seeding_;
    private HashSet<string> found_;
    private const string MSG_FMT_ = "{0,-6} - T/D={1:F2}/{2:F2}mb Packs={3}/{4, -5} Depth={5,-5} Status={6}";
    private readonly string system_;
    protected int depth_ = 0; 
    private long bytes_delta_ = 0;
    private long bytes_total_ = 0;

    protected enum Status {
      CHECK = 0,
      FETCH = 1,
      COMPLETE = 2
    };

    protected ILogger logger_ = NullLogger.Instance;
    protected CancellationToken ct_ = CancellationToken.None; 
    protected BaseFetcher(
      string out_dir, 
      string delta_dir,
      ProgressBar pb,
      string system,
      bool seeding = false) {
      this.out_dir_ = out_dir;
      this.delta_dir_ = delta_dir;
      this.pb_ = pb;
      this.cb_ = pb.Spawn(0, "");
      this.seeding_ = seeding;
      this.found_ = new();
      this.system_ = system;
    }

    protected void AddPkgCount(int c) {
      pkg_count_ += c;
      this.cb_.MaxTicks = pkg_count_;
    }

    private string GetFilePath(string dir, string filename) {
      return Path.Combine(Path.GetFullPath(dir), filename);
    }

    protected void Write(string msg) {
      this.cb_.WriteLine(msg);
    }

    protected void Tick() {
      this.cb_.Tick();
    }

    protected void AddBytes(string file_path) {
      this.bytes_total_ += GetBytes(file_path);
    }

    protected long GetBytes(string file_path) {
      return new FileInfo(file_path).Length;
    }

    protected void Message(string id, Status status) {
      string status_msg = status switch {
        Status.CHECK => $"Checking {id}",
        Status.FETCH => $"Fetching {id}",
        Status.COMPLETE => "Complete!",
        _ => "Unknown"
      };
      this.cb_.Message = string.Format(
        MSG_FMT_,
        system_,
        bytes_total_ / (1024.0 * 1024.0),
        bytes_delta_ / (1024.0 * 1024.0),
        this.cb_.CurrentTick,
        pkg_count_, 
        depth_,
        status_msg
      );
    }

    ~BaseFetcher() {
      Message("", Status.COMPLETE);
    }

    protected void CreateFilePath(string file_path) {
      string dir =  Path.GetDirectoryName(file_path);
      if (!Directory.Exists(dir)) {
        Directory.CreateDirectory(dir);
      }
    }

    protected string GetOutPath(string filename) {
      return GetFilePath(this.out_dir_, filename);
    }

    protected string GetDeltaPath(string filename) {
      return GetFilePath(this.delta_dir_, filename);
    }

    protected void CopyToDelta(string filename) {
      if (!this.seeding_) {
        string out_fp = GetOutPath(filename);
        string delta_fp = GetDeltaPath(filename);
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
    
    protected bool Memorize(string id) {
      return this.found_.Add(id);
    }

    protected bool OnDisk(string path) {
      return File.Exists(path);
    }

    public abstract Task Get(string id);
  }
}
