using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Fetchers {
class Nuget : BaseFetcher  {
  private static readonly string REPOSITORY_URL = "https://api.nuget.org/v3/index.json";
  private SourceRepository repository_; 
  private FindPackageByIdResource resource_;
  private SourceCacheContext cache_;
  ILogger logger = NullLogger.Instance;
  CancellationToken ct = CancellationToken.None; 
  private List<KeyValuePair<string, NuGetVersion>> to_get_;
  public Nuget(string out_dir) : base(out_dir) {
    this.repository_ = Repository.Factory.GetCoreV3(REPOSITORY_URL);
    this.resource_ = repository_.GetResource<FindPackageByIdResource>();
    this.cache_ = new SourceCacheContext();
    to_get_ = new();
    if (!Directory.Exists(out_dir_)) {
      Directory.CreateDirectory(out_dir_);
    }
  }

  public async Task AddPackage(string package_name) {
    IEnumerable<NuGetVersion> versions = await this.resource_.GetAllVersionsAsync(package_name, cache_, logger, ct);
    foreach(NuGetVersion v in versions) {
      this.to_get_.Add(new KeyValuePair<string, NuGetVersion>(package_name, v));
      await AddPackageDependencies(package_name, v);
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
    await this.resource_.CopyNupkgToStreamAsync(package_name, version, pkg_stream, cache_, logger, ct);
  }
  
  private async Task AddPackageDependencies(string package_name, NuGetVersion version) {
    FindPackageByIdDependencyInfo info = await this.resource_.GetDependencyInfoAsync(package_name, version, cache_, logger, ct);
    foreach(var x in info.DependencyGroups) {
      foreach (var package in x.Packages) {
        await this.AddPackage(package.Id);
      }
    }
  }
} 
}