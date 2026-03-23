using CookingClassFinder.Controllers;
using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Services;
using CookingClassFinder.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace CookingClassFinder.Tests.Controllers;

public class SchoolControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ISchoolService> _schoolServiceMock;
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly SchoolController _controller;

    public SchoolControllerTests()
    {
        _context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedTestDataAsync(_context).GetAwaiter().GetResult();

        _schoolServiceMock = new Mock<ISchoolService>();
        var store = new Mock<IUserStore<User>>();
        _userManagerMock = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var logger = new Mock<ILogger<SchoolController>>();

        _controller = new SchoolController(_schoolServiceMock.Object, _context, _userManagerMock.Object, logger.Object);

        // Setup TempData
        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        _controller.TempData = tempData;
    }

    public void Dispose() => _context.Dispose();

    #region Details

    [Fact]
    public async Task Details_ExistingSchool_ReturnsView()
    {
        var school = new CookingSchool { SchoolId = 1, Name = "Test", Slug = "test-slug" };
        _schoolServiceMock.Setup(s => s.GetBySlugAsync("test-slug")).ReturnsAsync(school);

        var result = await _controller.Details("test-slug");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(school, viewResult.Model);
    }

    [Fact]
    public async Task Details_NonExistent_ReturnsNotFound()
    {
        _schoolServiceMock.Setup(s => s.GetBySlugAsync("bad-slug")).ReturnsAsync((CookingSchool?)null);

        var result = await _controller.Details("bad-slug");

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region ClassDetails

    [Fact]
    public async Task ClassDetails_ExistingClass_ReturnsView()
    {
        var result = await _controller.ClassDetails("italian-kitchen-academy-new-york", "pasta-making-101");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CookingClass>(viewResult.Model);
        Assert.Equal("Pasta Making 101", model.Name);
    }

    [Fact]
    public async Task ClassDetails_NonExistent_ReturnsNotFound()
    {
        var result = await _controller.ClassDetails("fake-school", "fake-class");
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ClassDetails_InactiveClass_ReturnsNotFound()
    {
        var result = await _controller.ClassDetails("italian-kitchen-academy-new-york", "inactive-class");
        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region AddReview

    [Fact]
    public async Task AddReview_SchoolNotFound_ReturnsNotFound()
    {
        _schoolServiceMock.Setup(s => s.GetBySlugAsync("bad")).ReturnsAsync((CookingSchool?)null);

        var result = await _controller.AddReview("bad", 5, "Good", "Content");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AddReview_UserNotAuth_ReturnsUnauthorized()
    {
        var school = new CookingSchool { SchoolId = 1, Name = "Test" };
        _schoolServiceMock.Setup(s => s.GetBySlugAsync("test")).ReturnsAsync(school);
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((User?)null);

        var result = await _controller.AddReview("test", 5, "Good", "Content");

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task AddReview_ValidReview_CreatesAndRedirects()
    {
        var school = new CookingSchool { SchoolId = 1, Name = "Test" };
        var user = new User { Id = 1, UserName = "testuser" };

        _schoolServiceMock.Setup(s => s.GetBySlugAsync("test")).ReturnsAsync(school);
        _userManagerMock.Setup(u => u.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        var result = await _controller.AddReview("test", 4, "Great!", "Loved it");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);

        var review = _context.Reviews.OrderByDescending(r => r.ReviewId).First();
        Assert.Equal(4, review.Rating);
        Assert.Equal("Great!", review.Title);
        Assert.False(review.IsApproved); // Pending approval
    }

    #endregion

    #region Cuisines

    [Fact]
    public async Task Cuisines_ReturnsView()
    {
        _schoolServiceMock.Setup(s => s.GetAllCuisineTypesAsync())
            .ReturnsAsync(new List<string> { "Italian", "French" });

        var result = await _controller.Cuisines();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<string>>(viewResult.Model);
        Assert.Equal(2, model.Count);
    }

    #endregion

    #region ByCuisine

    [Fact]
    public async Task ByCuisine_ReturnsFilteredSchools()
    {
        var mockResults = new List<CookingClassFinder.Models.DTOs.SchoolSearchResultDto>
        {
            new() { SchoolId = 1, Name = "Test" }
        };

        _schoolServiceMock.Setup(s => s.FilterSchoolsAsync(null, null, "Italian", null, null, null))
            .ReturnsAsync(mockResults);

        var result = await _controller.ByCuisine("Italian");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Italian", _controller.ViewBag.Cuisine);
    }

    #endregion
}
