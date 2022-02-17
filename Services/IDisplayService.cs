using Stockpile.Constants;
using Stockpile.Infrastructure.Entities;

namespace Stockpile.Services {
  public interface IDisplayService {
    public void Post(string msg, Operation op);
    public void PostError(string msg);
    public void PostWarning(string msg);
    public void PostInfo(DisplayInfo info);
    public void PostDownload(Artifact artifact, ArtifactVersion version, int c, int m);
  }
}