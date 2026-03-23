using EscapeRoomFinder.Controllers;
using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models.Domain;
using EscapeRoomFinder.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace EscapeRoomFinder.Tests
{
    public class AdminControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly AdminController _controller;
        private readonly Mock<IVenueService> _venueServiceMock;
        private readonly Mock<IBlogService> _blogServiceMock;
        private readonly Mock<IGoogleMapsCrawlerService> _crawlerServiceMock;
        private readonly Mock<IVenueWebsiteCrawlerService> _websiteCrawlerServiceMock;
        private readonly Mock<UserManager<User>> _userManagerMock;
        private readonly Mock<RoleManager<IdentityRole<int>>> _roleManagerMock;
        private readonly Mock<ILogger<AdminController>> _loggerMock;
        private readonly Mock<IWebHostEnvironment> _environmentMock;
        private readonly Mock<IAutoCrawlService> _autoCrawlServiceMock;
        private readonly string _tempWebRoot;

        public AdminControllerTests()
        {
            _context = TestDbContextFactory.Create();

            _venueServiceMock = new Mock<IVenueService>();
            _blogServiceMock = new Mock<IBlogService>();
            _crawlerServiceMock = new Mock<IGoogleMapsCrawlerService>();
            _websiteCrawlerServiceMock = new Mock<IVenueWebsiteCrawlerService>();
            _loggerMock = new Mock<ILogger<AdminController>>();

            // Setup UserManager mock
            var userStoreMock = new Mock<IUserStore<User>>();
            _userManagerMock = new Mock<UserManager<User>>(
                userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            // Setup RoleManager mock
            var roleStoreMock = new Mock<IRoleStore<IdentityRole<int>>>();
            _roleManagerMock = new Mock<RoleManager<IdentityRole<int>>>(
                roleStoreMock.Object, null!, null!, null!, null!);

            // Setup WebHostEnvironment mock with temp directory
            _tempWebRoot = Path.Combine(Path.GetTempPath(), "EscapeRoomFinderTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempWebRoot);
            _environmentMock = new Mock<IWebHostEnvironment>();
            _environmentMock.Setup(e => e.WebRootPath).Returns(_tempWebRoot);

            _autoCrawlServiceMock = new Mock<IAutoCrawlService>();

            _controller = new AdminController(
                _context,
                _venueServiceMock.Object,
                _blogServiceMock.Object,
                _crawlerServiceMock.Object,
                _websiteCrawlerServiceMock.Object,
                _userManagerMock.Object,
                _roleManagerMock.Object,
                _loggerMock.Object,
                _environmentMock.Object,
                _autoCrawlServiceMock.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            // Clean up temp directory
            if (Directory.Exists(_tempWebRoot))
            {
                try { Directory.Delete(_tempWebRoot, true); } catch { }
            }
        }

        #region Dashboard Tests

        [Fact]
        public async Task Dashboard_ReturnsViewResult()
        {
            // Act
            var result = await _controller.Dashboard();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Dashboard_ShowsCorrectCounts()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 3);

            // Act
            var result = await _controller.Dashboard() as ViewResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, _controller.ViewBag.TotalVenues);
            Assert.Equal(6, _controller.ViewBag.TotalRooms);
            Assert.Equal(6, _controller.ViewBag.TotalReviews);
        }

        [Fact]
        public async Task Dashboard_EmptyDb_ReturnsZeroCounts()
        {
            // Act
            var result = await _controller.Dashboard() as ViewResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, _controller.ViewBag.TotalVenues);
            Assert.Equal(0, _controller.ViewBag.TotalRooms);
        }

        #endregion

        #region ClearAllData Tests

        [Fact]
        public async Task ClearAllData_DeletesAllVenues()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 3);
            Assert.Equal(3, await _context.Venues.CountAsync());

            // Act
            var result = await _controller.ClearAllData();

            // Assert
            Assert.Equal(0, await _context.Venues.CountAsync());
        }

        [Fact]
        public async Task ClearAllData_DeletesAllRooms()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 3);
            Assert.Equal(6, await _context.Rooms.CountAsync());

            // Act
            await _controller.ClearAllData();

            // Assert
            Assert.Equal(0, await _context.Rooms.CountAsync());
        }

        [Fact]
        public async Task ClearAllData_DeletesAllReviews()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 2);
            Assert.Equal(4, await _context.Reviews.CountAsync());

            // Act
            await _controller.ClearAllData();

            // Assert
            Assert.Equal(0, await _context.Reviews.CountAsync());
        }

        [Fact]
        public async Task ClearAllData_DeletesAllPhotos()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 2);
            Assert.Equal(2, await _context.Photos.CountAsync());

            // Act
            await _controller.ClearAllData();

            // Assert
            Assert.Equal(0, await _context.Photos.CountAsync());
        }

        [Fact]
        public async Task ClearAllData_DeletesPremiumListings()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1);
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            _context.PremiumListings.Add(TestDbContextFactory.CreateTestPremiumListing(1, venue.VenueId));
            await _context.SaveChangesAsync();

            Assert.Equal(1, await _context.PremiumListings.CountAsync());

            // Act
            await _controller.ClearAllData();

            // Assert
            Assert.Equal(0, await _context.PremiumListings.CountAsync());
        }

        [Fact]
        public async Task ClearAllData_ReturnsSuccessJson()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 2);

            // Act
            var result = await _controller.ClearAllData();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = jsonResult.Value;
            Assert.NotNull(data);

            var successProp = data.GetType().GetProperty("success");
            Assert.NotNull(successProp);
            Assert.True((bool)successProp.GetValue(data)!);
        }

        [Fact]
        public async Task ClearAllData_ReturnsCorrectDeletedCounts()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 2);

            // Act
            var result = await _controller.ClearAllData();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = jsonResult.Value!;

            var deletedProp = data.GetType().GetProperty("deleted");
            Assert.NotNull(deletedProp);
            var deleted = deletedProp.GetValue(data)!;

            var venuesProp = deleted.GetType().GetProperty("venues");
            Assert.Equal(2, (int)venuesProp!.GetValue(deleted)!);

            var roomsProp = deleted.GetType().GetProperty("rooms");
            Assert.Equal(4, (int)roomsProp!.GetValue(deleted)!);

            var reviewsProp = deleted.GetType().GetProperty("reviews");
            Assert.Equal(4, (int)reviewsProp!.GetValue(deleted)!);
        }

        [Fact]
        public async Task ClearAllData_EmptyDb_ReturnsSuccess()
        {
            // Act
            var result = await _controller.ClearAllData();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = jsonResult.Value!;
            var successProp = data.GetType().GetProperty("success");
            Assert.True((bool)successProp!.GetValue(data)!);
        }

        [Fact]
        public async Task ClearAllData_DeletesImageFiles()
        {
            // Arrange
            var venuesDir = Path.Combine(_tempWebRoot, "images", "venues");
            Directory.CreateDirectory(venuesDir);
            File.WriteAllText(Path.Combine(venuesDir, "test1.jpg"), "fake image");
            File.WriteAllText(Path.Combine(venuesDir, "test2.jpg"), "fake image");

            var photosDir = Path.Combine(_tempWebRoot, "images", "photos");
            Directory.CreateDirectory(photosDir);
            File.WriteAllText(Path.Combine(photosDir, "photo1.jpg"), "fake image");

            // Act
            await _controller.ClearAllData();

            // Assert
            Assert.Empty(Directory.GetFiles(venuesDir));
            Assert.Empty(Directory.GetFiles(photosDir));
        }

        [Fact]
        public async Task ClearAllData_HandlesNonExistentImageFolders()
        {
            // Arrange - don't create image directories

            // Act
            var result = await _controller.ClearAllData();

            // Assert - should not throw
            var jsonResult = Assert.IsType<JsonResult>(result);
            var data = jsonResult.Value!;
            var successProp = data.GetType().GetProperty("success");
            Assert.True((bool)successProp!.GetValue(data)!);
        }

        #endregion

        #region Crawl Page Tests

        [Fact]
        public void Crawl_ReturnsViewResult()
        {
            // Act
            var result = _controller.Crawl();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        #endregion

        #region GetVenuesWithoutRooms Tests

        [Fact]
        public async Task GetVenuesWithoutRooms_ReturnsJsonResult()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1);
            venue.Website = "https://example.com";
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetVenuesWithoutRooms();

            // Assert
            Assert.IsType<JsonResult>(result);
        }

        [Fact]
        public async Task GetVenuesWithoutRooms_EmptyDb_ReturnsEmptyJson()
        {
            // Act
            var result = await _controller.GetVenuesWithoutRooms();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
        }

        #endregion

        #region Venues List Tests

        [Fact]
        public async Task Venues_ReturnsViewResultWithVenues()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 3);

            // Act
            var result = await _controller.Venues(null, 1);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<EscapeRoomVenue>>(viewResult.Model);
            Assert.Equal(3, model.Count);
        }

        [Fact]
        public async Task Venues_WithSearch_FiltersResults()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 3);

            // Act - search for "Seattle" which some venues have
            var result = await _controller.Venues("Seattle", 1);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<EscapeRoomVenue>>(viewResult.Model);
            Assert.All(model, v => Assert.Contains("Seattle", v.City));
        }

        #endregion
    }
}
