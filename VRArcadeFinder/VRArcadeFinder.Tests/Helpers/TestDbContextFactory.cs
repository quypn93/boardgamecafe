using Microsoft.EntityFrameworkCore;
using VRArcadeFinder.Data;

namespace VRArcadeFinder.Tests.Helpers
{
    public static class TestDbContextFactory
    {
        public static ApplicationDbContext Create(string? dbName = null)
        {
            dbName ??= Guid.NewGuid().ToString();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }
    }
}
