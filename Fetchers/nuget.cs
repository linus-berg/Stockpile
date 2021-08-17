using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using ShellProgressBar;

namespace Stockpile.Fetchers {

  class Nuget : BaseFetcher {
    private static readonly string REPOSITORY_URL = "https://api.nuget.org/v3/index.json";
    private SourceRepository repository_;
    private PackageMetadataResource meta_res_;
    private FindPackageByIdResource resource_;
    private SourceCacheContext cache_;

    public Nuget(Config.Main main_cfg, Config.Fetcher cfg) : base(main_cfg, cfg) {
      repository_ = Repository.Factory.GetCoreV3(REPOSITORY_URL);
      meta_res_ = repository_.GetResource<PackageMetadataResource>();
      resource_ = repository_.GetResource<FindPackageByIdResource>();
      cache_ = new SourceCacheContext();
    }

    public override void Get(string id) {
      SetText($"Scanning {id}");
      Depth++;
      /* Memorize to not check again */
      Memorize(id);
      var versions = GetMetadata(id);
      AddToVersionCount(Enumerable.Count(versions));
      foreach (var version in versions) {
        var v_str = version.Identity.Version.ToString();
        var db_pkg = db_.GetPackage(id, v_str);
        if (db_pkg != null && db_pkg.IsProcessed()) {
          continue;
        } else if (db_pkg == null) {
          db_.AddPackage(id, v_str, "");
        }
        AddTransient(version.DependencySets);
        /* Set dependency has been processed */
        db_.SetProcessed(id, v_str);
      }
      Depth--;
    }

    public override void ProcessIds() {
      /* Parallel, max 5 concurrent fetchers */
      var ids = (List<string>)db_.GetAllPackages();
      SetVersionCount(db_.GetVersionCount());
      SetPackageCount(db_.GetPackageCount());
      bar_.MaxTicks = ids.Count;
      SetText($"Downloading");
      Parallel.ForEach(ids, po_, (id) => {
        try {
          bar_.Tick();
          main_bar_.Tick();
          if (IsValid(id)) {
            ProcessVersions(id).Wait();
          }
        } catch (Exception ex) {
          bar_.WriteErrorLine($"Error [{id}] - {ex}");
        }
      });
      SetText($"Completed");
    }

    private async Task ProcessVersions(string id) {
      var pkgs = (List<DBPackage>)db_.GetAllToDownload(id);
      using var bar = bar_.Spawn(pkgs.Count, id, bar_opts_);
      foreach (var pkg in pkgs) {
        bar.Tick($"{id}@{pkg.version}");
        await GetNupkg(pkg.id, pkg.version);
      }
    }

    private void AddTransient(IEnumerable<PackageDependencyGroup> deps) {
      foreach (var x in deps) {
        foreach (var pkg in x.Packages) {
          if (!InMemory(pkg.Id)) {
            Get(pkg.Id);
          }
        }
      }
    }

    private IEnumerable<IPackageSearchMetadata> GetMetadata(string id) {
      return meta_res_.GetMetadataAsync(
        id,
        includePrerelease: true,
        includeUnlisted: false,
        cache_,
        logger_,
        ct_
      ).Result;
    }

    private async Task GetNupkg(string id, string version) {
      var filename = $"{id}/{id}.{version}.nupkg";
      var out_file = GetOutFilePath(filename);
      CreateFilePath(out_file);
      if (OnDisk(out_file)) {
        return;
      }
      using var fs = File.OpenWrite(out_file);
      var v = new NuGetVersion(version);
      await resource_.CopyNupkgToStreamAsync(id, v, fs, cache_, logger_, ct_);
      fs.Close();
      CopyToDelta(filename);
    }
  }
}
