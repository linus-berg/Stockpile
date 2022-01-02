using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Stockpile.Config;
using Stockpile.Database;

namespace Stockpile.Channels {
  internal class Nuget : BaseChannel {
    private const string API_ = "https://api.nuget.org/v3/index.json";
    private const string NUGET_ = "https://api.nuget.org/v3-flatcontainer/";
    private readonly SourceCacheContext cache_;
    private readonly PackageMetadataResource meta_res_;
    private readonly SourceRepository repository_;
    private FindPackageByIdResource resource_;

    public Nuget(MainConfig main_config, ChannelConfig cfg) : base(main_config, cfg) {
      repository_ = Repository.Factory.GetCoreV3(API_);
      meta_res_ = repository_.GetResource<PackageMetadataResource>();
      resource_ = repository_.GetResource<FindPackageByIdResource>();
      cache_ = new SourceCacheContext();
    }

    protected override string GetFilePath(Artifact artifact,
      ArtifactVersion version) {
      return $"{artifact.Name}/{artifact.Name}.{version.Version}.nupkg";
    }

    protected override async Task InspectArtifact(Artifact artifact) {
      IEnumerable<IPackageSearchMetadata> versions =
        await GetMetadata(artifact.Name);
      foreach (IPackageSearchMetadata version in versions) {
        string v = version.Identity.Version.ToString();
        string u = NUGET_ + $"{artifact.Name}/{v}/{artifact.Name}.{v}.nupkg";
        ArtifactVersion a_v = artifact.AddVersionIfNotExists(v, u);
        if (!a_v.ShouldProcess()) continue;
        await ProcessArtifactDependencies(version.DependencySets);
        /* Set dependency has been processed */
        a_v.SetStatus(ArtifactVersionStatus.PROCESSED);
      }
    }

    private async Task ProcessArtifactDependencies(
      IEnumerable<PackageDependencyGroup> deps) {
      foreach (PackageDependencyGroup x in deps)
      foreach (PackageDependency pkg in x.Packages)
        await TryInspectArtifact(pkg.Id);
    }

    private async Task<IEnumerable<IPackageSearchMetadata>> GetMetadata(
      string id) {
      return await meta_res_.GetMetadataAsync(
        id,
        true,
        false,
        cache_,
        logger_,
        ct_
      );
    }
  }
}