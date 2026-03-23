using CookingClassFinder.Controllers;
using CookingClassFinder.Models.DTOs;
using CookingClassFinder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CookingClassFinder.Tests.Controllers;

public class HomeControllerTests
{
    private readonly Mock<ISchoolService> _schoolServiceMock;
    private readonly HomeController _controller;

    public HomeControllerTests()
    {
        _schoolServiceMock = new Mock<ISchoolService>();
        var logger = new Mock<ILogger<HomeController>>();
        _controller = new HomeController(_schoolServiceMock.Object, logger.Object);
    }

    [Fact]
    public async Task Index_ReturnsViewWithSchools()
    {
        var pagedResult = new SchoolSearchPagedResult
        {
            Schools = new List<SchoolListItemDto>
            {
                new() { SchoolId = 1, Name = "Test School" }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _schoolServiceMock.Setup(s => s.SearchSchoolsPagedAsync(It.IsAny<SchoolSearchRequest>()))
            .ReturnsAsync(pagedResult);
        _schoolServiceMock.Setup(s => s.GetAllCitiesAsync())
            .ReturnsAsync(new List<string> { "New York", "LA" });
        _schoolServiceMock.Setup(s => s.GetAllCuisineTypesAsync())
            .ReturnsAsync(new List<string> { "Italian", "French" });
        _schoolServiceMock.Setup(s => s.GetTotalSchoolCountAsync()).ReturnsAsync(10);
        _schoolServiceMock.Setup(s => s.GetTotalClassCountAsync()).ReturnsAsync(50);

        var result = await _controller.Index(null, null, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SchoolSearchPagedResult>(viewResult.Model);
        Assert.Single(model.Schools);
    }

    [Fact]
    public async Task Index_PassesSearchParams_ToService()
    {
        _schoolServiceMock.Setup(s => s.SearchSchoolsPagedAsync(It.IsAny<SchoolSearchRequest>()))
            .ReturnsAsync(new SchoolSearchPagedResult());
        _schoolServiceMock.Setup(s => s.GetAllCitiesAsync()).ReturnsAsync(new List<string>());
        _schoolServiceMock.Setup(s => s.GetAllCuisineTypesAsync()).ReturnsAsync(new List<string>());
        _schoolServiceMock.Setup(s => s.GetTotalSchoolCountAsync()).ReturnsAsync(0);
        _schoolServiceMock.Setup(s => s.GetTotalClassCountAsync()).ReturnsAsync(0);

        await _controller.Index("pizza", "New York", "Italian");

        _schoolServiceMock.Verify(s => s.SearchSchoolsPagedAsync(
            It.Is<SchoolSearchRequest>(r =>
                r.Query == "pizza" &&
                r.City == "New York" &&
                r.CuisineType == "Italian" &&
                r.PageSize == 20)),
            Times.Once);
    }

    [Fact]
    public async Task Index_SetsViewBagProperties()
    {
        _schoolServiceMock.Setup(s => s.SearchSchoolsPagedAsync(It.IsAny<SchoolSearchRequest>()))
            .ReturnsAsync(new SchoolSearchPagedResult());
        _schoolServiceMock.Setup(s => s.GetAllCitiesAsync())
            .ReturnsAsync(new List<string> { "NYC" });
        _schoolServiceMock.Setup(s => s.GetAllCuisineTypesAsync())
            .ReturnsAsync(new List<string> { "Thai" });
        _schoolServiceMock.Setup(s => s.GetTotalSchoolCountAsync()).ReturnsAsync(42);
        _schoolServiceMock.Setup(s => s.GetTotalClassCountAsync()).ReturnsAsync(100);

        var result = await _controller.Index("test", "NYC", "Thai");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("test", _controller.ViewBag.Query);
        Assert.Equal("NYC", _controller.ViewBag.City);
        Assert.Equal("Thai", _controller.ViewBag.Cuisine);
        Assert.Equal(42, _controller.ViewBag.TotalSchools);
        Assert.Equal(100, _controller.ViewBag.TotalClasses);
    }

    [Fact]
    public void About_ReturnsView()
    {
        var result = _controller.About();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        var result = _controller.Privacy();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void Terms_ReturnsView()
    {
        var result = _controller.Terms();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void ForBusiness_ReturnsView()
    {
        var result = _controller.ForBusiness();
        Assert.IsType<ViewResult>(result);
    }
}
