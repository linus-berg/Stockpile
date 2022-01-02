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
    private int versions_;
    private int cur_tree_depth_;
    private int max_tree_depth_;

    protected BaseChannel(MainConfig main_config, ChannelConfig cfg) {
      package_list_ = File.ReadAllLines(cfg.input);
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

    private int Depth {
          get => cur_tree_depth_;
          set {
            if (value > max_tree_depth_) max_tree_depth_ = value; 
            cur_tree_depth_ = value;
          }
        }
    private void IssueWarning() {
      if (cfg_.force) ds_.Post("Force is enabled.", Operation.WARNING);
    }

    protected HashSet<string> GetAllArtifactsInMemory() {
      return ms_.GetMemory();
    }

    public async Task Start() {
      foreach (string id in package_list_) {
        await TryInspectArtifact(id);
      }
      /* Process Ids! */
      await DownloadArtifactsToDisk();
    }
    protected async Task TryInspectArtifact(string id) {
      Depth++;
      try {
        if (!ms_.Exists(id)) {
          ms_.Add(id);
          Artifact artifact = await CreateOrGetArtifact(id);
          await ProcessArtifact(artifact);
          /* Save the artifact changes */
          await db_.SaveArtifact(artifact);
        }
      } catch (Exception e) {
        Console.WriteLine(e);
      }
      Depth--;
    }

    private async Task<Artifact> CreateOrGetArtifact(string id) {
      return await db_.AddArtifact(id);
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
        if (artifact.Status != ArtifactStatus.ERROR) {
          artifact.Status = ArtifactStatus.PROCESSED;
        }
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
        MaxTreeDepth = max_tree_depth_,
        CurrentTreeDepth = cur_tree_depth_
      });
    }

    protected virtual async Task DownloadArtifactsToDisk() {
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
      }, artifact => { TryDownloadArtifact(artifact).Wait(ct_); });
      Update("", Operation.COMPLETED);
    }

    private async Task TryDownloadArtifact(Artifact artifact) {
      try {
        if (ms_.IsValid(artifact.Name)) {
          List<ArtifactVersion> versions = artifact.Versions.ToList();
          for (int i = 0; i < versions.Count; i++) {
            ArtifactVersion version = versions[i];
            if (!ShouldDownloadVersion(artifact, version)) continue;
            await TryDownloadArtifactVersion(version, GetFilePath(artifact, version));
            ds_.PostDownload(artifact.Name, version.Version, i + 1, versions.Count);
          }
        }
      }
      catch (Exception ex) {
        ds_.PostError($"{artifact.Name} - {ex}");
      }
    }

    private bool ShouldDownloadVersion(Artifact artifact, ArtifactVersion version) {
      return version.Status != ArtifactVersionStatus.BLACKLISTED &&
             fi_.Exec(artifact.Name, version.Version, 0, "");
    }

    /* Try download a remote file */
    private async Task
      TryDownloadArtifactVersion(ArtifactVersion version, string path) {
      try {
        if (string.IsNullOrEmpty(version.Url))
          throw new ArgumentNullException(
            $"{version.ArtifactId} tarball is null.");
        await DownloadArtifactVersion(version, path);
      }
      catch (Exception ex) {
        ds_.PostError(
          $"TryDownload failed {cfg_.id}->{version.Version} - {ex}");
      }
    }

    /* Download remote file. */
    private async Task DownloadArtifactVersion(ArtifactVersion version, string path) {
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