using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using Stockpile.Config;
using Stockpile.Constants;
using Stockpile.Infrastructure.Entities;
using Stockpile.Services;

namespace Stockpile.Channels {
  public abstract class Channel {
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
    private int cur_tree_depth_;
    private int max_tree_depth_;
    private int versions_;

    protected Channel(MainConfig main_config, ChannelConfig cfg) {
      package_list_ = File.ReadAllLines(cfg.input);
      main_config_ = main_config;
      cfg_ = cfg;
      /* Services */
      db_ = DatabaseService.Open(cfg.id);
      ds_ = new ConsoleDisplayService(cfg.id);
      fs_ = new FileService(main_config, cfg);
      fi_ = new FilterService(main_config, cfg);
      ms_ = new MemoryService();
      
      /* If this channel has warnings due to configuration */
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
      foreach (string id in package_list_) await TryInspectArtifact(id);
      /* Process Ids! */
      await DownloadArtifactsToDisk();
    }

    protected async Task TryInspectArtifact(string id) {
      Depth++;
      try {
        if (!ms_.Exists(id)) {
          ms_.Add(id);
          Artifact artifact = await CreateOrGetArtifact(id);
          /* Add the versions from the external source */
          Update(artifact.Name, Operation.INSPECT);
          
          /* This is implemented by individual channels */
          await InspectArtifact(artifact);
          /* --- */
          
          if (artifact.Status != ArtifactStatus.ERROR)
            artifact.Status = ArtifactStatus.PROCESSED;
          /* Save the artifact changes */
          await db_.SaveArtifact(artifact);
        }
      }
      catch (Exception e) {
        ds_.PostError($"Could not fetch {id}.");
        ds_.PostError(e.ToString());
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
      ms_.SetCount(await db_.GetArtifactCount());
      versions_ = await db_.GetArtifactVersionCount();
      /* Process all IDs in parallel based on configuration */
      Parallel.ForEach(artifacts, new ParallelOptions {
        MaxDegreeOfParallelism = cfg_.threads.parallel_pkg
      }, artifact => { DownloadAllVersions(artifact).Wait(ct_); });
      Update("", Operation.COMPLETED);
    }

    private async Task DownloadAllVersions(Artifact artifact) {
      try {
        if (ms_.IsValid(artifact.Name)) {
          List<ArtifactVersion> versions = artifact.Versions.ToList();
          int count = versions.Count;
          for (int i = 0; i < count; i++) {
            ArtifactVersion version = versions[i];
            if (!ShouldDownloadVersion(artifact, version)) continue;
            await TryDownloadVersion(version, GetDepositPath(artifact, version));
            ds_.PostDownload(artifact, version, i + 1, count);
          }
        }
      }
      catch (Exception ex) {
        ds_.PostError($"{artifact.Name} - {ex}");
      }
    }

    private bool ShouldDownloadVersion(Artifact artifact,
      ArtifactVersion version) {
      return !version.IsBlacklisted() &&
             fi_.Exec(artifact.Name, version.Version, 0, "");
    }

    private async Task
      TryDownloadVersion(ArtifactVersion version, string path) {
      try {
        if (string.IsNullOrEmpty(version.Url))
          throw new ArgumentNullException(
            $"{version.ArtifactId} tarball is null.");
        await DownloadVersion(version, path);
      }
      catch (Exception ex) {
        ds_.PostError(
          $"TryDownload failed {cfg_.id}->{version.Version} - {ex}");
      }
    }

    /* Download remote file. */
    private async Task DownloadVersion(ArtifactVersion version, string path) {
      string out_fp = fs_.GetMainFilePath(path);
      FileService.CreateDirectory(out_fp);
      if (FileService.OnDisk(out_fp)) {
        /* If file size == 0 is probably an error. */
        if (FileService.GetSize(out_fp) == 0)
          File.Delete(out_fp);
        else
          return;
      }

      RemoteFile file = new(version.Url, ds_);
      if (await file.Get(out_fp))
        fs_.CopyToDelta(path);
      else
        ds_.PostError($" Download failed {cfg_.id}->{version.Url}");
    }
  
    /* Required to be overriden by each channel. */
    protected abstract Task InspectArtifact(Artifact artifact);
    protected abstract string GetDepositPath(Artifact artifact,
      ArtifactVersion version);
  }
}
