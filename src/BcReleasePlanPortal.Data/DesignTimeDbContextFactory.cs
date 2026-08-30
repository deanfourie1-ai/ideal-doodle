using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BcReleasePlanPortal.Data;

/// <summary>Lets `dotnet ef migrations add` build a DbContext without spinning up the Worker host.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BcReleasePlanDbContext>
{
    public BcReleasePlanDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BcReleasePlanDbContext>();
        optionsBuilder.UseSqlite("Data Source=bcreleaseplan.design.db");
        return new BcReleasePlanDbContext(optionsBuilder.Options);
    }
}
