using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Packaging;
using NuGet.Versioning;
using System.Linq;
using ShellProgressBar;

namespace Stockpile.Fetchers {

class Nuget : BaseFetcher  {
  private static readonly string REPOSITORY_URL = "https://api.nuget.org/v3/index.json";
  private SourceRepository repository_; 
  private PackageMetadataResource meta_res_;
  private FindPackageByIdResource resource_;
  private SourceCacheContext cache_;

  public Nuget(Config.Main main_cfg, Config.Fetcher cfg) : base(main_cfg, cfg) {
    this.repository_ = Repository.Factory.GetCoreV3(REPOSITORY_URL);
    this.meta_res_ = repository_.GetResource<PackageMetadataResource>();
    this.resource_ = repository_.GetResource<FindPackageByIdResource>();
    this.cache_ = new SourceCacheContext();
  }

  public override void Get(string id) {
    SetText($"Scanning {id}");
    Depth++;
    /* Memorize to not check again */
    Memorize(id);
    IEnumerable<IPackageSearchMetadata> versions = this.GetMetadata(id);
    AddToVersionCount(Enumerable.Count(versions));
    foreach(IPackageSearchMetadata version in versions) {
      string v_str = version.Identity.Version.ToString();
      DBPackage db_pkg = db_.GetPackage(id, v_str);
      if (db_pkg != null && db_pkg.IsProcessed()) {
        continue;
      } else if (db_pkg == null) {
        this.db_.AddPackage(id, v_str, ""); 
      }
      this.AddTransient(version.DependencySets);
      /* Set dependency has been processed */
      this.db_.SetProcessed(id, v_str);
    }
    Depth--;
  }

  public override void ProcessIds() {
    /* Parallel, max 5 concurrent fetchers */
    List<string> ids = (List<string>)db_.GetAllPackages();
    SetVersionCount(this.db_.GetVersionCount());
    SetPackageCount(this.db_.GetPackageCount());
    bar_.MaxTicks = ids.Count;
    SetText($"Downloading");
    Parallel.ForEach(ids, po_, (id) => {
      try {
        bar_.Tick();
        main_bar_.Tick();
        ProcessVersions(id);
      } catch (Exception) {
        // Processing error for {id}, for now ignore.
      }
    });
    SetText($"Completed");
  }
  
  private void ProcessVersions(string id) {
    List<DBPackage> pkgs = (List<DBPackage>)this.db_.GetAllToDownload(id);
    using ChildProgressBar bar = bar_.Spawn(pkgs.Count, id, bar_opts_);
    foreach (DBPackage pkg in pkgs) {
      bar.Tick($"{id}@{pkg.version}");
      this.GetNupkg(pkg.id, pkg.version);
    }
  }

  private void AddTransient(IEnumerable<PackageDependencyGroup> deps) {
    foreach (var x in deps) {
      foreach(var pkg in x.Packages){
        if (!InMemory(pkg.Id)) {
          this.Get(pkg.Id);
        }
      }
    }
  }
  
  private IEnumerable<IPackageSearchMetadata> GetMetadata(string id) {
    return this.meta_res_.GetMetadataAsync(
      id,
      includePrerelease: true, 
      includeUnlisted: false, 
      cache_, 
      logger_, 
      ct_
    ).Result;
  }

  private void GetNupkg(string id, string version) {
    string filename = $"{id}/{id}.{version}.nupkg";
    string out_file = this.GetOutFilePath(filename);
    this.CreateFilePath(out_file);
    if (OnDisk(out_file)) {
      return;
    }
    using FileStream fs = File.OpenWrite(out_file);
    NuGetVersion v = new NuGetVersion(version);
    Task t = this.resource_.CopyNupkgToStreamAsync(id, v, fs, cache_, logger_, ct_);
    Task.WaitAll(t);
    fs.Close();
    this.CopyToDelta(filename);
  }
} 
}
