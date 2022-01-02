namespace Stockpile.Services {
  public interface IDisplayService {
    public void Post(string msg, Operation op);
    public void PostError(string msg);
    public void PostWarning(string msg);
    public void PostInfo(DisplayInfo info);
    public void PostDownload(string id, string v, int c, int m);
  }
}