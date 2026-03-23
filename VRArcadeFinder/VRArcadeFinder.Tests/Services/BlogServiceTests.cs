using Microsoft.Extensions.Logging;
using Moq;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models.Domain;
using VRArcadeFinder.Services;
using VRArcadeFinder.Tests.Helpers;

namespace VRArcadeFinder.Tests.Services
{
    public class BlogServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly BlogService _service;
        private readonly Mock<ILogger<BlogService>> _loggerMock;

        public BlogServiceTests()
        {
            _context = TestDbContextFactory.Create();
            _loggerMock = new Mock<ILogger<BlogService>>();
            _service = new BlogService(_context, _loggerMock.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region CRUD Tests

        [Fact]
        public async Task GetAllPostsAsync_ReturnsOnlyPublished_ByDefault()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);

            // Act
            var results = await _service.GetAllPostsAsync();

            // Assert
            Assert.All(results, p => Assert.True(p.IsPublished));
        }

        [Fact]
        public async Task GetAllPostsAsync_IncludesUnpublished_WhenRequested()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);

            // Act
            var results = await _service.GetAllPostsAsync(includeUnpublished: true);

            // Assert
            Assert.Equal(5, results.Count);
        }

        [Fact]
        public async Task GetAllPostsAsync_ReturnsOrderedByCreatedAtDescending()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);

            // Act
            var results = await _service.GetAllPostsAsync(includeUnpublished: true);

            // Assert
            if (results.Count > 1)
            {
                for (int i = 1; i < results.Count; i++)
                {
                    Assert.True(results[i].CreatedAt <= results[i - 1].CreatedAt);
                }
            }
        }

        [Fact]
        public async Task GetPostByIdAsync_ReturnsCorrectPost()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);
            var post = _context.BlogPosts.First();

            // Act
            var result = await _service.GetPostByIdAsync(post.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(post.Title, result.Title);
        }

        [Fact]
        public async Task GetPostByIdAsync_ReturnsNullForInvalidId()
        {
            // Act
            var result = await _service.GetPostByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetPostBySlugAsync_ReturnsCorrectPost()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);

            // Act
            var result = await _service.GetPostBySlugAsync("blog-post-1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Blog Post 1", result.Title);
        }

        [Fact]
        public async Task CreatePostAsync_SetsTimestampAndSlug()
        {
            // Arrange
            var post = new BlogPost
            {
                Title = "My New Blog Post",
                Content = "<p>Great content</p>",
                IsPublished = true
            };

            // Act
            var result = await _service.CreatePostAsync(post);

            // Assert
            Assert.True(result.Id > 0);
            Assert.NotEmpty(result.Slug);
            Assert.True(result.CreatedAt <= DateTime.UtcNow);
            Assert.NotNull(result.PublishedAt);
        }

        [Fact]
        public async Task CreatePostAsync_GeneratesUniqueSlug()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);
            var post = new BlogPost
            {
                Title = "Blog Post 1", // Same title as existing
                Content = "<p>Different content</p>"
            };

            // Act
            var result = await _service.CreatePostAsync(post);

            // Assert
            Assert.NotEqual("blog-post-1", result.Slug); // Should be different from existing
        }

        [Fact]
        public async Task CreatePostAsync_DoesNotSetPublishedAt_WhenNotPublished()
        {
            // Arrange
            var post = new BlogPost
            {
                Title = "Draft Post",
                Content = "<p>Draft content</p>",
                IsPublished = false
            };

            // Act
            var result = await _service.CreatePostAsync(post);

            // Assert
            Assert.Null(result.PublishedAt);
        }

        [Fact]
        public async Task UpdatePostAsync_SetsUpdatedAt()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);
            var post = _context.BlogPosts.First();
            post.Title = "Updated Title";

            // Act
            var result = await _service.UpdatePostAsync(post);

            // Assert
            Assert.NotNull(result.UpdatedAt);
        }

        [Fact]
        public async Task UpdatePostAsync_SetsPublishedAt_WhenPublished()
        {
            // Arrange
            var post = new BlogPost
            {
                Title = "Draft",
                Slug = "draft",
                Content = "<p>Draft</p>",
                IsPublished = false
            };
            _context.BlogPosts.Add(post);
            await _context.SaveChangesAsync();

            post.IsPublished = true;

            // Act
            var result = await _service.UpdatePostAsync(post);

            // Assert
            Assert.NotNull(result.PublishedAt);
        }

        [Fact]
        public async Task DeletePostAsync_RemovesPost()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);
            var post = _context.BlogPosts.First();
            var originalCount = _context.BlogPosts.Count();

            // Act
            var result = await _service.DeletePostAsync(post.Id);

            // Assert
            Assert.True(result);
            Assert.Equal(originalCount - 1, _context.BlogPosts.Count());
        }

        [Fact]
        public async Task DeletePostAsync_ReturnsFalseForNonExistent()
        {
            // Act
            var result = await _service.DeletePostAsync(999);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Query Tests

        [Fact]
        public async Task GetPublishedPostsAsync_ReturnsPaginatedResults()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);

            // Act
            var page1 = await _service.GetPublishedPostsAsync(page: 1, pageSize: 2);
            var page2 = await _service.GetPublishedPostsAsync(page: 2, pageSize: 2);

            // Assert
            Assert.True(page1.Count <= 2);
            Assert.True(page2.Count <= 2);
            Assert.All(page1, p => Assert.True(p.IsPublished));
        }

        [Fact]
        public async Task GetPostsByCategoryAsync_FiltersCorrectly()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);

            // Act
            var results = await _service.GetPostsByCategoryAsync("Top Games");

            // Assert
            Assert.All(results, p => Assert.Equal("Top Games", p.Category));
        }

        [Fact]
        public async Task GetPostsByCityAsync_FiltersCorrectly()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);

            // Act
            var results = await _service.GetPostsByCityAsync("New York");

            // Assert
            Assert.All(results, p => Assert.Equal("New York", p.RelatedCity));
        }

        [Fact]
        public async Task GetTotalPublishedCountAsync_ReturnsCorrectCount()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);

            // Act
            var count = await _service.GetTotalPublishedCountAsync();

            // Assert
            Assert.Equal(3, count); // Posts 1-3 are published
        }

        [Fact]
        public async Task GetAllCategoriesAsync_ReturnsDistinctCategories()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);

            // Act
            var categories = await _service.GetAllCategoriesAsync();

            // Assert
            Assert.Contains("Top Games", categories);
            Assert.Contains("City Guide", categories);
            Assert.Equal(categories.Distinct().Count(), categories.Count);
        }

        [Fact]
        public async Task GetAllCitiesWithPostsAsync_ReturnsDistinctCities()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);

            // Act
            var cities = await _service.GetAllCitiesWithPostsAsync();

            // Assert
            Assert.NotEmpty(cities);
            Assert.Equal(cities.Distinct().Count(), cities.Count);
        }

        #endregion

        #region Auto-Generation Tests

        [Fact]
        public async Task GenerateTopGamesPostAsync_CreatesPost()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            TestDataSeeder.SeedGames(_context);
            TestDataSeeder.SeedArcadeGames(_context);

            // Act
            var result = await _service.GenerateTopGamesPostAsync("New York");

            // Assert
            Assert.NotNull(result);
            Assert.Contains("New York", result.Title);
            Assert.Equal("Top Games", result.Category);
            Assert.Equal("top-games", result.PostType);
            Assert.True(result.IsAutoGenerated);
            Assert.False(result.IsPublished);
            Assert.Equal("New York", result.RelatedCity);
        }

        [Fact]
        public async Task GenerateTopGamesPostAsync_ThrowsWhenNoArcadesInCity()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.GenerateTopGamesPostAsync("Nonexistent City"));
        }

        [Fact]
        public async Task GenerateCityGuidePostAsync_CreatesPost()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var result = await _service.GenerateCityGuidePostAsync("New York");

            // Assert
            Assert.NotNull(result);
            Assert.Contains("New York", result.Title);
            Assert.Equal("City Guide", result.Category);
            Assert.Equal("city-guide", result.PostType);
            Assert.True(result.IsAutoGenerated);
        }

        [Fact]
        public async Task GenerateCityGuidePostAsync_ThrowsWhenNoArcadesInCity()
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.GenerateCityGuidePostAsync("Ghost City"));
        }

        [Fact]
        public async Task GenerateTopGamesPostsForCitiesAsync_SkipsExistingPosts()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            TestDataSeeder.SeedGames(_context);
            TestDataSeeder.SeedArcadeGames(_context);

            // Generate first post
            await _service.GenerateTopGamesPostAsync("New York");

            // Act - try to generate again for same city
            var results = await _service.GenerateTopGamesPostsForCitiesAsync(new List<string> { "New York" });

            // Assert
            Assert.Empty(results); // Should skip existing
        }

        #endregion

        #region Utility Tests

        [Theory]
        [InlineData("Hello World", "hello-world")]
        [InlineData("Top VR Games in New York!", "top-vr-games-in-new-york")]
        [InlineData("  spaces  everywhere  ", "spaces-everywhere")]
        [InlineData("Special@#$Characters", "specialcharacters")]
        public void GenerateSlug_ProducesCorrectSlugs(string input, string expected)
        {
            // Act
            var result = _service.GenerateSlug(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GenerateSlug_TruncatesLongSlugs()
        {
            // Arrange
            var longTitle = new string('a', 200);

            // Act
            var result = _service.GenerateSlug(longTitle);

            // Assert
            Assert.True(result.Length <= 100);
        }

        [Fact]
        public async Task IncrementViewCountAsync_IncrementsCorrectly()
        {
            // Arrange
            TestDataSeeder.SeedBlogPosts(_context);
            var post = _context.BlogPosts.First();
            var originalCount = post.ViewCount;

            // Act
            await _service.IncrementViewCountAsync(post.Id);

            // Assert
            var updatedPost = await _context.BlogPosts.FindAsync(post.Id);
            Assert.Equal(originalCount + 1, updatedPost!.ViewCount);
        }

        [Fact]
        public async Task IncrementViewCountAsync_DoesNothingForInvalidId()
        {
            // Act - should not throw
            await _service.IncrementViewCountAsync(999);
        }

        #endregion
    }
}
