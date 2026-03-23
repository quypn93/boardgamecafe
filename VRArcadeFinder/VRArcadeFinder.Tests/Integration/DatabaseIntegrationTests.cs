using Microsoft.EntityFrameworkCore;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models.Domain;
using VRArcadeFinder.Tests.Helpers;

namespace VRArcadeFinder.Tests.Integration
{
    public class DatabaseIntegrationTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public DatabaseIntegrationTests()
        {
            _context = TestDbContextFactory.Create();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Arcade CRUD Tests

        [Fact]
        public async Task CanCreateAndRetrieveArcade()
        {
            // Arrange
            var arcade = new Arcade
            {
                Name = "Test VR Hub",
                Address = "123 Test St",
                City = "New York",
                Country = "United States",
                Slug = "test-vr-hub",
                Latitude = 40.7128,
                Longitude = -74.0060,
                IsActive = true
            };

            // Act
            _context.Arcades.Add(arcade);
            await _context.SaveChangesAsync();

            var retrieved = await _context.Arcades.FindAsync(arcade.ArcadeId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("Test VR Hub", retrieved.Name);
            Assert.Equal("New York", retrieved.City);
        }

        [Fact]
        public async Task ArcadeSlug_IsUnique()
        {
            // Arrange
            _context.Arcades.Add(new Arcade
            {
                Name = "Arcade 1",
                Address = "123 St",
                City = "NY",
                Slug = "unique-slug"
            });
            await _context.SaveChangesAsync();

            // InMemoryDatabase doesn't enforce unique constraints,
            // but we verify the model configuration exists
            var entity = _context.Model.FindEntityType(typeof(Arcade));
            var slugIndex = entity?.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == "Slug"));

            Assert.NotNull(slugIndex);
            Assert.True(slugIndex.IsUnique);
        }

        #endregion

        #region Review Tests

        [Fact]
        public async Task CanCreateReviewForArcade()
        {
            // Arrange
            var arcade = new Arcade
            {
                Name = "Review Target",
                Address = "456 St",
                City = "LA",
                Slug = "review-target"
            };
            _context.Arcades.Add(arcade);
            await _context.SaveChangesAsync();

            var review = new Review
            {
                ArcadeId = arcade.ArcadeId,
                Rating = 5,
                Title = "Amazing VR!",
                Content = "Best VR experience ever"
            };

            // Act
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // Assert
            var reviews = await _context.Reviews.Where(r => r.ArcadeId == arcade.ArcadeId).ToListAsync();
            Assert.Single(reviews);
            Assert.Equal(5, reviews[0].Rating);
        }

        #endregion

        #region ArcadeGame Relationship Tests

        [Fact]
        public async Task CanLinkGameToArcade()
        {
            // Arrange
            var arcade = new Arcade
            {
                Name = "Game Arcade",
                Address = "789 St",
                City = "SF",
                Slug = "game-arcade"
            };
            var game = new VRGame
            {
                Name = "Beat Saber",
                Slug = "beat-saber",
                Genre = "Rhythm"
            };

            _context.Arcades.Add(arcade);
            _context.VRGames.Add(game);
            await _context.SaveChangesAsync();

            var arcadeGame = new ArcadeGame
            {
                ArcadeId = arcade.ArcadeId,
                GameId = game.GameId,
                IsAvailable = true,
                Quantity = 3
            };

            // Act
            _context.ArcadeGames.Add(arcadeGame);
            await _context.SaveChangesAsync();

            // Assert
            var result = await _context.ArcadeGames
                .Include(ag => ag.Game)
                .FirstOrDefaultAsync(ag => ag.ArcadeId == arcade.ArcadeId);

            Assert.NotNull(result);
            Assert.Equal("Beat Saber", result.Game.Name);
            Assert.Equal(3, result.Quantity);
        }

        #endregion

        #region BlogPost Tests

        [Fact]
        public async Task CanCreateAndQueryBlogPosts()
        {
            // Arrange
            var post = new BlogPost
            {
                Title = "VR Guide",
                Slug = "vr-guide",
                Content = "<p>Guide content</p>",
                Category = "Guide",
                IsPublished = true,
                PublishedAt = DateTime.UtcNow,
                RelatedCity = "Tokyo"
            };

            // Act
            _context.BlogPosts.Add(post);
            await _context.SaveChangesAsync();

            var published = await _context.BlogPosts.Where(p => p.IsPublished).ToListAsync();
            var byCity = await _context.BlogPosts.Where(p => p.RelatedCity == "Tokyo").ToListAsync();

            // Assert
            Assert.Single(published);
            Assert.Single(byCity);
        }

        [Fact]
        public async Task BlogPostSlug_IsUnique()
        {
            var entity = _context.Model.FindEntityType(typeof(BlogPost));
            var slugIndex = entity?.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == "Slug"));

            Assert.NotNull(slugIndex);
            Assert.True(slugIndex.IsUnique);
        }

        #endregion

        #region City and CrawlHistory Tests

        [Fact]
        public async Task CanCreateCityWithCrawlHistory()
        {
            // Arrange
            var city = new City
            {
                Name = "Test City",
                Country = "Test Country",
                Region = "US",
                IsEnabled = true,
                MaxResults = 20
            };

            _context.Cities.Add(city);
            await _context.SaveChangesAsync();

            var history = new CrawlHistory
            {
                CityId = city.CityId,
                StartedAt = DateTime.UtcNow,
                Status = "Success",
                ArcadesFound = 10,
                ArcadesAdded = 8,
                ArcadesUpdated = 2,
                CompletedAt = DateTime.UtcNow
            };

            // Act
            _context.CrawlHistories.Add(history);
            await _context.SaveChangesAsync();

            // Assert
            var cityWithHistory = await _context.Cities
                .Include(c => c.CrawlHistories)
                .FirstOrDefaultAsync(c => c.CityId == city.CityId);

            Assert.NotNull(cityWithHistory);
            Assert.Single(cityWithHistory.CrawlHistories);
            Assert.Equal("Success", cityWithHistory.CrawlHistories.First().Status);
            Assert.Equal(10, cityWithHistory.CrawlHistories.First().ArcadesFound);
        }

        [Fact]
        public async Task CanQueryCitiesNeedingCrawl()
        {
            // Arrange
            TestDataSeeder.SeedCities(_context);

            // Act
            var citiesNeedingCrawl = await _context.Cities
                .Where(c => c.IsEnabled)
                .Where(c => c.LastCrawledAt == null || c.CrawlCount == 0)
                .OrderBy(c => c.CrawlCount)
                .ToListAsync();

            // Assert
            Assert.NotEmpty(citiesNeedingCrawl);
            Assert.DoesNotContain(citiesNeedingCrawl, c => c.Name == "Disabled City");
        }

        #endregion

        #region Event and Booking Tests

        [Fact]
        public async Task CanCreateEventWithBooking()
        {
            // Arrange
            var arcade = new Arcade
            {
                Name = "Event Arcade",
                Address = "111 St",
                City = "Chicago",
                Slug = "event-arcade"
            };
            _context.Arcades.Add(arcade);
            await _context.SaveChangesAsync();

            var vrEvent = new Event
            {
                ArcadeId = arcade.ArcadeId,
                Title = "VR Tournament",
                EventType = "Tournament",
                StartDateTime = DateTime.UtcNow.AddDays(7),
                EndDateTime = DateTime.UtcNow.AddDays(7).AddHours(3),
                MaxParticipants = 20,
                IsActive = true
            };

            _context.Events.Add(vrEvent);
            await _context.SaveChangesAsync();

            // Act
            var events = await _context.Events
                .Where(e => e.ArcadeId == arcade.ArcadeId)
                .ToListAsync();

            // Assert
            Assert.Single(events);
            Assert.Equal("VR Tournament", events[0].Title);
        }

        #endregion

        #region Full Data Seeding Test

        [Fact]
        public void SeedAll_CreatesCompleteDataSet()
        {
            // Act
            TestDataSeeder.SeedAll(_context);

            // Assert
            Assert.True(_context.Arcades.Any());
            Assert.True(_context.VRGames.Any());
            Assert.True(_context.ArcadeGames.Any());
            Assert.True(_context.Reviews.Any());
            Assert.True(_context.BlogPosts.Any());
            Assert.True(_context.Cities.Any());
        }

        #endregion
    }
}
