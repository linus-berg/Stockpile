using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace CloneX.Fetchers {

class Nuget : BaseFetcher  {
  private static readonly string REPOSITORY_URL = "https://api.nuget.org/v3/index.json";
  private SourceRepository repository_; 
  private PackageMetadataResource meta_res_;
  private FindPackageByIdResource resource_;
  private SourceCacheContext cache_;
  private HashSet<string> found_;
  private bool seeding_;

  public Nuget(string out_dir, string delta_dir, bool seed = false) : base(out_dir, delta_dir) {
    this.repository_ = Repository.Factory.GetCoreV3(REPOSITORY_URL);
    this.meta_res_ = repository_.GetResource<PackageMetadataResource>();
    this.resource_ = repository_.GetResource<FindPackageByIdResource>();
    this.cache_ = new SourceCacheContext();
    this.found_ = new();
    this.seeding_ = seed;
  }

  public async Task Get(string id) {
    if (this.found_.Contains(id)) {
      return;
    }

    IEnumerable<IPackageSearchMetadata> pkgs = await this.GetMetadata(id);
    foreach(IPackageSearchMetadata metadata in pkgs) {
      var identity = metadata.Identity;
      this.found_.Add(identity.Id); 
      /* Download the package */
      await GetPackage(identity.Id, identity.Version);
      /* Get all the dependencies */
      foreach (var x in metadata.DependencySets) {
        foreach(var pkg in x.Packages){
          await this.Get(pkg.Id);
        }
      }
    }
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

  private async Task GetPackage(string id, NuGetVersion version) {
    string filename = $"{id}.{version}.nupkg";
    string f_path = this.GetOutPath(filename);
    if (!File.Exists(f_path)) {
      using FileStream pkg_stream = File.OpenWrite(f_path);
      await this.resource_.CopyNupkgToStreamAsync(id, version, pkg_stream, cache_, logger_, ct_);
      pkg_stream.Close();
      if (!this.seeding_) {
        File.Copy(f_path, this.GetDeltaPath(filename));
      }
    } else {
      Console.WriteLine($"{f_path} Not Part Of Delta!");
    }
  }
} 
}
