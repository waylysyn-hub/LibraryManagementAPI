using Data;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementAPI.Tests.TestHelpers;

public static class DbContextFactory
{
    public static BankDbContext Create()
    {
        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        var ctx = new BankDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }
}
