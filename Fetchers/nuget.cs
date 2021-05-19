using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using ShellProgressBar;
using System.Linq;

namespace CloneX.Fetchers {

class Nuget : BaseFetcher  {
  public const string SYSTEM = "NUGET";
  private static readonly string REPOSITORY_URL = "https://api.nuget.org/v3/index.json";
  private SourceRepository repository_; 
  private PackageMetadataResource meta_res_;
  private FindPackageByIdResource resource_;
  private SourceCacheContext cache_;

  public Nuget(string out_dir, string delta_dir, ProgressBar pb, bool seeding = false) : base(out_dir, delta_dir, pb, SYSTEM, seeding) {
    this.repository_ = Repository.Factory.GetCoreV3(REPOSITORY_URL);
    this.meta_res_ = repository_.GetResource<PackageMetadataResource>();
    this.resource_ = repository_.GetResource<FindPackageByIdResource>();
    this.cache_ = new SourceCacheContext();
  }

  public override async Task Get(string id) {
    depth_++;
    Message(id, Status.CHECK);
    IEnumerable<IPackageSearchMetadata> pkgs = await this.GetMetadata(id);
    AddPkgCount(Enumerable.Count(pkgs));
    foreach(IPackageSearchMetadata metadata in pkgs) {
      var identity = metadata.Identity;
      /* Memorize to not check again */
      Memorize(identity.Id);
      /* Download the package */
      await GetNupkg(identity.Id, identity.Version);
      this.Tick();
      /* Get all the dependencies */
      foreach (var x in metadata.DependencySets) {
        foreach(var pkg in x.Packages){
          if (!InMemory(pkg.Id)) {
            await this.Get(pkg.Id);
          }
        }
      }
    }
    depth_--;
  }
  
  private async Task<IEnumerable<IPackageSearchMetadata>> GetMetadata(string pkg) {
    return await this.meta_res_.GetMetadataAsync(
      pkg,
      includePrerelease: true, 
      includeUnlisted: false, 
      cache_, 
      logger_, 
      ct_
    );
  }

  private async Task GetNupkg(string id, NuGetVersion version) {
    Message(id + "@" + version, Status.FETCH);
    string filename = $"{id}/{id}.{version}.nupkg";
    string out_file = this.GetOutPath(filename);
    this.CreateFilePath(out_file);
    if (OnDisk(out_file)) {
      AddBytes(out_file);
      return;
    }
    using FileStream fs = File.OpenWrite(out_file);
    await this.resource_.CopyNupkgToStreamAsync(id, version, fs, cache_, logger_, ct_);
    fs.Close();
    AddBytes(out_file);
    this.CopyToDelta(out_file);
  }
} 
}
