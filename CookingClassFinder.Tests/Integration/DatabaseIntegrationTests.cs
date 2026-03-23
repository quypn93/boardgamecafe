using CookingClassFinder.Data;
using CookingClassFinder.Models;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Models.DTOs;
using CookingClassFinder.Services;
using CookingClassFinder.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CookingClassFinder.Tests.Integration;

public class DatabaseIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public DatabaseIntegrationTests()
    {
        _context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedTestDataAsync(_context).GetAwaiter().GetResult();
    }

    public void Dispose() => _context.Dispose();

    #region Entity Relationships

    [Fact]
    public async Task School_HasClasses_RelationshipWorks()
    {
        var school = await _context.Schools
            .Include(s => s.Classes)
            .FirstAsync(s => s.SchoolId == 1);

        Assert.Equal(3, school.Classes.Count); // 2 active + 1 inactive
    }

    [Fact]
    public async Task School_HasReviews_RelationshipWorks()
    {
        var school = await _context.Schools
            .Include(s => s.Reviews)
            .FirstAsync(s => s.SchoolId == 1);

        Assert.Equal(3, school.Reviews.Count); // 2 approved + 1 pending
    }

    [Fact]
    public async Task City_HasCrawlHistories_RelationshipWorks()
    {
        // Add crawl history
        _context.CrawlHistories.Add(new CrawlHistory
        {
            CityId = 1,
            Status = "Success",
            SchoolsFound = 5,
            SchoolsAdded = 3
        });
        await _context.SaveChangesAsync();

        var city = await _context.Cities
            .Include(c => c.CrawlHistories)
            .FirstAsync(c => c.CityId == 1);

        Assert.Single(city.CrawlHistories);
    }

    [Fact]
    public async Task Class_BelongsToSchool()
    {
        var cls = await _context.Classes
            .Include(c => c.School)
            .FirstAsync(c => c.ClassId == 1);

        Assert.NotNull(cls.School);
        Assert.Equal("Italian Kitchen Academy", cls.School.Name);
    }

    #endregion

    #region Full Search Pipeline

    [Fact]
    public async Task FullSearchPipeline_TextSearch_ReturnsCorrectResults()
    {
        var logger = new Mock<ILogger<SchoolService>>();
        var service = new SchoolService(_context, logger.Object);

        var request = new SchoolSearchRequest
        {
            Query = "Italian",
            PageSize = 20
        };

        var result = await service.SearchSchoolsPagedAsync(request);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Schools);
        Assert.Equal("Italian Kitchen Academy", result.Schools[0].Name);
    }

    [Fact]
    public async Task FullSearchPipeline_CityAndCuisineFilter_WorksTogether()
    {
        var logger = new Mock<ILogger<SchoolService>>();
        var service = new SchoolService(_context, logger.Object);

        var request = new SchoolSearchRequest
        {
            City = "New York",
            CuisineType = "Thai",
            PageSize = 20
        };

        var result = await service.SearchSchoolsPagedAsync(request);

        Assert.Single(result.Schools);
        Assert.Equal("Thai Cooking Studio", result.Schools[0].Name);
    }

    [Fact]
    public async Task FullSearchPipeline_PaginationThroughAllPages()
    {
        var logger = new Mock<ILogger<SchoolService>>();
        var service = new SchoolService(_context, logger.Object);

        var allSchoolIds = new HashSet<int>();

        // Page 1
        var page1 = await service.SearchSchoolsPagedAsync(new SchoolSearchRequest { Page = 1, PageSize = 2 });
        foreach (var s in page1.Schools) allSchoolIds.Add(s.SchoolId);

        // Page 2
        var page2 = await service.SearchSchoolsPagedAsync(new SchoolSearchRequest { Page = 2, PageSize = 2 });
        foreach (var s in page2.Schools) allSchoolIds.Add(s.SchoolId);

        Assert.Equal(3, allSchoolIds.Count); // All 3 active schools
    }

    #endregion

    #region Full Crawl Pipeline

    [Fact]
    public async Task FullCrawlPipeline_CrawlCity_CompleteWorkflow()
    {
        // Verify initial state
        var cityBefore = await _context.Cities.FindAsync(1);
        Assert.NotNull(cityBefore);
        Assert.Equal(0, cityBefore.CrawlCount);
        Assert.Null(cityBefore.LastCrawledAt);

        // Create AutoCrawlService
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        services.AddLogging();

        using var sp = services.BuildServiceProvider();

        // Seed the new DB
        using (var scope = sp.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await TestDbContextFactory.SeedTestDataAsync(ctx);
        }

        var logger = new Mock<ILogger<AutoCrawlService>>();
        var settings = Options.Create(new CrawlSettings
        {
            EnableAutoCrawl = false,
            CitiesPerBatch = 3,
            RetryDelayHours = 2
        });
        var crawlService = new AutoCrawlService(sp, logger.Object, settings);

        // Get next cities to crawl
        var citiesToCrawl = await crawlService.GetNextCitiesToCrawlAsync(5);
        Assert.True(citiesToCrawl.Count > 0);

        // Crawl first city
        var firstCity = citiesToCrawl[0];
        var result = await crawlService.CrawlCityAsync(firstCity);
        Assert.True(result.Success);

        // Verify crawl history was created
        using (var scope = sp.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var history = await ctx.CrawlHistories
                .Where(h => h.CityId == firstCity.CityId)
                .ToListAsync();

            Assert.NotEmpty(history);
            Assert.Contains(history, h => h.Status == "Success");

            // Verify city metadata was updated
            var crawledCity = await ctx.Cities.FindAsync(firstCity.CityId);
            Assert.NotNull(crawledCity);
            Assert.NotNull(crawledCity.LastCrawledAt);
            Assert.Equal("Success", crawledCity.LastCrawlStatus);
            Assert.True(crawledCity.CrawlCount > 0);
        }
    }

    #endregion

    #region Blog Pipeline

    [Fact]
    public async Task FullBlogPipeline_CreatePublishView()
    {
        var logger = new Mock<ILogger<BlogService>>();
        var blogService = new BlogService(_context, logger.Object);

        // Create a draft
        var draft = await blogService.CreatePostAsync(new BlogPost
        {
            Title = "Pipeline Test Post",
            Content = "Test content for pipeline"
        });

        Assert.NotEmpty(draft.Slug);
        Assert.False(draft.IsPublished);

        // Can't find by slug as published
        var notFound = await blogService.GetPostBySlugAsync(draft.Slug);
        Assert.Null(notFound);

        // Publish it
        draft.IsPublished = true;
        await blogService.UpdatePostAsync(draft);

        // Now can find
        var published = await blogService.GetPostBySlugAsync(draft.Slug);
        Assert.NotNull(published);
        Assert.NotNull(published.PublishedAt);

        // Increment views
        await blogService.IncrementViewCountAsync(draft.Id);
        await blogService.IncrementViewCountAsync(draft.Id);
        await blogService.IncrementViewCountAsync(draft.Id);

        var viewed = await blogService.GetPostByIdAsync(draft.Id);
        Assert.Equal(3, viewed!.ViewCount);

        // Delete
        var deleted = await blogService.DeletePostAsync(draft.Id);
        Assert.True(deleted);

        var gone = await blogService.GetPostByIdAsync(draft.Id);
        Assert.Null(gone);
    }

    #endregion

    #region School CRUD Pipeline

    [Fact]
    public async Task FullSchoolCRUD_CreateUpdateDelete()
    {
        var logger = new Mock<ILogger<SchoolService>>();
        var service = new SchoolService(_context, logger.Object);

        // Create
        var school = await service.CreateSchoolAsync(new CookingSchool
        {
            Name = "Pipeline Test School",
            Address = "123 Test St",
            City = "Boston",
            Country = "United States",
            Latitude = 42.3601,
            Longitude = -71.0589
        });

        Assert.True(school.SchoolId > 0);
        Assert.Contains("pipeline-test-school", school.Slug);

        // Read
        var found = await service.GetByIdAsync(school.SchoolId);
        Assert.NotNull(found);
        Assert.Equal("Pipeline Test School", found.Name);

        // Update
        school.Name = "Updated Pipeline School";
        var updated = await service.UpdateSchoolAsync(school);
        Assert.Equal("Updated Pipeline School", updated.Name);

        // Delete (soft)
        var deleted = await service.DeleteSchoolAsync(school.SchoolId);
        Assert.True(deleted);

        // Should not be findable via GetById (filters by IsActive)
        var gone = await service.GetByIdAsync(school.SchoolId);
        Assert.Null(gone);
    }

    #endregion

    #region Review Pipeline

    [Fact]
    public async Task ReviewPipeline_AddAndUpdateRating()
    {
        var logger = new Mock<ILogger<SchoolService>>();
        var service = new SchoolService(_context, logger.Object);

        // Add new approved reviews to school 3 (Thai - no reviews yet)
        _context.Reviews.AddRange(
            new Review { SchoolId = 3, Rating = 5, IsApproved = true },
            new Review { SchoolId = 3, Rating = 3, IsApproved = true },
            new Review { SchoolId = 3, Rating = 4, IsApproved = false } // Not approved
        );
        await _context.SaveChangesAsync();

        // Update rating
        await service.UpdateSchoolRatingAsync(3);

        var school = await _context.Schools.FindAsync(3);
        Assert.NotNull(school);
        Assert.Equal(4.0m, school.AverageRating); // (5 + 3) / 2
        Assert.Equal(2, school.TotalReviews); // Only approved
    }

    #endregion

    #region Data Consistency

    [Fact]
    public async Task AllActiveSchools_HaveRequiredFields()
    {
        var schools = await _context.Schools.Where(s => s.IsActive).ToListAsync();

        Assert.All(schools, s =>
        {
            Assert.NotEmpty(s.Name);
            Assert.NotEmpty(s.Address);
            Assert.NotEmpty(s.City);
            Assert.NotEmpty(s.Slug);
        });
    }

    [Fact]
    public async Task AllActiveCities_HaveRequiredFields()
    {
        var cities = await _context.Cities.Where(c => c.IsActive).ToListAsync();

        Assert.All(cities, c =>
        {
            Assert.NotEmpty(c.Name);
            Assert.NotEmpty(c.Slug);
            Assert.NotEmpty(c.Country);
            Assert.InRange(c.MaxResults, 5, 50);
        });
    }

    [Fact]
    public async Task SlugUniqueness_Schools()
    {
        var slugs = await _context.Schools.Select(s => s.Slug).ToListAsync();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }

    [Fact]
    public async Task SlugUniqueness_Cities()
    {
        var slugs = await _context.Cities.Select(c => c.Slug).ToListAsync();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }

    [Fact]
    public async Task SlugUniqueness_BlogPosts()
    {
        var slugs = await _context.BlogPosts.Select(p => p.Slug).ToListAsync();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }

    #endregion
}
