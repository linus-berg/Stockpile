using Stockpile.Constants;

namespace Stockpile.Services {
  public class DisplayInfo {
    public string Message { get; set; }
    public Operation Operation { get; set; }
    public int Packages { get; set; }
    public int Versions { get; set; }
    public int MaxTreeDepth { get; set; }
    public int CurrentTreeDepth { get; set; }
  }
}