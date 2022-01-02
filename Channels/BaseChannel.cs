using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using Stockpile.Config;
using Stockpile.Database;
using Stockpile.Services;

namespace Stockpile.Channels {
  public abstract class BaseChannel {
    protected readonly Fetcher cfg_;

    /* Stockpile services */
    protected readonly DatabaseService db_;
    protected readonly IDisplayService ds_;
    protected readonly FilterService fi_;
    private readonly FileService fs_;
    private readonly Main main_cfg_;
    protected readonly MemoryService ms_;
    private readonly string[] package_list_;
    protected CancellationToken ct_ = CancellationToken.None;
    private int depth_;


    /* List of found package ids */
    protected readonly ILogger logger_ = NullLogger.Instance;
    private int max_depth_;
    private int versions_;

    protected BaseChannel(Main main_cfg, Fetcher cfg) {
      package_list_ = File.ReadAllLines(cfg.input);
      main_cfg_ = main_cfg;
      cfg_ = cfg;

      /* Services */
      db_ = DatabaseService.Open(cfg.id);
      ds_ = new ConsoleDisplayService(cfg.id);
      fs_ = new FileService(main_cfg, cfg);
      fi_ = new FilterService(main_cfg, cfg);
      ms_ = new MemoryService();
      IssueWarning();
    }

    protected int Depth {
      get => depth_;
      set {
        if (value > max_depth_) max_depth_ = value;
        depth_ = value;
      }
    }

    private void IssueWarning() {
      if (cfg_.force) ds_.Post("Force is enabled.", Operation.WARNING);
    }

    public async Task Start() {
      foreach (string id in package_list_) {
        if (!string.IsNullOrEmpty(id))
          await TryGet(id);
      }
      /* Process Ids! */
      await ProcessIds();
    }

    private async Task TryGet(string id) {
      try {
        await Get(id);
      }
      catch (Exception e) {
        ds_.PostError($"Could not fetch {id}.");
        ds_.PostError(e.ToString());
      }
    }

    public async Task BlacklistArtifact(string artifact_id, string version) {
      await db_.BlacklistArtifact(artifact_id, version);
    }

    protected void Update(string msg, Operation op) {
      ds_.PostInfo(new DisplayInfo {
        Message = msg,
        Operation = op,
        Packages = ms_.GetCount(),
        Versions = versions_,
        Depth = depth_,
        Max_Depth = max_depth_
      });
    }

    protected virtual async Task ProcessIds() {
      /* Parallel, max 5 concurrent fetchers */
      IEnumerable<Artifact> artifacts = await db_.GetArtifacts();
      /* Set the counts based on what is in database. */
      int v_c = await db_.GetArtifactVersionCount();
      int p_c = await db_.GetArtifactCount();
      versions_ = v_c;
      ms_.SetCount(p_c);
      /* Process all IDs in parallel based on configuration */
      Parallel.ForEach(artifacts, new ParallelOptions {
        MaxDegreeOfParallelism = cfg_.threading.parallel_pkg
      }, artifact => { TryProcessId(artifact).Wait(); });
      Update("", Operation.COMPLETED);
    }

    private async Task TryProcessId(Artifact artifact) {
      try {
        if (ms_.IsValid(artifact.Id)) await ProcessVersions(artifact);
      }
      catch (Exception ex) {
        ds_.PostError($"{artifact.Id} - {ex}");
      }
    }

    /* Process all versions for a package. */
    private async Task ProcessVersions(Artifact artifact) {
      List<ArtifactVersion> versions = artifact.Versions.ToList();
      for (int i = 0; i < versions.Count; i++) {
        ArtifactVersion version = versions[i];
        ds_.PostDownload(artifact.Id, version.Version, i + 1, versions.Count);
        await TryDownload(version, GetFilePath(version));
      }
    }

    /* Try download a remote file */
    private async Task TryDownload(ArtifactVersion version, string path) {
      try {
        if (string.IsNullOrEmpty(version.Url))
          throw new ArgumentNullException($"{version.ArtifactId} tarball is null.");
        await Download(version, path);
      }
      catch (Exception ex) {
        ds_.PostError($"TryDownload failed {cfg_.id}->{version.Version} - {ex}");
      }
    }

    /* Download remote file. */
    protected virtual async Task Download(ArtifactVersion version, string path) {
      string out_fp = fs_.GetMainFilePath(path);
      FileService.CreateDirectory(out_fp);
      if (FileService.OnDisk(out_fp)) {
        /* If file size == 0 is probably an error. */
        if (FileService.GetSize(out_fp) == 0)
          File.Delete(out_fp);
        else
          return;
      }

      /* If not on disk and the download succeeded */
      RemoteFile file = new(version.Url, ds_);
      if (await file.Get(out_fp))
        fs_.CopyToDelta(path);
      else
        ds_.PostError($" Download failed {cfg_.id}->{version.Url}");
    }

    protected abstract Task Get(string id);
    protected abstract string GetFilePath(ArtifactVersion version);
  }
}