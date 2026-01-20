# Board Game Café Finder - Project Planning Document

## Executive Summary

### Project Overview
A modern, interactive web application for discovering board game cafés worldwide, addressing the $15B+ hobby board game market with real-time data, reviews, and booking capabilities.

### Market Opportunity
- **Target Market**: 15-20 million hobby gamers in US alone
- **Global Reach**: 2,000+ board game cafés worldwide (rapidly growing)
- **Competition Gap**: Current leader (boardgamecafenearme.com) is static HTML maintained by one person
- **Competitive Advantage**: Interactive app, real-time data, reviews, event booking, crowdsourced inventory

### Revenue Model
| Monthly Visitors | Estimated Revenue |
|-----------------|-------------------|
| 10,000 | $500/month |
| 50,000 | $3,000/month |
| 100,000+ | $10,000+/month |

**Revenue Streams**:
1. Café premium listing fees ($50-$200/month)
2. Event booking commissions (10-15%)
3. Board game affiliate links (Amazon, publishers) - 4-8% commission
4. B2B SaaS: Reservation/waitlist system for cafés

---

## Technology Stack

### Backend
- **Framework**: .NET Core 8 MVC
- **Language**: C# 12
- **ORM**: Entity Framework Core 8
- **Database**: SQL Server / PostgreSQL
- **API Architecture**: RESTful API + MVC Controllers
- **Authentication**: ASP.NET Core Identity
- **Caching**: Redis (for API responses and frequent queries)

### Frontend
- **View Engine**: Razor Views
- **CSS Framework**: Bootstrap 5 / Tailwind CSS
- **JavaScript**:
  - Vanilla JS for basic interactions
  - Google Maps JavaScript API for interactive maps
  - Alpine.js (lightweight) for reactive components
- **Map Integration**: Google Maps JavaScript API

### Third-Party Integrations
1. **Google Places API**
   - Café location data
   - Business hours
   - Photos
   - Basic ratings

2. **Yelp Fusion API**
   - Detailed reviews
   - Rating aggregation
   - Business information verification

3. **Payment Processing**
   - Stripe for premium listings and commissions

4. **Email Service**
   - SendGrid for transactional emails

### Infrastructure
- **Hosting**: Azure App Service / AWS Elastic Beanstalk
- **CDN**: Cloudflare for static assets
- **File Storage**: Azure Blob Storage / AWS S3 (for café photos)
- **CI/CD**: GitHub Actions

---

## Database Schema Design

### Core Tables

#### 1. Cafes
```sql
CREATE TABLE Cafes (
    CafeId INT PRIMARY KEY IDENTITY,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),

    -- Location Data
    Address NVARCHAR(500) NOT NULL,
    City NVARCHAR(100) NOT NULL,
    State NVARCHAR(100),
    Country NVARCHAR(100) NOT NULL,
    PostalCode NVARCHAR(20),
    Latitude DECIMAL(10, 8) NOT NULL,
    Longitude DECIMAL(11, 8) NOT NULL,

    -- Contact Information
    Phone NVARCHAR(20),
    Email NVARCHAR(100),
    Website NVARCHAR(500),

    -- Business Information
    OpeningHours NVARCHAR(MAX), -- JSON format
    PriceRange NVARCHAR(10), -- $, $$, $$$, $$$$

    -- External IDs
    GooglePlaceId NVARCHAR(200),
    YelpBusinessId NVARCHAR(200),

    -- Ratings & Stats
    AverageRating DECIMAL(3,2),
    TotalReviews INT DEFAULT 0,

    -- Status
    IsVerified BIT DEFAULT 0,
    IsPremium BIT DEFAULT 0,
    IsActive BIT DEFAULT 1,

    -- Metadata
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CreatedByUserId INT,

    -- SEO
    Slug NVARCHAR(300) UNIQUE,
    MetaDescription NVARCHAR(500)
)
```

#### 2. BoardGames
```sql
CREATE TABLE BoardGames (
    GameId INT PRIMARY KEY IDENTITY,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    Publisher NVARCHAR(200),
    MinPlayers INT,
    MaxPlayers INT,
    PlaytimeMinutes INT,
    AgeRating NVARCHAR(10),
    Complexity DECIMAL(3,2), -- 1.0 to 5.0
    BGGId INT, -- BoardGameGeek ID
    ImageUrl NVARCHAR(500),
    AmazonAffiliateUrl NVARCHAR(1000),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
)
```

#### 3. CafeGames (Many-to-Many)
```sql
CREATE TABLE CafeGames (
    CafeGameId INT PRIMARY KEY IDENTITY,
    CafeId INT NOT NULL FOREIGN KEY REFERENCES Cafes(CafeId),
    GameId INT NOT NULL FOREIGN KEY REFERENCES BoardGames(GameId),
    IsAvailable BIT DEFAULT 1,
    Quantity INT DEFAULT 1,
    RentalPrice DECIMAL(10,2),
    Notes NVARCHAR(500),
    LastVerified DATETIME2,
    VerifiedByUserId INT,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
)
```

#### 4. Reviews
```sql
CREATE TABLE Reviews (
    ReviewId INT PRIMARY KEY IDENTITY,
    CafeId INT NOT NULL FOREIGN KEY REFERENCES Cafes(CafeId),
    UserId INT NOT NULL FOREIGN KEY REFERENCES Users(UserId),
    Rating INT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
    Title NVARCHAR(200),
    Content NVARCHAR(MAX),
    VisitDate DATE,
    IsVerifiedVisit BIT DEFAULT 0,
    HelpfulCount INT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE()
)
```

#### 5. Events
```sql
CREATE TABLE Events (
    EventId INT PRIMARY KEY IDENTITY,
    CafeId INT NOT NULL FOREIGN KEY REFERENCES Cafes(CafeId),
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    EventType NVARCHAR(50), -- Tournament, Game Night, Workshop, etc.
    StartDateTime DATETIME2 NOT NULL,
    EndDateTime DATETIME2,
    MaxParticipants INT,
    CurrentParticipants INT DEFAULT 0,
    EntryFee DECIMAL(10,2),
    ImageUrl NVARCHAR(500),
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
)
```

#### 6. EventBookings
```sql
CREATE TABLE EventBookings (
    BookingId INT PRIMARY KEY IDENTITY,
    EventId INT NOT NULL FOREIGN KEY REFERENCES Events(EventId),
    UserId INT NOT NULL FOREIGN KEY REFERENCES Users(UserId),
    NumberOfSeats INT DEFAULT 1,
    Status NVARCHAR(20), -- Confirmed, Cancelled, Waitlist
    PaymentStatus NVARCHAR(20), -- Pending, Paid, Refunded
    TotalAmount DECIMAL(10,2),
    BookingDate DATETIME2 DEFAULT GETUTCDATE(),
    CancellationDate DATETIME2
)
```

#### 7. Users (ASP.NET Identity Extended)
```sql
CREATE TABLE Users (
    UserId INT PRIMARY KEY IDENTITY,
    -- ASP.NET Identity fields (AspNetUsers integration)
    Email NVARCHAR(256) NOT NULL UNIQUE,
    UserName NVARCHAR(256) NOT NULL,

    -- Profile Information
    FirstName NVARCHAR(100),
    LastName NVARCHAR(100),
    DisplayName NVARCHAR(100),
    Bio NVARCHAR(1000),
    AvatarUrl NVARCHAR(500),

    -- Location (optional for personalized recommendations)
    City NVARCHAR(100),
    Country NVARCHAR(100),

    -- Preferences
    FavoriteGameTypes NVARCHAR(MAX), -- JSON array

    -- Stats
    TotalReviews INT DEFAULT 0,
    TotalBookings INT DEFAULT 0,
    ReputationScore INT DEFAULT 0,

    -- Account Type
    IsCafeOwner BIT DEFAULT 0,
    CafeId INT FOREIGN KEY REFERENCES Cafes(CafeId),

    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    LastLoginAt DATETIME2
)
```

#### 8. PremiumListings
```sql
CREATE TABLE PremiumListings (
    ListingId INT PRIMARY KEY IDENTITY,
    CafeId INT NOT NULL FOREIGN KEY REFERENCES Cafes(CafeId),
    PlanType NVARCHAR(50), -- Basic ($50), Premium ($100), Featured ($200)
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    IsActive BIT DEFAULT 1,
    MonthlyFee DECIMAL(10,2) NOT NULL,

    -- Features
    FeaturedPlacement BIT DEFAULT 0,
    PhotoGallery BIT DEFAULT 0,
    EventListings BIT DEFAULT 0,
    GameInventoryManager BIT DEFAULT 0,
    AnalyticsDashboard BIT DEFAULT 0,

    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
)
```

#### 9. Photos
```sql
CREATE TABLE Photos (
    PhotoId INT PRIMARY KEY IDENTITY,
    CafeId INT NOT NULL FOREIGN KEY REFERENCES Cafes(CafeId),
    UploadedByUserId INT FOREIGN KEY REFERENCES Users(UserId),
    Url NVARCHAR(500) NOT NULL,
    ThumbnailUrl NVARCHAR(500),
    Caption NVARCHAR(500),
    DisplayOrder INT DEFAULT 0,
    IsApproved BIT DEFAULT 0,
    UploadedAt DATETIME2 DEFAULT GETUTCDATE()
)
```

---

## Architecture & Design Patterns

### MVC Structure
```
BoardGameCafesFinder/
├── Controllers/
│   ├── HomeController.cs
│   ├── CafesController.cs (Main CRUD + Search)
│   ├── MapController.cs (Map-based search API)
│   ├── ReviewsController.cs
│   ├── EventsController.cs
│   ├── AdminController.cs
│   └── ApiController.cs (RESTful API endpoints)
├── Models/
│   ├── Domain/ (Entity models)
│   ├── ViewModels/
│   └── DTOs/ (Data Transfer Objects for API)
├── Views/
│   ├── Home/
│   ├── Cafes/
│   ├── Map/
│   ├── Events/
│   └── Shared/
├── Services/
│   ├── CafeService.cs
│   ├── GooglePlacesService.cs
│   ├── YelpService.cs
│   ├── GeocodingService.cs
│   ├── ReviewService.cs
│   └── EmailService.cs
├── Repositories/
│   ├── ICafeRepository.cs
│   ├── CafeRepository.cs
│   └── ... (Repository pattern)
├── Data/
│   └── ApplicationDbContext.cs
├── wwwroot/
│   ├── css/
│   ├── js/
│   │   ├── map.js (Google Maps integration)
│   │   └── search.js
│   └── images/
└── appsettings.json
```

### Design Patterns
1. **Repository Pattern**: Data access abstraction
2. **Unit of Work**: Transaction management
3. **Dependency Injection**: Built-in .NET Core DI
4. **Service Layer**: Business logic separation
5. **DTO Pattern**: API data transfer
6. **Factory Pattern**: External API client creation

---

## Map-Based Search Feature - Detailed Implementation

### Frontend Components

#### 1. Interactive Map View (Razor + JavaScript)
**View: Views/Map/Index.cshtml**
```html
@model MapViewModel

<div class="container-fluid p-0">
    <div class="row g-0">
        <!-- Sidebar with Search & Filters -->
        <div class="col-md-4 sidebar-panel">
            <div class="search-container p-3">
                <h4>Find Board Game Cafés</h4>

                <!-- Location Search -->
                <div class="mb-3">
                    <label>Location</label>
                    <input type="text" id="locationInput"
                           class="form-control"
                           placeholder="Enter city or address">
                </div>

                <!-- Radius Filter -->
                <div class="mb-3">
                    <label>Search Radius</label>
                    <select id="radiusSelect" class="form-select">
                        <option value="5000">5 km</option>
                        <option value="10000" selected>10 km</option>
                        <option value="25000">25 km</option>
                        <option value="50000">50 km</option>
                    </select>
                </div>

                <!-- Filters -->
                <div class="mb-3">
                    <label>Open Now</label>
                    <input type="checkbox" id="openNowFilter">
                </div>

                <div class="mb-3">
                    <label>Has Game Library</label>
                    <input type="checkbox" id="hasGamesFilter">
                </div>

                <button id="searchBtn" class="btn btn-primary w-100">
                    Search
                </button>
            </div>

            <!-- Results List -->
            <div id="resultsContainer" class="results-list">
                <!-- Dynamically populated -->
            </div>
        </div>

        <!-- Google Map -->
        <div class="col-md-8">
            <div id="map" style="height: 100vh;"></div>
        </div>
    </div>
</div>

@section Scripts {
    <script src="https://maps.googleapis.com/maps/api/js?key=@Model.GoogleMapsApiKey&libraries=places"></script>
    <script src="~/js/map.js"></script>
}
```

#### 2. Map JavaScript Implementation (wwwroot/js/map.js)
```javascript
// Map instance
let map;
let markers = [];
let infoWindow;
let currentLocation = null;

// Initialize map
function initMap() {
    // Default to center of US or user's location
    const defaultCenter = { lat: 39.8283, lng: -98.5795 };

    map = new google.maps.Map(document.getElementById('map'), {
        zoom: 12,
        center: defaultCenter,
        mapTypeControl: false,
        streetViewControl: false,
        styles: getMapStyles() // Custom styling
    });

    infoWindow = new google.maps.InfoWindow();

    // Try to get user's location
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(
            (position) => {
                currentLocation = {
                    lat: position.coords.latitude,
                    lng: position.coords.longitude
                };
                map.setCenter(currentLocation);
                searchNearby();
            },
            () => {
                // Location access denied, use default
                console.log("Location access denied");
            }
        );
    }

    // Initialize autocomplete
    initAutocomplete();
}

// Search cafes near location
async function searchNearby() {
    const center = map.getCenter();
    const radius = document.getElementById('radiusSelect').value;
    const openNow = document.getElementById('openNowFilter').checked;
    const hasGames = document.getElementById('hasGamesFilter').checked;

    try {
        const response = await fetch('/api/cafes/search', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                latitude: center.lat(),
                longitude: center.lng(),
                radius: radius,
                openNow: openNow,
                hasGames: hasGames
            })
        });

        const cafes = await response.json();
        displayResults(cafes);
    } catch (error) {
        console.error('Search error:', error);
    }
}

// Display markers on map
function displayResults(cafes) {
    // Clear existing markers
    clearMarkers();

    // Create new markers
    cafes.forEach(cafe => {
        const marker = new google.maps.Marker({
            position: { lat: cafe.latitude, lng: cafe.longitude },
            map: map,
            title: cafe.name,
            icon: getCustomMarkerIcon(cafe)
        });

        // Add click listener
        marker.addListener('click', () => {
            showCafeInfo(cafe);
            highlightListItem(cafe.id);
        });

        markers.push(marker);
    });

    // Update results list
    updateResultsList(cafes);

    // Adjust map bounds
    if (cafes.length > 0) {
        fitMapBounds();
    }
}

// Show cafe information window
function showCafeInfo(cafe) {
    const content = `
        <div class="info-window">
            <h5>${cafe.name}</h5>
            <p class="text-muted">${cafe.address}</p>
            ${cafe.isOpenNow ? '<span class="badge bg-success">Open Now</span>' : '<span class="badge bg-danger">Closed</span>'}
            <div class="mt-2">
                <strong>Rating:</strong> ${cafe.averageRating || 'N/A'} ⭐
            </div>
            <div class="mt-2">
                ${cafe.totalGames ? `<strong>${cafe.totalGames}</strong> games available` : ''}
            </div>
            <div class="mt-3">
                <a href="/cafes/${cafe.slug}" class="btn btn-sm btn-primary">View Details</a>
                <a href="https://www.google.com/maps/dir/?api=1&destination=${cafe.latitude},${cafe.longitude}"
                   target="_blank" class="btn btn-sm btn-outline-primary">Directions</a>
            </div>
        </div>
    `;

    infoWindow.setContent(content);

    // Find marker
    const marker = markers.find(m => m.getTitle() === cafe.name);
    if (marker) {
        infoWindow.open(map, marker);
    }
}

// Custom marker icon
function getCustomMarkerIcon(cafe) {
    // Different colors for premium, verified, or regular cafes
    const color = cafe.isPremium ? '#FFD700' :
                  cafe.isVerified ? '#4CAF50' : '#FF5722';

    return {
        url: `data:image/svg+xml,${encodeURIComponent(getMarkerSVG(color))}`,
        scaledSize: new google.maps.Size(40, 40)
    };
}

function getMarkerSVG(color) {
    return `
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="${color}">
            <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/>
        </svg>
    `;
}

// Update sidebar results list
function updateResultsList(cafes) {
    const container = document.getElementById('resultsContainer');

    if (cafes.length === 0) {
        container.innerHTML = '<p class="p-3 text-muted">No cafés found in this area</p>';
        return;
    }

    container.innerHTML = cafes.map(cafe => `
        <div class="result-item p-3 border-bottom" data-cafe-id="${cafe.id}">
            <h6>${cafe.name}</h6>
            <p class="text-muted small mb-1">${cafe.address}</p>
            <div class="d-flex justify-content-between align-items-center">
                <span>${cafe.averageRating || 'N/A'} ⭐ (${cafe.totalReviews || 0} reviews)</span>
                ${cafe.isOpenNow ? '<span class="badge bg-success">Open</span>' : '<span class="badge bg-secondary">Closed</span>'}
            </div>
            ${cafe.totalGames ? `<small class="text-success">${cafe.totalGames} games</small>` : ''}
        </div>
    `).join('');

    // Add click listeners to result items
    document.querySelectorAll('.result-item').forEach(item => {
        item.addEventListener('click', function() {
            const cafeId = parseInt(this.dataset.cafeId);
            const cafe = cafes.find(c => c.id === cafeId);
            if (cafe) {
                map.setCenter({ lat: cafe.latitude, lng: cafe.longitude });
                map.setZoom(15);
                showCafeInfo(cafe);
            }
        });
    });
}

// Initialize autocomplete for location search
function initAutocomplete() {
    const input = document.getElementById('locationInput');
    const autocomplete = new google.maps.places.Autocomplete(input);

    autocomplete.addListener('place_changed', () => {
        const place = autocomplete.getPlace();
        if (place.geometry) {
            map.setCenter(place.geometry.location);
            map.setZoom(13);
            searchNearby();
        }
    });
}

// Event listeners
document.getElementById('searchBtn').addEventListener('click', searchNearby);
document.getElementById('radiusSelect').addEventListener('change', searchNearby);
document.getElementById('openNowFilter').addEventListener('change', searchNearby);
document.getElementById('hasGamesFilter').addEventListener('change', searchNearby);

// Initialize on page load
window.addEventListener('load', initMap);
```

### Backend Implementation

#### 1. Map API Controller (Controllers/ApiController.cs)
```csharp
using Microsoft.AspNetCore.Mvc;
using BoardGameCafeFinder.Services;
using BoardGameCafeFinder.Models.DTOs;

namespace BoardGameCafeFinder.Controllers
{
    [ApiController]
    [Route("api/cafes")]
    public class ApiController : ControllerBase
    {
        private readonly ICafeService _cafeService;
        private readonly IGeocodingService _geocodingService;

        public ApiController(ICafeService cafeService, IGeocodingService geocodingService)
        {
            _cafeService = cafeService;
            _geocodingService = geocodingService;
        }

        [HttpPost("search")]
        public async Task<IActionResult> SearchCafes([FromBody] CafeSearchRequest request)
        {
            try
            {
                // Validate request
                if (request.Latitude < -90 || request.Latitude > 90 ||
                    request.Longitude < -180 || request.Longitude > 180)
                {
                    return BadRequest("Invalid coordinates");
                }

                // Search cafes within radius
                var cafes = await _cafeService.SearchNearbyAsync(
                    request.Latitude,
                    request.Longitude,
                    request.Radius,
                    request.OpenNow,
                    request.HasGames
                );

                // Transform to DTOs
                var results = cafes.Select(c => new CafeSearchResultDto
                {
                    Id = c.CafeId,
                    Name = c.Name,
                    Address = c.Address,
                    Latitude = c.Latitude,
                    Longitude = c.Longitude,
                    AverageRating = c.AverageRating,
                    TotalReviews = c.TotalReviews,
                    IsOpenNow = c.IsOpenNow(),
                    IsPremium = c.IsPremium,
                    IsVerified = c.IsVerified,
                    TotalGames = c.CafeGames?.Count ?? 0,
                    Slug = c.Slug
                }).ToList();

                return Ok(results);
            }
            catch (Exception ex)
            {
                // Log error
                return StatusCode(500, "An error occurred while searching");
            }
        }

        [HttpGet("{id}/details")]
        public async Task<IActionResult> GetCafeDetails(int id)
        {
            var cafe = await _cafeService.GetByIdWithDetailsAsync(id);
            if (cafe == null)
            {
                return NotFound();
            }

            var result = new CafeDetailsDto
            {
                Id = cafe.CafeId,
                Name = cafe.Name,
                Description = cafe.Description,
                Address = cafe.Address,
                Phone = cafe.Phone,
                Website = cafe.Website,
                OpeningHours = cafe.GetOpeningHours(),
                Photos = cafe.Photos.Select(p => p.Url).ToList(),
                AverageRating = cafe.AverageRating,
                Reviews = cafe.Reviews.Select(r => new ReviewDto
                {
                    UserName = r.User.DisplayName,
                    Rating = r.Rating,
                    Content = r.Content,
                    CreatedAt = r.CreatedAt
                }).ToList()
            };

            return Ok(result);
        }
    }
}
```

#### 2. Cafe Service (Services/CafeService.cs)
```csharp
using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Services
{
    public interface ICafeService
    {
        Task<List<Cafe>> SearchNearbyAsync(double latitude, double longitude,
                                          int radiusMeters, bool openNow, bool hasGames);
        Task<Cafe> GetByIdWithDetailsAsync(int id);
        Task<Cafe> CreateAsync(Cafe cafe);
        Task UpdateAsync(Cafe cafe);
    }

    public class CafeService : ICafeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CafeService> _logger;

        public CafeService(ApplicationDbContext context, ILogger<CafeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Cafe>> SearchNearbyAsync(
            double latitude,
            double longitude,
            int radiusMeters,
            bool openNow,
            bool hasGames)
        {
            // Calculate bounding box for initial filter
            var radiusKm = radiusMeters / 1000.0;
            var latDelta = radiusKm / 111.0; // 1 degree latitude ≈ 111 km
            var lonDelta = radiusKm / (111.0 * Math.Cos(latitude * Math.PI / 180.0));

            var query = _context.Cafes
                .Where(c => c.IsActive)
                .Where(c => c.Latitude >= latitude - latDelta && c.Latitude <= latitude + latDelta)
                .Where(c => c.Longitude >= longitude - lonDelta && c.Longitude <= longitude + lonDelta)
                .Include(c => c.CafeGames)
                .AsQueryable();

            if (hasGames)
            {
                query = query.Where(c => c.CafeGames.Any());
            }

            var cafes = await query.ToListAsync();

            // Calculate exact distance and filter
            var results = cafes
                .Select(c => new
                {
                    Cafe = c,
                    Distance = CalculateDistance(latitude, longitude, c.Latitude, c.Longitude)
                })
                .Where(x => x.Distance <= radiusMeters)
                .OrderBy(x => x.Distance)
                .Select(x => x.Cafe)
                .ToList();

            // Filter by open now if requested
            if (openNow)
            {
                results = results.Where(c => c.IsOpenNow()).ToList();
            }

            return results;
        }

        public async Task<Cafe> GetByIdWithDetailsAsync(int id)
        {
            return await _context.Cafes
                .Include(c => c.Reviews)
                    .ThenInclude(r => r.User)
                .Include(c => c.Photos)
                .Include(c => c.CafeGames)
                    .ThenInclude(cg => cg.Game)
                .Include(c => c.Events)
                .FirstOrDefaultAsync(c => c.CafeId == id);
        }

        public async Task<Cafe> CreateAsync(Cafe cafe)
        {
            _context.Cafes.Add(cafe);
            await _context.SaveChangesAsync();
            return cafe;
        }

        public async Task UpdateAsync(Cafe cafe)
        {
            cafe.UpdatedAt = DateTime.UtcNow;
            _context.Cafes.Update(cafe);
            await _context.SaveChangesAsync();
        }

        // Haversine formula for distance calculation
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Earth's radius in meters

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
```

#### 3. Models/DTOs
```csharp
namespace BoardGameCafeFinder.Models.DTOs
{
    public class CafeSearchRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Radius { get; set; } = 10000; // Default 10km
        public bool OpenNow { get; set; }
        public bool HasGames { get; set; }
    }

    public class CafeSearchResultDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public bool IsOpenNow { get; set; }
        public bool IsPremium { get; set; }
        public bool IsVerified { get; set; }
        public int TotalGames { get; set; }
        public string Slug { get; set; }
    }
}
```

---

## API Integration Strategy

### Google Places API Integration

#### Purpose
- Initial café discovery and data seeding
- Verify business information
- Get photos and basic ratings
- Autocomplete for location search

#### Implementation (Services/GooglePlacesService.cs)
```csharp
public class GooglePlacesService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public async Task<List<PlaceResult>> SearchBoardGameCafesAsync(double lat, double lng, int radius)
    {
        // Use Places API Nearby Search
        var url = $"https://maps.googleapis.com/maps/api/place/nearbysearch/json" +
                  $"?location={lat},{lng}" +
                  $"&radius={radius}" +
                  $"&keyword=board+game+cafe" +
                  $"&key={_apiKey}";

        var response = await _httpClient.GetAsync(url);
        // Parse and return results
    }

    public async Task<PlaceDetails> GetPlaceDetailsAsync(string placeId)
    {
        // Get detailed information including hours, photos, reviews
    }
}
```

#### API Costs
- **Nearby Search**: $32 per 1000 requests
- **Place Details**: $17 per 1000 requests
- **Autocomplete**: $2.83 per 1000 requests

**Cost Management**:
- Cache results for 24 hours (Redis)
- Only fetch details when café is viewed
- Batch update café data weekly

### Yelp Fusion API Integration

#### Purpose
- Rich review data
- Business verification
- Updated hours and photos
- Rating aggregation

#### Implementation (Services/YelpService.cs)
```csharp
public class YelpService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public async Task<YelpBusiness> GetBusinessAsync(string businessId)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);

        var url = $"https://api.yelp.com/v3/businesses/{businessId}";
        var response = await _httpClient.GetAsync(url);
        // Parse response
    }

    public async Task<List<YelpReview>> GetReviewsAsync(string businessId)
    {
        var url = $"https://api.yelp.com/v3/businesses/{businessId}/reviews";
        // Fetch up to 3 reviews (API limit)
    }
}
```

#### API Limits
- **Free Tier**: 500 calls/day
- **Rate Limit**: 5 QPS (queries per second)

**Cost Management**:
- Cache for 24 hours
- Only fetch when user views café details
- Aggregate data in our database

---

## Data Seeding Strategy

### Phase 1: Automated Discovery
1. **Google Places Search** by major cities:
   - Query "board game cafe" in top 50 US cities
   - Extract basic information (name, location, phone)
   - Store with GooglePlaceId

2. **Yelp Matching**:
   - Search Yelp for same business by name + location
   - Link YelpBusinessId for review aggregation

### Phase 2: Community Crowdsourcing
1. **"Add Missing Café" Form**:
   - Users can submit new cafés
   - Verification workflow for admins
   - Reward system (badges, reputation)

2. **Game Inventory Crowdsourcing**:
   - Café owners can claim their listing
   - Community members can suggest games
   - Verification by café staff

3. **BoardGameGeek Integration**:
   - Partner with BGG for game data
   - Use BGG IDs for game matching
   - Import game metadata (players, complexity, etc.)

---

## MVP Feature Roadmap

### Phase 1: Core Discovery (Months 1-2)
- [x] Database setup and migrations
- [ ] Google Maps integration
- [ ] Basic café listing pages
- [ ] Search by location with radius
- [ ] Café detail pages
- [ ] Admin panel for café management

### Phase 2: User Engagement (Months 3-4)
- [ ] User registration and authentication
- [ ] Review and rating system
- [ ] Photo uploads
- [ ] Favorite cafés feature
- [ ] Email notifications

### Phase 3: Events & Booking (Months 5-6)
- [ ] Event creation and management
- [ ] Event booking system
- [ ] Payment integration (Stripe)
- [ ] Email reminders for events
- [ ] Calendar view

### Phase 4: Monetization (Months 7-8)
- [ ] Premium listing tiers
- [ ] Subscription management
- [ ] Analytics dashboard for café owners
- [ ] Affiliate link tracking
- [ ] Commission system for bookings

### Phase 5: Advanced Features (Months 9-12)
- [ ] Mobile app (React Native / Flutter)
- [ ] Real-time availability tracking
- [ ] Reservation system
- [ ] Game inventory management
- [ ] Social features (follow users, game clubs)
- [ ] Recommendation engine

---

## SEO & Marketing Strategy

### Technical SEO
1. **URL Structure**:
   - `/cafes/[city]/[cafe-name]`
   - `/events/[city]`
   - `/games/[game-name]`

2. **Meta Tags**:
   - Dynamic title: "{Cafe Name} - Board Game Café in {City}"
   - Descriptions from café data
   - Open Graph tags for social sharing

3. **Schema Markup**:
   - LocalBusiness schema
   - Review schema
   - Event schema

### Content Marketing
1. **City Guides**:
   - "Best Board Game Cafés in [City]"
   - Auto-generated from database

2. **Blog**:
   - Game reviews
   - Café spotlights
   - Event highlights

3. **User-Generated Content**:
   - Reviews
   - Photos
   - Event reports

### Acquisition Channels
1. **Reddit**:
   - r/boardgames (1M+ subscribers)
   - City-specific subreddits

2. **BoardGameGeek Forums**:
   - Announce new features
   - Seek feedback

3. **Social Media**:
   - Instagram (visual content)
   - Twitter (events, updates)
   - Facebook groups

4. **Partnerships**:
   - Game publishers
   - Board game cafés
   - Local game stores

---

## Performance & Scalability

### Caching Strategy
1. **Redis Caching**:
   - API responses (24h TTL)
   - Search results (1h TTL)
   - Popular café details (6h TTL)

2. **Output Caching**:
   - Static pages (city guides, etc.)
   - Café detail pages (vary by user)

### Database Optimization
1. **Indexes**:
   - Geospatial index on (Latitude, Longitude)
   - Index on City, State, Country
   - Full-text index on Name, Description

2. **Query Optimization**:
   - Use compiled queries
   - Avoid N+1 queries with eager loading
   - Pagination for lists

### CDN Strategy
- Cloudflare for static assets
- Image optimization and lazy loading
- Compression (Brotli/Gzip)

---

## Security Considerations

### Authentication & Authorization
1. **ASP.NET Core Identity**:
   - Email/password registration
   - OAuth providers (Google, Facebook)
   - Two-factor authentication

2. **Role-Based Access**:
   - Admin: Full access
   - Café Owner: Manage own café
   - User: Reviews, bookings

### Data Protection
1. **HTTPS Enforcement**
2. **SQL Injection Prevention**: EF Core parameterized queries
3. **XSS Protection**: Razor automatic encoding
4. **CSRF Tokens**: Built-in anti-forgery tokens
5. **Rate Limiting**: API throttling

### Payment Security
- Stripe Checkout (PCI compliant)
- Never store credit card data
- Webhook signature verification

---

## Analytics & Tracking

### User Analytics
- **Google Analytics 4**:
  - Page views
  - Search behavior
  - Conversion tracking

- **Custom Events**:
  - Café views
  - Review submissions
  - Booking completions
  - Affiliate link clicks

### Business Metrics
1. **KPIs**:
   - Monthly Active Users (MAU)
   - Conversion rate (visitor → booking)
   - Average order value
   - Café owner acquisition

2. **Dashboard for Café Owners**:
   - Profile views
   - Click-through rate
   - Booking statistics
   - Review summary

---

## Legal & Compliance

### Terms of Service
- User-generated content policy
- Café owner responsibilities
- Booking terms and cancellation

### Privacy Policy
- GDPR compliance (EU visitors)
- CCPA compliance (California)
- Cookie consent
- Data retention policy

### Content Moderation
- Review approval system
- Report abuse functionality
- Automated spam detection

---

## Budget & Cost Estimates (Monthly)

### Development Phase
| Item | Cost |
|------|------|
| Developer (your time) | - |
| Azure/AWS hosting | $50-100 |
| Database | $20-50 |
| Google Maps API | $50-200 |
| Yelp API | Free (500/day) |
| Domain & SSL | $15 |
| Email service (SendGrid) | $15-50 |
| **Total** | **$150-415/mo** |

### At Scale (100K visitors/mo)
| Item | Cost |
|------|------|
| Hosting | $200-500 |
| Database | $100-200 |
| CDN (Cloudflare Pro) | $20 |
| Google Maps API | $500-1000 |
| Redis Cache | $50 |
| Email service | $100-200 |
| **Total** | **$970-2070/mo** |

**Note**: Should be covered by revenue at this scale ($10K/mo estimated)

---

## Next Steps

### Immediate Actions (Week 1)
1. ✅ Review and approve this planning document
2. [ ] Set up development environment
3. [ ] Create Azure/AWS account
4. [ ] Register domain name
5. [ ] Get API keys (Google Places, Yelp)

### Week 2-4: Project Setup
1. [ ] Initialize .NET Core 8 MVC project
2. [ ] Set up database and migrations
3. [ ] Configure authentication
4. [ ] Implement basic map view
5. [ ] Create API endpoints for search

### Month 2: Core Features
1. [ ] Café CRUD operations
2. [ ] Seed initial data from Google Places
3. [ ] Implement search filters
4. [ ] Create café detail pages
5. [ ] Deploy MVP to production

### Month 3: Growth
1. [ ] Launch beta to r/boardgames
2. [ ] Gather user feedback
3. [ ] Implement review system
4. [ ] Add 100+ cafés manually
5. [ ] Start SEO optimization

---

## Questions & Decisions Needed

1. **Domain Name**: Have you chosen a domain?
   - Suggestions: boardgamecafes.com, cafeboardgames.com, playcafefinder.com

2. **Hosting Preference**: Azure vs AWS?
   - Azure: Better .NET integration
   - AWS: More cost-effective at scale

3. **Design**: Custom design or use template?
   - Bootstrap admin template
   - Tailwind UI components
   - Hire designer ($500-2000)

4. **Initial Data**: Start with US only or worldwide?
   - US first (easier to moderate)
   - Add international after traction

---

## Risk Assessment & Mitigation

### Risks
1. **API Costs**: Google Maps can be expensive
   - **Mitigation**: Aggressive caching, consider Mapbox alternative

2. **Data Accuracy**: Cafés close/relocate
   - **Mitigation**: Crowdsourced updates, quarterly verification

3. **Competition**: Existing players improve
   - **Mitigation**: Focus on community, niche features (game inventory)

4. **Chicken-Egg Problem**: Need cafés and users
   - **Mitigation**: Seed 200+ cafés before launch, partner with cafés

5. **Monetization**: Cafés may not pay
   - **Mitigation**: Freemium model, prove value with analytics

---

## Success Metrics

### 6-Month Goals
- 500+ cafés listed
- 10,000 monthly visitors
- 100+ reviews
- 10 premium café listings
- $500-1000 monthly revenue

### 12-Month Goals
- 1,500+ cafés worldwide
- 50,000 monthly visitors
- 1,000+ reviews
- 50 premium listings
- $5,000+ monthly revenue
- Mobile app launched

---

## Conclusion

This Board Game Café Finder project addresses a clear market gap with solid monetization potential. The .NET Core MVC architecture provides a robust foundation, and the map-based search feature will be the key differentiator from the existing static competitor.

**Key Success Factors**:
1. ✅ Large, growing market ($15B+)
2. ✅ Weak competition (static website)
3. ✅ Clear value proposition (interactive, real-time, comprehensive)
4. ✅ Multiple revenue streams
5. ✅ Community-driven growth potential

**Recommended Immediate Focus**:
- Build the map-based search MVP quickly (8-12 weeks)
- Seed 200+ US cafés before public launch
- Launch to r/boardgames for initial traction
- Iterate based on user feedback

**Total Estimated Time to MVP**: 3-4 months part-time, 2 months full-time

Ready to start building? 🎲☕
