using CookingClassFinder.Controllers;
using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Models.ViewModels;
using CookingClassFinder.Services;
using CookingClassFinder.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CookingClassFinder.Tests.Controllers;

public class BlogControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IBlogService> _blogServiceMock;
    private readonly BlogController _controller;

    public BlogControllerTests()
    {
        _context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedTestDataAsync(_context).GetAwaiter().GetResult();
        _blogServiceMock = new Mock<IBlogService>();
        _controller = new BlogController(_context, _blogServiceMock.Object);
    }

    public void Dispose() => _context.Dispose();

    #region Index

    [Fact]
    public async Task Index_ReturnsPublishedPosts()
    {
        var result = await _controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<BlogPost>>(viewResult.Model);
        Assert.Equal(2, model.Count); // 2 published
        Assert.All(model, p => Assert.True(p.IsPublished));
    }

    [Fact]
    public async Task Index_WithPagination_SkipsCorrectly()
    {
        var result = await _controller.Index(page: 2);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<BlogPost>>(viewResult.Model);
        // Page 2 with 10 per page - only 2 published posts total
        Assert.Empty(model);
    }

    #endregion

    #region Post

    [Fact]
    public async Task Post_ExistingPublished_ReturnsView()
    {
        var result = await _controller.Post("best-cooking-classes-nyc");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<BlogPost>(viewResult.Model);
        Assert.Equal("Best Cooking Classes in NYC", model.Title);
    }

    [Fact]
    public async Task Post_ExistingPublished_IncrementsViewCount()
    {
        var originalCount = (await _context.BlogPosts.FindAsync(1))!.ViewCount;

        await _controller.Post("best-cooking-classes-nyc");

        var post = await _context.BlogPosts.FindAsync(1);
        Assert.Equal(originalCount + 1, post!.ViewCount);
    }

    [Fact]
    public async Task Post_DraftPost_ReturnsNotFound()
    {
        var result = await _controller.Post("draft-post");
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Post_NonExistent_ReturnsNotFound()
    {
        var result = await _controller.Post("nonexistent-slug");
        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Cities

    [Fact]
    public async Task Cities_ReturnsCitiesWithSchools()
    {
        var result = await _controller.Cities();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<CityBlogItem>>(viewResult.Model);
        Assert.All(model, c => Assert.True(c.SchoolCount > 0));
    }

    #endregion

    #region CityGuide

    [Fact]
    public async Task CityGuide_ExistingCity_ReturnsView()
    {
        var result = await _controller.CityGuide("new-york");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<City>(viewResult.Model);
        Assert.Equal("New York", model.Name);
    }

    [Fact]
    public async Task CityGuide_NonExistent_ReturnsNotFound()
    {
        var result = await _controller.CityGuide("nonexistent-city");
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CityGuide_SetsSchoolsInViewBag()
    {
        var result = await _controller.CityGuide("new-york");

        var viewResult = Assert.IsType<ViewResult>(result);
        var schools = _controller.ViewBag.Schools as List<CookingSchool>;
        Assert.NotNull(schools);
        Assert.All(schools, s => Assert.True(s.IsActive));
    }

    #endregion
}
