using Microsoft.EntityFrameworkCore.Design;

namespace Stockpile.Database {
  public class
    StockpileContextFactory : IDesignTimeDbContextFactory<StockpileContext> {
    public StockpileContext CreateDbContext(string[] args) {
      return new StockpileContext(args[0]);
    }
  }
}