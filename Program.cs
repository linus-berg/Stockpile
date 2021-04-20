using System;
using System.Threading.Tasks;
using Fetchers;
namespace CloneX {
  class Program {
    static async Task Main(string[] args) {
      Console.WriteLine("Hello World!");
      await Run();
    }

    static async Task Run() {
      Nuget nuget = new("./out");
      await nuget.Fetch("ClosedXML");
    }
  }
}
