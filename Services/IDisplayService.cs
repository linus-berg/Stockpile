namespace Stockpile.Services {
  public enum Operation {
    INSPECT = 0,
    DOWNLOAD = 1,
    COMPLETED = 2,
    WARNING = 100,
    ERROR = 101
  }

  public interface IDisplayService {
    public void Post(string msg, Operation op);
    public void PostError(string msg);
    public void PostWarning(string msg);
    public void PostInfo(DisplayInfo info);
    public void PostDownload(string id, string v, int c, int m);
  }
}