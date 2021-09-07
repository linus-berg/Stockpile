using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ShellProgressBar;


namespace Stockpile.Channels {
internal class RemoteFile {
  private static readonly HttpClient CLIENT_ = new HttpClient();
  private static readonly ProgressBarOptions bar_opts_ = new ProgressBarOptions {
    CollapseWhenFinished = true,
    ProgressCharacter = '─'
  };
  private readonly string URL_;
  private readonly IProgressBar BAR_;
  const int BUFFER_SIZE = 8192;
  
  public RemoteFile(string url, IProgressBar bar) {
    URL_ = url;
    BAR_ = bar;
  }

  public async Task<bool> Get(string filepath) {
    HttpResponseMessage response = await CLIENT_.GetAsync(URL_, HttpCompletionOption.ResponseHeadersRead);
    if (response.Content.Headers.ContentLength == null) {
      return false;
    }
    long size = (long)response.Content.Headers.ContentLength;
    using Stream s = await response.Content.ReadAsStreamAsync();
    try {
      await ProcessStream(s, (int)size, filepath);
    } catch (Exception e) {
      BAR_.WriteErrorLine($"Error {URL_}:{e}");
    } finally {
      s.Close();
    }
    return true;
  }

  private async Task ProcessStream(Stream s, int size, string filepath) {
    using FileStream fs = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, true);

    /* Progress */
    IProgressBar bar = BAR_.Spawn(size, URL_, bar_opts_);
    IProgress<double> progress = bar.AsProgress<double>();
    int total = 0;
    int read = -1;
    byte[] buffer = new byte[BUFFER_SIZE];
    while (read != 0) {
      read = await s.ReadAsync(buffer, 0, buffer.Length);
      total += read;
      progress.Report(1.0 * total / size); 
      if (read == 0) {
        break;
      }
      await fs.WriteAsync(buffer, 0, read);
    }
    fs.Close();
  }
}
}
