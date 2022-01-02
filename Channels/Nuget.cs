using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Stockpile.Config;
using Stockpile.Database;
using Stockpile.Services;

namespace Stockpile.Channels {
  internal class Nuget : BaseChannel {
    private const string API_ = "https://api.nuget.org/v3/index.json";
    private const string NUGET_ = "https://api.nuget.org/v3-flatcontainer/";
    private readonly SourceCacheContext cache_;
    private readonly PackageMetadataResource meta_res_;
    private readonly SourceRepository repository_;
    private FindPackageByIdResource resource_;

    public Nuget(Main main_cfg, Fetcher cfg) : base(main_cfg, cfg) {
      repository_ = Repository.Factory.GetCoreV3(API_);
      meta_res_ = repository_.GetResource<PackageMetadataResource>();
      resource_ = repository_.GetResource<FindPackageByIdResource>();
      cache_ = new SourceCacheContext();
    }

    protected override string GetFilePath(ArtifactVersion version) {
      return $"{version.ArtifactId}/{version.ArtifactId}.{version.Version}.nupkg";
    }

    protected override async Task Get(string id) {
      Update(id, Operation.INSPECT);
      Depth++;
      /* Memorize to not check again */
      ms_.Add(id);
      IEnumerable<IPackageSearchMetadata> versions = await GetMetadata(id);

      Artifact artifact = await db_.GetArtifact(id);
      
      foreach (IPackageSearchMetadata version in versions) {
        string v = version.Identity.Version.ToString();
        string u = NUGET_ + $"{id}/{v}/{id}.{v}.nupkg";
        if (artifact.IsVersionProcessed(v)) {
          continue;
        }
        await AddTransient(version.DependencySets);
        /* Set dependency has been processed */
        artifact.SetVersionAsProcessed(v, u);
      }
      db_.SaveArtifact(artifact);
      Depth--;
    }

    private async Task AddTransient(IEnumerable<PackageDependencyGroup> deps) {
      foreach (PackageDependencyGroup x in deps)
      foreach (PackageDependency pkg in x.Packages)
        if (!ms_.Exists(pkg.Id))
          await Get(pkg.Id);
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