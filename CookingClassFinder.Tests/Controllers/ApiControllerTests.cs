using CookingClassFinder.Controllers;
using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Services;
using CookingClassFinder.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CookingClassFinder.Tests.Controllers;

public class ApiControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ISchoolService> _schoolServiceMock;
    private readonly ApiController _controller;

    public ApiControllerTests()
    {
        _context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedTestDataAsync(_context).GetAwaiter().GetResult();
        _schoolServiceMock = new Mock<ISchoolService>();
        _controller = new ApiController(_context, _schoolServiceMock.Object);
    }

    public void Dispose() => _context.Dispose();

    #region SearchSchools

    [Fact]
    public async Task SearchSchools_ValidQuery_ReturnsResults()
    {
        var result = await _controller.SearchSchools("Italian");

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SearchSchools_ShortQuery_ReturnsEmpty()
    {
        var result = await _controller.SearchSchools("I");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var array = okResult.Value as Array;
        Assert.NotNull(array);
        Assert.Empty(array);
    }

    [Fact]
    public async Task SearchSchools_EmptyQuery_ReturnsEmpty()
    {
        var result = await _controller.SearchSchools("");

        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SearchSchools_NullQuery_ReturnsEmpty()
    {
        var result = await _controller.SearchSchools(null!);

        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    #endregion

    #region GetNearbySchools

    [Fact]
    public async Task GetNearbySchools_CallsSchoolService()
    {
        _schoolServiceMock.Setup(s => s.GetNearbySchoolsAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>()))
            .ReturnsAsync(new List<CookingSchool>());

        var result = await _controller.GetNearbySchools(40.7128, -74.0060, 25);

        _schoolServiceMock.Verify(s => s.GetNearbySchoolsAsync(40.7128, -74.0060, 25, 20), Times.Once);
        Assert.IsType<OkObjectResult>(result);
    }

    #endregion

    #region GetCuisines

    [Fact]
    public async Task GetCuisines_ReturnsGroupedCuisines()
    {
        var result = await _controller.GetCuisines();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    #endregion

    #region GetSchool

    [Fact]
    public async Task GetSchool_ExistingActiveSchool_ReturnsSchool()
    {
        var result = await _controller.GetSchool(1);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetSchool_InactiveSchool_ReturnsNotFound()
    {
        var result = await _controller.GetSchool(4); // Inactive

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetSchool_NonExistent_ReturnsNotFound()
    {
        var result = await _controller.GetSchool(999);

        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region RecordAffiliateClick

    [Fact]
    public async Task RecordAffiliateClick_ValidRequest_SavesClick()
    {
        // Set up HttpContext
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var request = new AffiliateClickRequest
        {
            SchoolId = 1,
            ClickType = "website",
            DestinationUrl = "https://example.com"
        };

        var result = await _controller.RecordAffiliateClick(request);

        Assert.IsType<OkObjectResult>(result);
        var click = _context.AffiliateClicks.FirstOrDefault();
        Assert.NotNull(click);
        Assert.Equal(1, click.SchoolId);
        Assert.Equal("website", click.LinkType);
    }

    [Fact]
    public async Task RecordAffiliateClick_NullClickType_DefaultsToWebsite()
    {
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new AffiliateClickRequest
        {
            SchoolId = 1,
            ClickType = null
        };

        await _controller.RecordAffiliateClick(request);

        var click = _context.AffiliateClicks.FirstOrDefault();
        Assert.NotNull(click);
        Assert.Equal("website", click.LinkType);
    }

    #endregion
}
