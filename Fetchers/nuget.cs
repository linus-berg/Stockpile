using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Fetchers {
class Nuget : BaseFetcher  {
  private static readonly string REPOSITORY_URL = "https://api.nuget.org/v3/index.json";
  private SourceRepository repository_; 
  private FindPackageByIdResource resource_;
  private SourceCacheContext cache_;
  private List<KeyValuePair<string, NuGetVersion>> to_get_;
  private HashSet<string> found_;
  public Nuget(string out_dir) : base(out_dir) {
    this.repository_ = Repository.Factory.GetCoreV3(REPOSITORY_URL);
    this.resource_ = repository_.GetResource<FindPackageByIdResource>();
    this.cache_ = new SourceCacheContext();
    this.found_ = new();
    if (!Directory.Exists(out_dir_)) {
      Directory.CreateDirectory(this.out_dir_);
    }
  }
  public async Task Get(string package_name) {
    this.to_get_ = new();
    await this.AddAllPackageVersions(package_name);
  }


  private async Task AddAllPackageVersions(string package_name) {
    if (this.found_.Contains(package_name)) {
      return;
    }

    IEnumerable<NuGetVersion> versions = await this.resource_.GetAllVersionsAsync(package_name, cache_, logger_, ct_);
    foreach(NuGetVersion v in versions) {
      this.to_get_.Add(new KeyValuePair<string, NuGetVersion>(package_name, v));
      this.found_.Add(package_name); 
      await AddPackageDependencies(package_name, v);
      Console.WriteLine("ADDING: " + package_name + "@" + v.ToString());
    }
  }

  private async Task StartProcessing() {
    foreach (KeyValuePair<string, NuGetVersion> kv in to_get_) {
      await this.GetPackage(kv.Key, kv.Value);
    }
  }

  private async Task GetPackage(string package_name, NuGetVersion version) {
    string filename = package_name.ToLower() + "." + version.ToString();
    using FileStream pkg_stream = File.OpenWrite($"{out_dir_}/{package_name}/{filename}.nupkg");
    await this.resource_.CopyNupkgToStreamAsync(package_name, version, pkg_stream, cache_, logger_, ct_);
  }
  
  private async Task AddPackageDependencies(string package_name, NuGetVersion version) {
    FindPackageByIdDependencyInfo info = await this.resource_.GetDependencyInfoAsync(package_name, version, cache_, logger_, ct_);
    foreach(var x in info.DependencyGroups) {
      foreach (var package in x.Packages) {
        await this.AddAllPackageVersions(package.Id);
      }
    }
  }
} 
}