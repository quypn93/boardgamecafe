using VRArcadeFinder.Data;
using VRArcadeFinder.Models.Domain;

namespace VRArcadeFinder.Tests.Helpers
{
    public static class TestDataSeeder
    {
        public static void SeedArcades(ApplicationDbContext context, int count = 5)
        {
            var arcades = new List<Arcade>();
            for (int i = 1; i <= count; i++)
            {
                arcades.Add(new Arcade
                {
                    Name = $"VR Arcade {i}",
                    Address = $"{i}00 Main St",
                    City = i <= 3 ? "New York" : "Los Angeles",
                    State = i <= 3 ? "NY" : "CA",
                    Country = "United States",
                    Latitude = 40.7128 + (i * 0.01),
                    Longitude = -74.0060 + (i * 0.01),
                    Slug = $"vr-arcade-{i}",
                    IsActive = true,
                    AverageRating = 3.0m + (i * 0.3m),
                    TotalReviews = i * 5,
                    VRPlatforms = i % 2 == 0 ? "Meta Quest, HTC Vive" : "PlayStation VR",
                    TotalVRStations = i * 3,
                    HasMultiplayerArea = i % 2 == 0,
                    IsPremium = i == 1,
                    IsVerified = i <= 2,
                    CreatedAt = DateTime.UtcNow.AddDays(-i),
                    UpdatedAt = DateTime.UtcNow
                });
            }

            context.Arcades.AddRange(arcades);
            context.SaveChanges();
        }

        public static void SeedGames(ApplicationDbContext context, int count = 5)
        {
            var games = new List<VRGame>();
            for (int i = 1; i <= count; i++)
            {
                games.Add(new VRGame
                {
                    Name = $"VR Game {i}",
                    Slug = $"vr-game-{i}",
                    Description = $"An amazing VR game number {i}",
                    Genre = i % 2 == 0 ? "Action" : "Adventure",
                    Category = i % 2 == 0 ? "Shooter" : "Escape Room",
                    VRPlatform = "Meta Quest",
                    MinPlayers = 1,
                    MaxPlayers = i <= 3 ? 2 : 4,
                    IntensityLevel = i % 2 == 0 ? "High" : "Medium",
                    Rating = 3.5m + (i * 0.2m),
                    CreatedAt = DateTime.UtcNow
                });
            }

            context.VRGames.AddRange(games);
            context.SaveChanges();
        }

        public static void SeedArcadeGames(ApplicationDbContext context)
        {
            var arcades = context.Arcades.ToList();
            var games = context.VRGames.ToList();

            var arcadeGames = new List<ArcadeGame>();
            foreach (var arcade in arcades)
            {
                foreach (var game in games.Take(3))
                {
                    arcadeGames.Add(new ArcadeGame
                    {
                        ArcadeId = arcade.ArcadeId,
                        GameId = game.GameId,
                        IsAvailable = true,
                        Quantity = 2,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            context.ArcadeGames.AddRange(arcadeGames);
            context.SaveChanges();
        }

        public static void SeedReviews(ApplicationDbContext context)
        {
            var arcades = context.Arcades.ToList();
            var reviews = new List<Review>();

            foreach (var arcade in arcades)
            {
                for (int i = 1; i <= 3; i++)
                {
                    reviews.Add(new Review
                    {
                        ArcadeId = arcade.ArcadeId,
                        Rating = Math.Min(5, i + 2),
                        Title = $"Review {i} for {arcade.Name}",
                        Content = $"This is review content {i} for arcade {arcade.Name}",
                        IsApproved = i <= 2,
                        CreatedAt = DateTime.UtcNow.AddDays(-i)
                    });
                }
            }

            context.Reviews.AddRange(reviews);
            context.SaveChanges();
        }

        public static void SeedBlogPosts(ApplicationDbContext context, int count = 5)
        {
            var posts = new List<BlogPost>();
            for (int i = 1; i <= count; i++)
            {
                posts.Add(new BlogPost
                {
                    Title = $"Blog Post {i}",
                    Slug = $"blog-post-{i}",
                    Content = $"<p>Content of blog post {i}</p>",
                    Summary = $"Summary of post {i}",
                    Category = i % 2 == 0 ? "Top Games" : "City Guide",
                    Author = "Test Author",
                    IsPublished = i <= 3,
                    PublishedAt = i <= 3 ? DateTime.UtcNow.AddDays(-i) : null,
                    RelatedCity = i <= 2 ? "New York" : "Los Angeles",
                    PostType = i % 2 == 0 ? "top-games" : "city-guide",
                    CreatedAt = DateTime.UtcNow.AddDays(-i)
                });
            }

            context.BlogPosts.AddRange(posts);
            context.SaveChanges();
        }

        public static void SeedCities(ApplicationDbContext context)
        {
            var cities = new List<City>
            {
                new City { Name = "New York", Country = "United States", Region = "US", IsEnabled = true, CrawlCount = 0, MaxResults = 15 },
                new City { Name = "Los Angeles", Country = "United States", Region = "US", IsEnabled = true, CrawlCount = 1, LastCrawledAt = DateTime.UtcNow.AddDays(-7), MaxResults = 15 },
                new City { Name = "Tokyo", Country = "Japan", Region = "International", IsEnabled = true, CrawlCount = 0, MaxResults = 10 },
                new City { Name = "Disabled City", Country = "Nowhere", Region = "US", IsEnabled = false, CrawlCount = 0, MaxResults = 10 },
            };

            context.Cities.AddRange(cities);
            context.SaveChanges();
        }

        public static void SeedAll(ApplicationDbContext context)
        {
            SeedArcades(context);
            SeedGames(context);
            SeedArcadeGames(context);
            SeedReviews(context);
            SeedBlogPosts(context);
            SeedCities(context);
        }
    }
}
