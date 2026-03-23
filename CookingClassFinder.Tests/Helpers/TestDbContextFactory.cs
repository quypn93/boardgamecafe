using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace CookingClassFinder.Tests.Helpers;

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

    public static async Task<ApplicationDbContext> CreateWithSeedDataAsync(string? dbName = null)
    {
        var context = Create(dbName);
        await SeedTestDataAsync(context);
        return context;
    }

    public static async Task SeedTestDataAsync(ApplicationDbContext context)
    {
        // Seed schools
        var schools = new List<CookingSchool>
        {
            new()
            {
                SchoolId = 1,
                Name = "Italian Kitchen Academy",
                Description = "Best Italian cooking school",
                Address = "123 Main St",
                City = "New York",
                State = "NY",
                Country = "United States",
                Latitude = 40.7128,
                Longitude = -74.0060,
                Slug = "italian-kitchen-academy-new-york",
                IsActive = true,
                IsPremium = true,
                AverageRating = 4.5m,
                TotalReviews = 10,
                TotalClasses = 3,
                Phone = "555-0001",
                Website = "https://example.com",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new()
            {
                SchoolId = 2,
                Name = "French Culinary Arts",
                Description = "Authentic French cooking",
                Address = "456 Oak Ave",
                City = "San Francisco",
                State = "CA",
                Country = "United States",
                Latitude = 37.7749,
                Longitude = -122.4194,
                Slug = "french-culinary-arts-san-francisco",
                IsActive = true,
                IsPremium = false,
                AverageRating = 4.0m,
                TotalReviews = 5,
                TotalClasses = 2,
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            },
            new()
            {
                SchoolId = 3,
                Name = "Thai Cooking Studio",
                Description = "Learn authentic Thai cuisine",
                Address = "789 Elm St",
                City = "New York",
                State = "NY",
                Country = "United States",
                Latitude = 40.7580,
                Longitude = -73.9855,
                Slug = "thai-cooking-studio-new-york",
                IsActive = true,
                IsPremium = false,
                AverageRating = 3.5m,
                TotalReviews = 3,
                TotalClasses = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new()
            {
                SchoolId = 4,
                Name = "Inactive School",
                Description = "This school is inactive",
                Address = "000 Dead St",
                City = "Chicago",
                State = "IL",
                Country = "United States",
                Latitude = 41.8781,
                Longitude = -87.6298,
                Slug = "inactive-school-chicago",
                IsActive = false,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            }
        };

        context.Schools.AddRange(schools);
        await context.SaveChangesAsync();

        // Seed classes
        var classes = new List<CookingClass>
        {
            new()
            {
                ClassId = 1,
                SchoolId = 1,
                Name = "Pasta Making 101",
                CuisineType = "Italian",
                DifficultyLevel = "Beginner",
                PricePerPerson = 75m,
                DurationMinutes = 120,
                MinStudents = 2,
                MaxStudents = 12,
                Slug = "pasta-making-101",
                IsActive = true,
                IsVegetarian = true
            },
            new()
            {
                ClassId = 2,
                SchoolId = 1,
                Name = "Advanced Italian",
                CuisineType = "Italian",
                DifficultyLevel = "Advanced",
                PricePerPerson = 150m,
                DurationMinutes = 180,
                MinStudents = 2,
                MaxStudents = 8,
                Slug = "advanced-italian",
                IsActive = true,
                IsVegan = true
            },
            new()
            {
                ClassId = 3,
                SchoolId = 1,
                Name = "Inactive Class",
                CuisineType = "Italian",
                DifficultyLevel = "Beginner",
                Slug = "inactive-class",
                IsActive = false
            },
            new()
            {
                ClassId = 4,
                SchoolId = 2,
                Name = "French Pastry",
                CuisineType = "French",
                DifficultyLevel = "Intermediate",
                PricePerPerson = 120m,
                DurationMinutes = 150,
                MinStudents = 4,
                MaxStudents = 10,
                Slug = "french-pastry",
                IsActive = true,
                IsKidsFriendly = true
            },
            new()
            {
                ClassId = 5,
                SchoolId = 3,
                Name = "Thai Street Food",
                CuisineType = "Thai",
                DifficultyLevel = "Beginner",
                PricePerPerson = 60m,
                DurationMinutes = 90,
                Slug = "thai-street-food",
                IsActive = true,
                IsOnline = true
            }
        };

        context.Classes.AddRange(classes);
        await context.SaveChangesAsync();

        // Seed reviews
        var reviews = new List<Review>
        {
            new() { ReviewId = 1, SchoolId = 1, Rating = 5, Title = "Amazing!", Content = "Loved it", IsApproved = true, CreatedAt = DateTime.UtcNow },
            new() { ReviewId = 2, SchoolId = 1, Rating = 4, Title = "Good", Content = "Nice experience", IsApproved = true, CreatedAt = DateTime.UtcNow },
            new() { ReviewId = 3, SchoolId = 1, Rating = 3, Title = "Pending", Content = "Pending review", IsApproved = false, CreatedAt = DateTime.UtcNow },
            new() { ReviewId = 4, SchoolId = 2, Rating = 4, Title = "Tres bien!", Content = "Great French cooking", IsApproved = true, CreatedAt = DateTime.UtcNow },
        };

        context.Reviews.AddRange(reviews);
        await context.SaveChangesAsync();

        // Seed blog posts
        var posts = new List<BlogPost>
        {
            new() { Id = 1, Title = "Best Cooking Classes in NYC", Slug = "best-cooking-classes-nyc", Content = "Content here", IsPublished = true, PublishedAt = DateTime.UtcNow.AddDays(-5), RelatedCity = "New York", Category = "city-guide", ViewCount = 100 },
            new() { Id = 2, Title = "Italian Cooking Guide", Slug = "italian-cooking-guide", Content = "Italian content", IsPublished = true, PublishedAt = DateTime.UtcNow.AddDays(-3), RelatedCuisine = "Italian", Category = "cuisine-guide", ViewCount = 50 },
            new() { Id = 3, Title = "Draft Post", Slug = "draft-post", Content = "Draft content", IsPublished = false, Category = "tips", ViewCount = 0 },
        };

        context.BlogPosts.AddRange(posts);
        await context.SaveChangesAsync();

        // Seed cities
        var cities = new List<City>
        {
            new() { CityId = 1, Name = "New York", Country = "United States", Region = "US", Slug = "new-york", IsActive = true, CrawlCount = 0, SchoolCount = 2 },
            new() { CityId = 2, Name = "San Francisco", Country = "United States", Region = "US", Slug = "san-francisco", IsActive = true, CrawlCount = 1, LastCrawledAt = DateTime.UtcNow.AddDays(-7), LastCrawlStatus = "Success", NextCrawlAt = DateTime.UtcNow.AddDays(-1), SchoolCount = 1 },
            new() { CityId = 3, Name = "Chicago", Country = "United States", Region = "US", Slug = "chicago", IsActive = true, CrawlCount = 0, SchoolCount = 0 },
            new() { CityId = 4, Name = "Tokyo", Country = "Japan", Region = "International", Slug = "tokyo", IsActive = true, CrawlCount = 2, LastCrawledAt = DateTime.UtcNow.AddDays(-3), LastCrawlStatus = "Failed", NextCrawlAt = DateTime.UtcNow.AddHours(-1), SchoolCount = 0 },
            new() { CityId = 5, Name = "Inactive City", Country = "Test", Region = "International", Slug = "inactive-city", IsActive = false, CrawlCount = 0, SchoolCount = 0 },
        };

        context.Cities.AddRange(cities);
        await context.SaveChangesAsync();
    }
}
