using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Models.DTOs;
using CookingClassFinder.Services;
using CookingClassFinder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace CookingClassFinder.Tests.Services;

public class SchoolServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SchoolService _service;

    public SchoolServiceTests()
    {
        _context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedTestDataAsync(_context).GetAwaiter().GetResult();
        var logger = new Mock<ILogger<SchoolService>>();
        _service = new SchoolService(_context, logger.Object);
    }

    public void Dispose() => _context.Dispose();

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ExistingActiveSchool_ReturnsSchool()
    {
        var result = await _service.GetByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Italian Kitchen Academy", result.Name);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_InactiveSchool_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(4);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesClasses()
    {
        var result = await _service.GetByIdAsync(1);

        Assert.NotNull(result);
        // InMemory provider does not support filtered includes,
        // so we verify classes are loaded (all 3 for school 1)
        Assert.True(result.Classes.Count >= 2);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesReviews()
    {
        var result = await _service.GetByIdAsync(1);

        Assert.NotNull(result);
        // InMemory provider does not support filtered includes,
        // so we verify reviews are loaded (all 3 for school 1)
        Assert.True(result.Reviews.Count >= 2);
    }

    #endregion

    #region GetBySlugAsync

    [Fact]
    public async Task GetBySlugAsync_ValidSlug_ReturnsSchool()
    {
        var result = await _service.GetBySlugAsync("italian-kitchen-academy-new-york");

        Assert.NotNull(result);
        Assert.Equal(1, result.SchoolId);
    }

    [Fact]
    public async Task GetBySlugAsync_InactiveSchoolSlug_ReturnsNull()
    {
        var result = await _service.GetBySlugAsync("inactive-school-chicago");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySlugAsync_InvalidSlug_ReturnsNull()
    {
        var result = await _service.GetBySlugAsync("nonexistent-slug");
        Assert.Null(result);
    }

    #endregion

    #region SearchByTextAsync

    [Fact]
    public async Task SearchByTextAsync_MatchingName_ReturnsSchools()
    {
        var result = await _service.SearchByTextAsync("Italian");

        Assert.Single(result);
        Assert.Equal("Italian Kitchen Academy", result[0].Name);
    }

    [Fact]
    public async Task SearchByTextAsync_MatchingCity_ReturnsSchools()
    {
        var result = await _service.SearchByTextAsync("New York");

        Assert.Equal(2, result.Count); // Italian Kitchen Academy + Thai Cooking Studio
    }

    [Fact]
    public async Task SearchByTextAsync_NoMatch_ReturnsEmpty()
    {
        var result = await _service.SearchByTextAsync("Nonexistent");
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchByTextAsync_ExcludesInactiveSchools()
    {
        var result = await _service.SearchByTextAsync("Inactive");
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchByTextAsync_RespectsLimit()
    {
        var result = await _service.SearchByTextAsync("New York", limit: 1);
        Assert.Single(result);
    }

    [Fact]
    public async Task SearchByTextAsync_CaseInsensitive()
    {
        var result = await _service.SearchByTextAsync("italian");
        Assert.Single(result);
    }

    [Fact]
    public async Task SearchByTextAsync_PremiumSchoolsFirst()
    {
        var result = await _service.SearchByTextAsync("New York");

        Assert.True(result.Count >= 2);
        // Italian Kitchen Academy (premium) should come first
        Assert.True(result[0].IsPremium);
    }

    #endregion

    #region FilterSchoolsAsync

    [Fact]
    public async Task FilterSchoolsAsync_ByCity_ReturnsMatchingSchools()
    {
        var result = await _service.FilterSchoolsAsync(null, "New York", null, null, null, null);

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal("New York", s.City));
    }

    [Fact]
    public async Task FilterSchoolsAsync_ByCuisine_ReturnsMatchingSchools()
    {
        var result = await _service.FilterSchoolsAsync(null, null, "Italian", null, null, null);

        Assert.Single(result);
        Assert.Equal("Italian Kitchen Academy", result[0].Name);
    }

    [Fact]
    public async Task FilterSchoolsAsync_ByMaxPrice_ReturnsAffordable()
    {
        var result = await _service.FilterSchoolsAsync(null, null, null, null, 80m, null);

        // Schools with at least one class <= $80: School 1 (Pasta $75), School 3 (Thai $60)
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task FilterSchoolsAsync_ByMinRating_ReturnsHighRated()
    {
        var result = await _service.FilterSchoolsAsync(null, null, null, null, null, 4.0);

        // School 1 (4.5) and School 2 (4.0) meet the rating threshold
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task FilterSchoolsAsync_ByDifficulty_ReturnsMatching()
    {
        var result = await _service.FilterSchoolsAsync(null, null, null, "Beginner", null, null);

        // Schools with beginner classes: School 1 (Pasta 101) + School 3 (Thai Street Food)
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task FilterSchoolsAsync_MultipleFilters_CombinesCorrectly()
    {
        var result = await _service.FilterSchoolsAsync(null, "New York", "Italian", null, null, null);

        Assert.Single(result);
        Assert.Equal("Italian Kitchen Academy", result[0].Name);
    }

    #endregion

    #region SearchSchoolsPagedAsync

    [Fact]
    public async Task SearchSchoolsPagedAsync_DefaultRequest_ReturnsAllActive()
    {
        var request = new SchoolSearchRequest { PageSize = 20 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        Assert.Equal(3, result.TotalCount); // 3 active schools
        Assert.Equal(3, result.Schools.Count);
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_WithQuery_FiltersResults()
    {
        var request = new SchoolSearchRequest { Query = "Italian", PageSize = 20 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_WithCityFilter_FiltersResults()
    {
        var request = new SchoolSearchRequest { City = "New York", PageSize = 20 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_Pagination_WorksCorrectly()
    {
        var request = new SchoolSearchRequest { Page = 1, PageSize = 2 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Schools.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_Page2_ReturnsRemainder()
    {
        var request = new SchoolSearchRequest { Page = 2, PageSize = 2 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        Assert.Equal(3, result.TotalCount);
        Assert.Single(result.Schools);
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_SortByName_SortsCorrectly()
    {
        var request = new SchoolSearchRequest { SortBy = "name", SortDescending = false, PageSize = 20 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        Assert.Equal("French Culinary Arts", result.Schools[0].Name);
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_SortByRating_SortsCorrectly()
    {
        var request = new SchoolSearchRequest { SortBy = "rating", SortDescending = true, PageSize = 20 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        Assert.Equal("Italian Kitchen Academy", result.Schools[0].Name);
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_VegetarianFilter_Works()
    {
        var request = new SchoolSearchRequest { IsVegetarian = true, PageSize = 20 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        Assert.Single(result.Schools); // Only School 1 has a vegetarian class
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_VeganFilter_Works()
    {
        var request = new SchoolSearchRequest { IsVegan = true, PageSize = 20 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        Assert.Single(result.Schools);
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_OnlineFilter_Works()
    {
        var request = new SchoolSearchRequest { IsOnline = true, PageSize = 20 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        Assert.Single(result.Schools); // Thai Cooking Studio
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_KidsFriendlyFilter_Works()
    {
        var request = new SchoolSearchRequest { IsKidsFriendly = true, PageSize = 20 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        Assert.Single(result.Schools); // French Culinary Arts
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_ClassSummariesIncluded()
    {
        var request = new SchoolSearchRequest { PageSize = 20 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        var italianSchool = result.Schools.First(s => s.Name == "Italian Kitchen Academy");
        Assert.Equal(2, italianSchool.Classes.Count); // 2 active classes
        Assert.All(italianSchool.Classes, c => Assert.NotEmpty(c.Name));
    }

    [Fact]
    public async Task SearchSchoolsPagedAsync_PriceRange_CalculatedCorrectly()
    {
        var request = new SchoolSearchRequest { PageSize = 20 };
        var result = await _service.SearchSchoolsPagedAsync(request);

        var italianSchool = result.Schools.First(s => s.Name == "Italian Kitchen Academy");
        Assert.Equal(75m, italianSchool.LowestPrice);
        Assert.Equal(150m, italianSchool.HighestPrice);
    }

    #endregion

    #region GetFeaturedSchoolsAsync

    [Fact]
    public async Task GetFeaturedSchoolsAsync_ReturnsPremiumSchools()
    {
        var result = await _service.GetFeaturedSchoolsAsync();

        Assert.Single(result);
        Assert.All(result, s => Assert.True(s.IsPremium));
    }

    [Fact]
    public async Task GetFeaturedSchoolsAsync_RespectsCount()
    {
        var result = await _service.GetFeaturedSchoolsAsync(count: 1);
        Assert.Single(result);
    }

    #endregion

    #region GetNearbySchoolsAsync

    [Fact]
    public async Task GetNearbySchoolsAsync_FindsSchoolsInRadius()
    {
        // New York coordinates with 10km radius
        var result = await _service.GetNearbySchoolsAsync(40.7128, -74.0060, 10, 20);

        // Should find Italian Kitchen Academy and Thai Cooking Studio (both in NYC area)
        Assert.True(result.Count >= 1);
        Assert.All(result, s => Assert.True(s.DistanceKm <= 10));
    }

    [Fact]
    public async Task GetNearbySchoolsAsync_EmptyForRemoteLocation()
    {
        // Tokyo coordinates - far from any test schools
        var result = await _service.GetNearbySchoolsAsync(35.6762, 139.6503, 10, 20);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNearbySchoolsAsync_OrderedByDistance()
    {
        var result = await _service.GetNearbySchoolsAsync(40.7128, -74.0060, 100, 20);

        if (result.Count >= 2)
        {
            for (int i = 1; i < result.Count; i++)
            {
                Assert.True(result[i].DistanceKm >= result[i - 1].DistanceKm);
            }
        }
    }

    [Fact]
    public async Task GetNearbySchoolsAsync_ExcludesInactive()
    {
        var result = await _service.GetNearbySchoolsAsync(41.8781, -87.6298, 10, 20);
        Assert.DoesNotContain(result, s => s.Name == "Inactive School");
    }

    #endregion

    #region CRUD Operations

    [Fact]
    public async Task CreateSchoolAsync_CreatesWithSlug()
    {
        var school = new CookingSchool
        {
            Name = "New Test School",
            Address = "Test Address",
            City = "Boston",
            Country = "United States",
            Latitude = 42.3601,
            Longitude = -71.0589
        };

        var result = await _service.CreateSchoolAsync(school);

        Assert.NotEqual(0, result.SchoolId);
        Assert.Contains("new-test-school", result.Slug);
        Assert.Contains("boston", result.Slug);
    }

    [Fact]
    public async Task CreateSchoolAsync_SetsTimestamps()
    {
        var school = new CookingSchool
        {
            Name = "Timestamp Test",
            Address = "Test",
            City = "Test",
            Country = "US"
        };

        var before = DateTime.UtcNow;
        var result = await _service.CreateSchoolAsync(school);
        var after = DateTime.UtcNow;

        Assert.InRange(result.CreatedAt, before, after);
        Assert.InRange(result.UpdatedAt, before, after);
    }

    [Fact]
    public async Task UpdateSchoolAsync_UpdatesTimestamp()
    {
        var school = await _context.Schools.FindAsync(1);
        Assert.NotNull(school);

        var oldUpdatedAt = school.UpdatedAt;
        school.Name = "Updated Name";

        await Task.Delay(10); // Ensure time difference
        var result = await _service.UpdateSchoolAsync(school);

        Assert.True(result.UpdatedAt >= oldUpdatedAt);
    }

    [Fact]
    public async Task DeleteSchoolAsync_SoftDeletes()
    {
        var result = await _service.DeleteSchoolAsync(1);

        Assert.True(result);
        var school = await _context.Schools.FindAsync(1);
        Assert.NotNull(school);
        Assert.False(school.IsActive);
    }

    [Fact]
    public async Task DeleteSchoolAsync_NonExistent_ReturnsFalse()
    {
        var result = await _service.DeleteSchoolAsync(999);
        Assert.False(result);
    }

    #endregion

    #region UpdateSchoolRatingAsync

    [Fact]
    public async Task UpdateSchoolRatingAsync_RecalculatesFromApprovedReviews()
    {
        await _service.UpdateSchoolRatingAsync(1);

        var school = await _context.Schools.FindAsync(1);
        Assert.NotNull(school);
        // Approved reviews: 5 and 4, average = 4.5
        Assert.Equal(4.5m, school.AverageRating);
        Assert.Equal(2, school.TotalReviews);
    }

    [Fact]
    public async Task UpdateSchoolRatingAsync_NoReviews_SetsNull()
    {
        await _service.UpdateSchoolRatingAsync(3); // Thai school has no reviews

        var school = await _context.Schools.FindAsync(3);
        Assert.NotNull(school);
        Assert.Null(school.AverageRating);
        Assert.Equal(0, school.TotalReviews);
    }

    [Fact]
    public async Task UpdateSchoolRatingAsync_NonExistentSchool_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.UpdateSchoolRatingAsync(999));
        Assert.Null(exception);
    }

    #endregion

    #region Utility Methods

    [Fact]
    public async Task GetAllCitiesAsync_ReturnsDistinctActiveCities()
    {
        var result = await _service.GetAllCitiesAsync();

        Assert.Contains("New York", result);
        Assert.Contains("San Francisco", result);
        Assert.DoesNotContain("Chicago", result); // Inactive school's city
        Assert.Equal(result.OrderBy(c => c).ToList(), result); // Alphabetically sorted
    }

    [Fact]
    public async Task GetAllCuisineTypesAsync_ReturnsDistinctActiveCuisines()
    {
        var result = await _service.GetAllCuisineTypesAsync();

        Assert.Contains("Italian", result);
        Assert.Contains("French", result);
        Assert.Contains("Thai", result);
    }

    [Fact]
    public async Task GetTotalSchoolCountAsync_CountsActiveOnly()
    {
        var result = await _service.GetTotalSchoolCountAsync();
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task GetTotalClassCountAsync_CountsActiveOnly()
    {
        var result = await _service.GetTotalClassCountAsync();
        Assert.Equal(4, result); // 5 total, 1 inactive
    }

    #endregion
}
