using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Packaging;
using NuGet.Versioning;
using System.Linq;

namespace Stockpile.Fetchers {

class Nuget : BaseFetcher  {
  private static readonly string REPOSITORY_URL = "https://api.nuget.org/v3/index.json";
  private SourceRepository repository_; 
  private PackageMetadataResource meta_res_;
  private FindPackageByIdResource resource_;
  private SourceCacheContext cache_;

  public Nuget(Database db, Config.Fetcher cfg, DateTime runtime, bool seeding = false) : base(db, cfg, runtime, seeding) {
    this.repository_ = Repository.Factory.GetCoreV3(REPOSITORY_URL);
    this.meta_res_ = repository_.GetResource<PackageMetadataResource>();
    this.resource_ = repository_.GetResource<FindPackageByIdResource>();
    this.cache_ = new SourceCacheContext();
  }

  public override void Get(string id) {
    depth_++;
    SetStatus(id, Status.CHECK);
    /* Memorize to not check again */
    Memorize(id);
    IEnumerable<IPackageSearchMetadata> versions = this.GetMetadata(id);
    AddPkgCount(Enumerable.Count(versions));
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
    depth_--;
  }

  public override void ProcessIds() {
    /* Parallel, max 5 concurrent fetchers */
    IEnumerable<string> ids = db_.GetAllPackages();
    Parallel.ForEach(ids, po_, (id) => {
      try {
        IEnumerable<DBPackage> pkgs = this.db_.GetAllToDownload(id);
        foreach (DBPackage pkg in pkgs) {
          this.GetNupkg(pkg.id, pkg.version);
        }
      } catch (Exception) {
        // Processing error for {id}, for now ignore.
      }
    });
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
    SetStatus($"{id}@{version}", Status.FETCH);
    string filename = $"{id}/{id}.{version}.nupkg";
    string out_file = this.GetOutFilePath(filename);
    this.CreateFilePath(out_file);
    if (OnDisk(out_file)) {
      AddBytes(out_file);
      return;
    }
    using FileStream fs = File.OpenWrite(out_file);
    NuGetVersion v = new NuGetVersion(version);
    Task t = this.resource_.CopyNupkgToStreamAsync(id, v, fs, cache_, logger_, ct_);
    Task.WaitAll(t);
    fs.Close();
    AddBytes(out_file);
    this.CopyToDelta(out_file);
  }
} 
}
