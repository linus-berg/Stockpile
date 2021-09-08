using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Stockpile.Channels {

  class Nuget : BaseChannel {
    private const string API_ = "https://api.nuget.org/v3/index.json";
    private const string NUGET_ = "https://api.nuget.org/v3-flatcontainer/";
    private SourceRepository repository_;
    private PackageMetadataResource meta_res_;
    private FindPackageByIdResource resource_;
    private SourceCacheContext cache_;

    public Nuget(Config.Main main_cfg, Config.Fetcher cfg) : base(main_cfg, cfg) {
      repository_ = Repository.Factory.GetCoreV3(API_);
      meta_res_ = repository_.GetResource<PackageMetadataResource>();
      resource_ = repository_.GetResource<FindPackageByIdResource>();
      cache_ = new SourceCacheContext();
    }

    protected override string GetFilePath(DBPackage pkg) {
      return $"{pkg.id}/{pkg.id}.{pkg.version}.nupkg";
    }
    
    public override async Task Get(string id) {
      SetText($"Scanning {id}");
      Depth++;
      /* Memorize to not check again */
      memory_.Add(id);
      var versions = await GetMetadata(id);
      AddToVersionCount(Enumerable.Count(versions));
      foreach (var version in versions) {
        string v = version.Identity.Version.ToString();
        string u = NUGET_ + $"{id}/{v}/{id}.{v}.nupkg";
        if (IsProcessed(id, v, u)) {
          continue;
        };
        await AddTransient(version.DependencySets);
        /* Set dependency has been processed */
        db_.SetProcessed(id, v);
      }
      Depth--;
    }

    private async Task AddTransient(IEnumerable<PackageDependencyGroup> deps) {
      foreach (var x in deps) {
        foreach (var pkg in x.Packages) {
          if (!memory_.Exists(pkg.Id)) {
            await Get(pkg.Id);
          }
        }
      }
    }

    private async Task<IEnumerable<IPackageSearchMetadata>> GetMetadata(string id) {
      return await meta_res_.GetMetadataAsync(
        id,
        includePrerelease: true,
        includeUnlisted: false,
        cache_,
        logger_,
        ct_
      );
    }
  }
}
