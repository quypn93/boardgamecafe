using CookingClassFinder.Controllers;
using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Services;
using CookingClassFinder.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CookingClassFinder.Tests.Controllers;

public class AdminControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAutoCrawlService> _crawlServiceMock;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedTestDataAsync(_context).GetAwaiter().GetResult();
        _crawlServiceMock = new Mock<IAutoCrawlService>();
        _controller = new AdminController(_context, _crawlServiceMock.Object);

        // Setup HttpContext and TempData for controller
        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        _controller.TempData = tempData;
    }

    public void Dispose() => _context.Dispose();

    #region Dashboard

    [Fact]
    public async Task Dashboard_ReturnsViewWithStats()
    {
        var result = await _controller.Dashboard();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(4, (int)_controller.ViewBag.TotalSchools);
        Assert.Equal(5, (int)_controller.ViewBag.TotalClasses);
        Assert.Equal(4, (int)_controller.ViewBag.TotalReviews);
    }

    [Fact]
    public async Task Dashboard_CountsPendingReviews()
    {
        var result = await _controller.Dashboard();

        Assert.Equal(1, (int)_controller.ViewBag.PendingReviews);
    }

    #endregion

    #region Index

    [Fact]
    public void Index_RedirectsToDashboard()
    {
        var result = _controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirect.ActionName);
    }

    #endregion

    #region Schools

    [Fact]
    public async Task Schools_ReturnsAllSchools()
    {
        var result = await _controller.Schools();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<CookingSchool>>(viewResult.Model);
        Assert.Equal(4, model.Count); // All schools including inactive
    }

    #endregion

    #region Classes

    [Fact]
    public async Task Classes_ReturnsAllClasses()
    {
        var result = await _controller.Classes();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<CookingClass>>(viewResult.Model);
        Assert.Equal(5, model.Count);
    }

    #endregion

    #region Reviews

    [Fact]
    public async Task Reviews_NoFilter_ReturnsAll()
    {
        var result = await _controller.Reviews(null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<Review>>(viewResult.Model);
        Assert.Equal(4, model.Count);
    }

    [Fact]
    public async Task Reviews_PendingFilter_ReturnsPending()
    {
        var result = await _controller.Reviews("pending");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<Review>>(viewResult.Model);
        Assert.Single(model);
        Assert.All(model, r => Assert.False(r.IsApproved));
    }

    [Fact]
    public async Task Reviews_ApprovedFilter_ReturnsApproved()
    {
        var result = await _controller.Reviews("approved");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<Review>>(viewResult.Model);
        Assert.Equal(3, model.Count);
        Assert.All(model, r => Assert.True(r.IsApproved));
    }

    #endregion

    #region ApproveReview

    [Fact]
    public async Task ApproveReview_ExistingReview_ApprovesAndRedirects()
    {
        var result = await _controller.ApproveReview(3); // Pending review

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Reviews", redirect.ActionName);

        var review = await _context.Reviews.FindAsync(3);
        Assert.True(review!.IsApproved);
    }

    [Fact]
    public async Task ApproveReview_NonExistent_StillRedirects()
    {
        var result = await _controller.ApproveReview(999);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Reviews", redirect.ActionName);
    }

    #endregion

    #region DeleteReview

    [Fact]
    public async Task DeleteReview_ExistingReview_DeletesAndRedirects()
    {
        var result = await _controller.DeleteReview(3);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Reviews", redirect.ActionName);

        var deleted = await _context.Reviews.FindAsync(3);
        Assert.Null(deleted);
    }

    #endregion

    #region City Management

    [Fact]
    public async Task Cities_ReturnsAllCities()
    {
        _crawlServiceMock.Setup(s => s.IsRunning).Returns(false);

        var result = await _controller.Cities(null, null, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<City>>(viewResult.Model);
        Assert.True(model.Count > 0);
    }

    [Fact]
    public async Task Cities_NeverCrawledFilter_Works()
    {
        _crawlServiceMock.Setup(s => s.IsRunning).Returns(false);

        var result = await _controller.Cities("never", null, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<City>>(viewResult.Model);
        Assert.All(model, c => Assert.Equal(0, c.CrawlCount));
    }

    [Fact]
    public async Task Cities_FailedFilter_Works()
    {
        _crawlServiceMock.Setup(s => s.IsRunning).Returns(false);

        var result = await _controller.Cities("failed", null, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<City>>(viewResult.Model);
        Assert.All(model, c => Assert.Equal("Failed", c.LastCrawlStatus));
    }

    [Fact]
    public async Task Cities_USRegionFilter_Works()
    {
        _crawlServiceMock.Setup(s => s.IsRunning).Returns(false);

        var result = await _controller.Cities(null, "US", null);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<City>>(viewResult.Model);
        Assert.All(model, c => Assert.Equal("US", c.Region));
    }

    [Fact]
    public async Task Cities_SearchFilter_Works()
    {
        _crawlServiceMock.Setup(s => s.IsRunning).Returns(false);

        var result = await _controller.Cities(null, null, "Tokyo");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<City>>(viewResult.Model);
        Assert.Single(model);
        Assert.Equal("Tokyo", model[0].Name);
    }

    #endregion

    #region CrawlCity

    [Fact]
    public async Task CrawlCity_ValidCity_CallsCrawlService()
    {
        _crawlServiceMock.Setup(s => s.CrawlCityAsync(It.IsAny<City>()))
            .ReturnsAsync(new CrawlResult { Success = true, SchoolsFound = 5, SchoolsAdded = 3, SchoolsUpdated = 2 });

        var result = await _controller.CrawlCity(1);

        var jsonResult = Assert.IsType<JsonResult>(result);
        _crawlServiceMock.Verify(s => s.CrawlCityAsync(It.Is<City>(c => c.CityId == 1)), Times.Once);
    }

    [Fact]
    public async Task CrawlCity_NonExistentCity_ReturnsFailure()
    {
        var result = await _controller.CrawlCity(999);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var value = jsonResult.Value;
        var successProp = value!.GetType().GetProperty("success");
        Assert.False((bool)successProp!.GetValue(value)!);
    }

    #endregion

    #region BulkCrawlCities

    [Fact]
    public async Task BulkCrawlCities_EmptyArray_ReturnsFailure()
    {
        var result = await _controller.BulkCrawlCities(Array.Empty<int>());

        var jsonResult = Assert.IsType<JsonResult>(result);
        var value = jsonResult.Value;
        var msgProp = value!.GetType().GetProperty("message");
        Assert.Contains("No cities selected", (string)msgProp!.GetValue(value)!);
    }

    [Fact]
    public async Task BulkCrawlCities_Null_ReturnsFailure()
    {
        var result = await _controller.BulkCrawlCities(null!);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var value = jsonResult.Value;
        var msgProp = value!.GetType().GetProperty("message");
        Assert.Contains("No cities selected", (string)msgProp!.GetValue(value)!);
    }

    [Fact]
    public async Task BulkCrawlCities_ValidIds_CrawlsEachCity()
    {
        _crawlServiceMock.Setup(s => s.CrawlCityAsync(It.IsAny<City>()))
            .ReturnsAsync(new CrawlResult { Success = true });

        var result = await _controller.BulkCrawlCities(new[] { 1, 2 });

        _crawlServiceMock.Verify(s => s.CrawlCityAsync(It.IsAny<City>()), Times.Exactly(2));
    }

    #endregion

    #region AddCity

    [Fact]
    public async Task AddCity_ValidCity_AddsAndRedirects()
    {
        var countBefore = await _context.Cities.CountAsync();

        var result = await _controller.AddCity("New City", "US", "US", 15);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Cities", redirect.ActionName);

        var countAfter = await _context.Cities.CountAsync();
        Assert.Equal(countBefore + 1, countAfter);
    }

    [Fact]
    public async Task AddCity_EmptyName_RedirectsWithError()
    {
        var result = await _controller.AddCity("", null, "US");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Cities", redirect.ActionName);
    }

    #endregion

    #region ToggleCityActive

    [Fact]
    public async Task ToggleCityActive_ActiveCity_DeactivatesIt()
    {
        var result = await _controller.ToggleCityActive(1);

        var city = await _context.Cities.FindAsync(1);
        Assert.False(city!.IsActive);
    }

    [Fact]
    public async Task ToggleCityActive_InactiveCity_ActivatesIt()
    {
        var result = await _controller.ToggleCityActive(5); // Inactive city

        var city = await _context.Cities.FindAsync(5);
        Assert.True(city!.IsActive);
    }

    [Fact]
    public async Task ToggleCityActive_NonExistent_RedirectsWithError()
    {
        var result = await _controller.ToggleCityActive(999);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Cities", redirect.ActionName);
    }

    #endregion

    #region DeleteCity

    [Fact]
    public async Task DeleteCity_ExistingCity_DeletesWithHistory()
    {
        // Add some crawl history first
        _context.CrawlHistories.Add(new CrawlHistory { CityId = 3, Status = "Success" });
        await _context.SaveChangesAsync();

        var result = await _controller.DeleteCity(3);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Cities", redirect.ActionName);

        Assert.Null(await _context.Cities.FindAsync(3));
    }

    #endregion

    #region StopAutoCrawl

    [Fact]
    public void StopAutoCrawl_CallsServiceStop()
    {
        var result = _controller.StopAutoCrawl();

        _crawlServiceMock.Verify(s => s.Stop(), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("CrawlStatus", redirect.ActionName);
    }

    #endregion

    #region SeedCities

    [Fact]
    public async Task SeedCities_CallsServiceSeedCities()
    {
        var result = await _controller.SeedCities();

        _crawlServiceMock.Verify(s => s.SeedCitiesAsync(), Times.Once);
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Cities", redirect.ActionName);
    }

    #endregion

    #region UpdateCityMaxResults

    [Fact]
    public async Task UpdateCityMaxResults_ValidCity_Updates()
    {
        var result = await _controller.UpdateCityMaxResults(1, 30);

        var jsonResult = Assert.IsType<JsonResult>(result);
        var city = await _context.Cities.FindAsync(1);
        Assert.Equal(30, city!.MaxResults);
    }

    [Fact]
    public async Task UpdateCityMaxResults_ClampsToRange()
    {
        await _controller.UpdateCityMaxResults(1, 100); // Over max of 50
        var city = await _context.Cities.FindAsync(1);
        Assert.Equal(50, city!.MaxResults);

        await _controller.UpdateCityMaxResults(1, 1); // Under min of 5
        city = await _context.Cities.FindAsync(1);
        Assert.Equal(5, city!.MaxResults);
    }

    #endregion
}
