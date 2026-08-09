using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ShiftYar.Infrastructure.Persistence.AppDbContext
{
    /// <summary>
    /// Used by dotnet ef / Package Manager Console (Add-Migration, Update-Database).
    /// </summary>
    public class ShiftYarDbContextFactory : IDesignTimeDbContextFactory<ShiftYarDbContext>
    {
        public ShiftYarDbContext CreateDbContext(string[] args)
        {
            var apiPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "ShiftYar.Api");
            if (!Directory.Exists(apiPath))
                apiPath = Path.Combine(Directory.GetCurrentDirectory(), "ShiftYar.Api");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(apiPath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddJsonFile("appsettings.Production.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' not found. Set it in ShiftYar.Api appsettings or environment variables.");

            var optionsBuilder = new DbContextOptionsBuilder<ShiftYarDbContext>();
            optionsBuilder.UseSqlServer(
                connectionString,
                sql => sql.CommandTimeout(600)); // 10 minutes for design-time migrations

            return new ShiftYarDbContext(optionsBuilder.Options);
        }
    }
}
