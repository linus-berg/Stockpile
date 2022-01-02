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
    private readonly Stack<Artifact> artifact_stack_;
    protected readonly ChannelConfig cfg_;

    /* Stockpile services */
    protected readonly DatabaseService db_;
    protected readonly IDisplayService ds_;
    private readonly FilterService fi_;
    private readonly FileService fs_;


    /* List of found package ids */
    protected readonly ILogger logger_ = NullLogger.Instance;
    private readonly MainConfig main_config_;
    private readonly MemoryService ms_;
    private readonly string[] package_list_;
    protected CancellationToken ct_ = CancellationToken.None;
    private int depth_;
    private int max_depth_;
    private int versions_;

    protected BaseChannel(MainConfig main_config, ChannelConfig cfg) {
      package_list_ = File.ReadAllLines(cfg.input);
      artifact_stack_ = new Stack<Artifact>();
      main_config_ = main_config;
      cfg_ = cfg;

      /* Services */
      db_ = DatabaseService.Open(cfg.id);
      ds_ = new ConsoleDisplayService(cfg.id);
      fs_ = new FileService(main_config, cfg);
      fi_ = new FilterService(main_config, cfg);
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

    protected HashSet<string> GetAllArtifactsInMemory() {
      return ms_.GetMemory();
    }

    public async Task Start() {
      await LoadArtifactsIntoMemory();      
      
      while (artifact_stack_.Count > 0) {
        Artifact artifact = artifact_stack_.Pop();
        await ProcessArtifact(artifact);
      }

      /* Process Ids! */
      await ProcessAllArtifacts();
    }

    private async Task LoadArtifactsIntoMemory() {
      IEnumerable<Artifact> artifacts_in_db = await db_.GetArtifacts();
      foreach (Artifact artifact in artifacts_in_db) {
        AddArtifactToStack(artifact);
      }
      /* Add the initial list */
      foreach (string id in package_list_) await AddArtifactIdToStack(id);
    }

    protected async Task AddArtifactIdToStack(string id) {
      if (ms_.Exists(id)) return;
      ms_.Add(id);
      artifact_stack_.Push(await db_.AddArtifact(id));
    }
    
    private void AddArtifactToStack(Artifact artifact) {
      if (ms_.Exists(artifact.Name)) return;
      ms_.Add(artifact.Name);
      artifact_stack_.Push(artifact);
    }

    protected void SetArtifactError(Artifact artifact) {
      ms_.SetError(artifact.Name);
      artifact.Status = ArtifactStatus.ERROR;
    }

    private async Task ProcessArtifact(Artifact artifact) {
      try {
        Update(artifact.Name, Operation.INSPECT);
        /* Add the versions from the external source */
        await InspectArtifact(artifact);
        /* Save the artifact changes */
        await db_.SaveArtifact(artifact);
      }
      catch (Exception e) {
        ds_.PostError($"Could not fetch {artifact.Name}.");
        ds_.PostError(e.ToString());
      }
    }

    private void Update(string msg, Operation op) {
      ds_.PostInfo(new DisplayInfo {
        Message = msg,
        Operation = op,
        Packages = ms_.GetCount(),
        Versions = versions_,
        Depth = depth_,
        Max_Depth = max_depth_
      });
    }

    protected virtual async Task ProcessAllArtifacts() {
      /* Parallel, max 5 concurrent fetchers */
      IEnumerable<Artifact> artifacts = await db_.GetArtifacts();
      /* Set the counts based on what is in database. */
      int p_c = await db_.GetArtifactCount();
      int v_c = await db_.GetArtifactVersionCount();
      versions_ = v_c;
      ms_.SetCount(p_c);
      /* Process all IDs in parallel based on configuration */
      Parallel.ForEach(artifacts, new ParallelOptions {
        MaxDegreeOfParallelism = cfg_.threads.parallel_pkg
      }, artifact => { TryProcessArtifact(artifact).Wait(ct_); });
      Update("", Operation.COMPLETED);
    }

    private async Task TryProcessArtifact(Artifact artifact) {
      try {
        if (ms_.IsValid(artifact.Name)) await ProcessArtifactVersions(artifact);
      }
      catch (Exception ex) {
        ds_.PostError($"{artifact.Name} - {ex}");
      }
    }

    /* Process all versions for a package. */
    private async Task ProcessArtifactVersions(Artifact artifact) {
      List<ArtifactVersion> versions = artifact.Versions.ToList();
      for (int i = 0; i < versions.Count; i++) {
        ArtifactVersion version = versions[i];
        if (version.Status == ArtifactVersionStatus.BLACKLISTED) continue;
        if (!fi_.Exec(artifact.Name, version.Version, 0, "")) continue;
        ds_.PostDownload(artifact.Name, version.Version, i + 1, versions.Count);
        await TryDownloadArtifact(version, GetFilePath(artifact, version));
      }
    }

    /* Try download a remote file */
    private async Task
      TryDownloadArtifact(ArtifactVersion version, string path) {
      try {
        if (string.IsNullOrEmpty(version.Url))
          throw new ArgumentNullException(
            $"{version.ArtifactId} tarball is null.");
        await DownloadArtifact(version, path);
      }
      catch (Exception ex) {
        ds_.PostError(
          $"TryDownload failed {cfg_.id}->{version.Version} - {ex}");
      }
    }

    /* Download remote file. */
    private async Task DownloadArtifact(ArtifactVersion version, string path) {
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

    protected abstract Task InspectArtifact(Artifact artifact);

    protected abstract string GetFilePath(Artifact artifact,
      ArtifactVersion version);
  }
}