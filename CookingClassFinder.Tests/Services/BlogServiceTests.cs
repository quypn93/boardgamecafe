using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Services;
using CookingClassFinder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace CookingClassFinder.Tests.Services;

public class BlogServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly BlogService _service;

    public BlogServiceTests()
    {
        _context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedTestDataAsync(_context).GetAwaiter().GetResult();
        var logger = new Mock<ILogger<BlogService>>();
        _service = new BlogService(_context, logger.Object);
    }

    public void Dispose() => _context.Dispose();

    #region GetPublishedPostsAsync

    [Fact]
    public async Task GetPublishedPostsAsync_ReturnsOnlyPublished()
    {
        var result = await _service.GetPublishedPostsAsync();

        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.True(p.IsPublished));
    }

    [Fact]
    public async Task GetPublishedPostsAsync_OrderedByPublishedDate()
    {
        var result = await _service.GetPublishedPostsAsync();

        Assert.True(result[0].PublishedAt >= result[1].PublishedAt);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_Pagination_FirstPage()
    {
        var result = await _service.GetPublishedPostsAsync(page: 1, pageSize: 1);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_Pagination_SecondPage()
    {
        var result = await _service.GetPublishedPostsAsync(page: 2, pageSize: 1);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_EmptyPage_ReturnsEmpty()
    {
        var result = await _service.GetPublishedPostsAsync(page: 10, pageSize: 10);
        Assert.Empty(result);
    }

    #endregion

    #region GetPostBySlugAsync

    [Fact]
    public async Task GetPostBySlugAsync_PublishedPost_ReturnsPost()
    {
        var result = await _service.GetPostBySlugAsync("best-cooking-classes-nyc");

        Assert.NotNull(result);
        Assert.Equal("Best Cooking Classes in NYC", result.Title);
    }

    [Fact]
    public async Task GetPostBySlugAsync_DraftPost_ReturnsNull()
    {
        var result = await _service.GetPostBySlugAsync("draft-post");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPostBySlugAsync_InvalidSlug_ReturnsNull()
    {
        var result = await _service.GetPostBySlugAsync("nonexistent");
        Assert.Null(result);
    }

    #endregion

    #region GetPostByIdAsync

    [Fact]
    public async Task GetPostByIdAsync_ExistingId_ReturnsPost()
    {
        var result = await _service.GetPostByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task GetPostByIdAsync_DraftPost_ReturnsPost()
    {
        // GetPostByIdAsync returns even unpublished posts (for admin)
        var result = await _service.GetPostByIdAsync(3);

        Assert.NotNull(result);
        Assert.False(result.IsPublished);
    }

    [Fact]
    public async Task GetPostByIdAsync_InvalidId_ReturnsNull()
    {
        var result = await _service.GetPostByIdAsync(999);
        Assert.Null(result);
    }

    #endregion

    #region CreatePostAsync

    [Fact]
    public async Task CreatePostAsync_GeneratesSlug()
    {
        var post = new BlogPost
        {
            Title = "My New Blog Post!",
            Content = "Some content",
            IsPublished = false
        };

        var result = await _service.CreatePostAsync(post);

        Assert.NotEmpty(result.Slug);
        Assert.Contains("my-new-blog-post", result.Slug);
    }

    [Fact]
    public async Task CreatePostAsync_SetsCreatedAt()
    {
        var post = new BlogPost
        {
            Title = "Timestamp Test",
            Content = "Content"
        };

        var before = DateTime.UtcNow;
        var result = await _service.CreatePostAsync(post);

        Assert.InRange(result.CreatedAt, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task CreatePostAsync_PublishedPost_SetsPublishedAt()
    {
        var post = new BlogPost
        {
            Title = "Published Immediately",
            Content = "Content",
            IsPublished = true
        };

        var result = await _service.CreatePostAsync(post);

        Assert.NotNull(result.PublishedAt);
    }

    [Fact]
    public async Task CreatePostAsync_UnpublishedPost_NoPublishedAt()
    {
        var post = new BlogPost
        {
            Title = "Draft Post New",
            Content = "Content",
            IsPublished = false
        };

        var result = await _service.CreatePostAsync(post);

        Assert.Null(result.PublishedAt);
    }

    [Fact]
    public async Task CreatePostAsync_DuplicateTitle_GeneratesUniqueSlug()
    {
        var post1 = new BlogPost { Title = "Unique Title Test", Content = "Content 1" };
        var post2 = new BlogPost { Title = "Unique Title Test", Content = "Content 2" };

        var result1 = await _service.CreatePostAsync(post1);
        var result2 = await _service.CreatePostAsync(post2);

        Assert.NotEqual(result1.Slug, result2.Slug);
    }

    #endregion

    #region UpdatePostAsync

    [Fact]
    public async Task UpdatePostAsync_SetsUpdatedAt()
    {
        var post = await _context.BlogPosts.FindAsync(1);
        Assert.NotNull(post);

        post.Title = "Updated Title";
        var result = await _service.UpdatePostAsync(post);

        Assert.NotNull(result.UpdatedAt);
    }

    [Fact]
    public async Task UpdatePostAsync_PublishDraft_SetsPublishedAt()
    {
        var post = await _context.BlogPosts.FindAsync(3); // Draft post
        Assert.NotNull(post);
        Assert.False(post.IsPublished);

        post.IsPublished = true;
        var result = await _service.UpdatePostAsync(post);

        Assert.NotNull(result.PublishedAt);
    }

    #endregion

    #region DeletePostAsync

    [Fact]
    public async Task DeletePostAsync_ExistingPost_ReturnsTrue()
    {
        var result = await _service.DeletePostAsync(3);

        Assert.True(result);
        var deleted = await _context.BlogPosts.FindAsync(3);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeletePostAsync_NonExistent_ReturnsFalse()
    {
        var result = await _service.DeletePostAsync(999);
        Assert.False(result);
    }

    #endregion

    #region GetPostsByCityAsync

    [Fact]
    public async Task GetPostsByCityAsync_ExistingCity_ReturnsPosts()
    {
        var result = await _service.GetPostsByCityAsync("New York");

        Assert.Single(result);
        Assert.Equal("Best Cooking Classes in NYC", result[0].Title);
    }

    [Fact]
    public async Task GetPostsByCityAsync_NoMatch_ReturnsEmpty()
    {
        var result = await _service.GetPostsByCityAsync("Nonexistent City");
        Assert.Empty(result);
    }

    #endregion

    #region GetPostsByCuisineAsync

    [Fact]
    public async Task GetPostsByCuisineAsync_ExistingCuisine_ReturnsPosts()
    {
        var result = await _service.GetPostsByCuisineAsync("Italian");

        Assert.Single(result);
        Assert.Equal("Italian Cooking Guide", result[0].Title);
    }

    [Fact]
    public async Task GetPostsByCuisineAsync_NoMatch_ReturnsEmpty()
    {
        var result = await _service.GetPostsByCuisineAsync("Martian");
        Assert.Empty(result);
    }

    #endregion

    #region IncrementViewCountAsync

    [Fact]
    public async Task IncrementViewCountAsync_IncrementsCount()
    {
        var originalCount = (await _context.BlogPosts.FindAsync(1))!.ViewCount;

        await _service.IncrementViewCountAsync(1);

        var updatedPost = await _context.BlogPosts.FindAsync(1);
        Assert.Equal(originalCount + 1, updatedPost!.ViewCount);
    }

    [Fact]
    public async Task IncrementViewCountAsync_NonExistent_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() =>
            _service.IncrementViewCountAsync(999));
        Assert.Null(exception);
    }

    #endregion
}
