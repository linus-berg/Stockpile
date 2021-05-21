using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Packaging;
using NuGet.Versioning;
using System.Linq;

namespace CloneX.Fetchers {

class Nuget : BaseFetcher  {
  public const string SYSTEM = "NUGET";
  private static readonly string REPOSITORY_URL = "https://api.nuget.org/v3/index.json";
  private SourceRepository repository_; 
  private PackageMetadataResource meta_res_;
  private FindPackageByIdResource resource_;
  private SourceCacheContext cache_;

  public Nuget(string out_dir, string delta_dir, DateTime runtime, bool seeding = false) : base(out_dir, delta_dir, SYSTEM, runtime, seeding) {
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
      this.AddTransient(version.DependencySets);
    }
    depth_--;
  }

  public void ProcessIds() {
    /* Parallel, max 5 concurrent fetchers */
    Parallel.ForEach(this.GetMemory(), po, (id) => {
      try {
        IEnumerable<IPackageSearchMetadata> versions = this.GetMetadata(id);
        foreach (IPackageSearchMetadata version in versions) {
          this.GetNupkg(version.Identity.Id, version.Identity.Version);
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

  private void GetNupkg(string id, NuGetVersion version) {
    SetStatus($"{id}@{version}", Status.FETCH);
    string filename = $"{id}/{id}.{version}.nupkg";
    string out_file = this.GetOutFilePath(filename);
    this.CreateFilePath(out_file);
    if (OnDisk(out_file)) {
      AddBytes(out_file);
      return;
    }
    using FileStream fs = File.OpenWrite(out_file);
    Task t = this.resource_.CopyNupkgToStreamAsync(id, version, fs, cache_, logger_, ct_);
    Task.WaitAll(t);
    fs.Close();
    AddBytes(out_file);
    this.CopyToDelta(out_file);
  }
} 
}
