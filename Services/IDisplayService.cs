namespace Stockpile.Services {
  public interface IDisplayService {
    public void Update(DisplayInfo info);
    public void UpdateChannel();
    public void UpdatePackage(string id, string v, int c, int m);
    public void Error(string msg);
    public void AddToCount(int count);
    public void SetChannelCount(int count);
  }
}
