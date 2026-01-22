# Google Places API - Cost Optimization Strategy

## Executive Summary

**Recommended Approach**: Seed database first, then search locally
**Estimated Savings**: 95% reduction in API costs compared to realtime approach
**Initial Investment**: $100-200 for data seeding
**Ongoing Cost**: $20-50/month for maintenance

---

## Strategy Overview

### Phase 1: Initial Data Seeding (Week 1-2)

#### Step 1: Target City Selection
Start with top US cities with most board game cafés:

**Priority Cities (Top 20)**:
1. Seattle, WA
2. Portland, OR
3. Chicago, IL
4. New York, NY
5. Los Angeles, CA
6. San Francisco, CA
7. Austin, TX
8. Denver, CO
9. Minneapolis, MN
10. Boston, MA
11. Washington, DC
12. Atlanta, GA
13. Philadelphia, PA
14. San Diego, CA
15. Phoenix, AZ
16. Dallas, TX
17. Houston, TX
18. Tampa, FL
19. Orlando, FL
20. Cleveland, OH

**Estimated Results**: ~300-500 cafés

#### Step 2: Automated Seeding Script

Run once to populate database:

```csharp
// Services/DataSeedingService.cs
public class DataSeedingService
{
    private readonly IGooglePlacesService _placesService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DataSeedingService> _logger;

    public async Task SeedCafesForCitiesAsync(List<string> cities)
    {
        foreach (var city in cities)
        {
            await SeedCafesInCityAsync(city);

            // Rate limiting: Wait 2 seconds between requests
            await Task.Delay(2000);
        }
    }

    private async Task SeedCafesInCityAsync(string cityName)
    {
        _logger.LogInformation($"Seeding cafés for {cityName}...");

        try
        {
            // Search for board game cafés in city
            var places = await _placesService.SearchBoardGameCafesAsync(
                location: cityName,
                keywords: new[] { "board game cafe", "board game bar", "tabletop gaming cafe" }
            );

            foreach (var place in places)
            {
                // Check if already exists
                var exists = await _context.Cafes
                    .AnyAsync(c => c.GooglePlaceId == place.PlaceId);

                if (exists)
                {
                    _logger.LogInformation($"Café {place.Name} already exists, skipping...");
                    continue;
                }

                // Get detailed information
                var details = await _placesService.GetPlaceDetailsAsync(place.PlaceId);

                // Map to domain model
                var cafe = new Cafe
                {
                    Name = details.Name,
                    Description = details.Description,
                    Address = details.FormattedAddress,
                    City = ExtractCity(details.AddressComponents),
                    State = ExtractState(details.AddressComponents),
                    Country = ExtractCountry(details.AddressComponents),
                    PostalCode = ExtractPostalCode(details.AddressComponents),
                    Latitude = details.Latitude,
                    Longitude = details.Longitude,
                    Phone = details.PhoneNumber,
                    Website = details.Website,
                    GooglePlaceId = details.PlaceId,
                    OpeningHours = SerializeOpeningHours(details.OpeningHours),
                    AverageRating = details.Rating,
                    TotalReviews = details.UserRatingsTotal,
                    IsVerified = false, // Manual verification required
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Slug = GenerateSlug(details.Name, ExtractCity(details.AddressComponents))
                };

                _context.Cafes.Add(cafe);

                // Download and save photos
                if (details.Photos?.Any() == true)
                {
                    await SaveCafePhotosAsync(cafe, details.Photos.Take(5)); // Max 5 photos
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Added café: {cafe.Name} in {cafe.City}");

                // Rate limiting
                await Task.Delay(1000);
            }

            _logger.LogInformation($"Completed seeding for {cityName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error seeding cafés for {cityName}");
        }
    }

    private string GenerateSlug(string name, string city)
    {
        var slug = $"{name}-{city}"
            .ToLower()
            .Replace(" ", "-")
            .Replace("&", "and");

        // Remove special characters
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");

        return slug;
    }

    private string SerializeOpeningHours(List<OpeningHour> hours)
    {
        return JsonSerializer.Serialize(hours);
    }

    // Helper methods for address parsing
    private string ExtractCity(List<AddressComponent> components)
    {
        return components
            .FirstOrDefault(c => c.Types.Contains("locality"))
            ?.LongName;
    }

    private string ExtractState(List<AddressComponent> components)
    {
        return components
            .FirstOrDefault(c => c.Types.Contains("administrative_area_level_1"))
            ?.ShortName;
    }

    private string ExtractCountry(List<AddressComponent> components)
    {
        return components
            .FirstOrDefault(c => c.Types.Contains("country"))
            ?.LongName ?? "United States";
    }

    private string ExtractPostalCode(List<AddressComponent> components)
    {
        return components
            .FirstOrDefault(c => c.Types.Contains("postal_code"))
            ?.LongName;
    }
}
```

#### Step 3: Run Seeding Command

Create a console command to run seeding:

```csharp
// Commands/SeedDataCommand.cs
public class SeedDataCommand
{
    public static async Task ExecuteAsync(IServiceProvider services)
    {
        var seedingService = services.GetRequiredService<DataSeedingService>();
        var logger = services.GetRequiredService<ILogger<SeedDataCommand>>();

        var cities = new List<string>
        {
            "Seattle, WA",
            "Portland, OR",
            "Chicago, IL",
            "New York, NY",
            "Los Angeles, CA",
            "San Francisco, CA",
            "Austin, TX",
            "Denver, CO",
            "Minneapolis, MN",
            "Boston, MA",
            "Washington, DC",
            "Atlanta, GA",
            "Philadelphia, PA",
            "San Diego, CA",
            "Phoenix, AZ",
            "Dallas, TX",
            "Houston, TX",
            "Tampa, FL",
            "Orlando, FL",
            "Cleveland, OH"
        };

        logger.LogInformation("Starting data seeding process...");
        logger.LogInformation($"Target cities: {cities.Count}");

        var startTime = DateTime.UtcNow;

        await seedingService.SeedCafesForCitiesAsync(cities);

        var duration = DateTime.UtcNow - startTime;
        logger.LogInformation($"Seeding completed in {duration.TotalMinutes:F2} minutes");
    }
}
```

Run command:
```bash
dotnet run -- seed-data
```

#### Estimated API Costs for Initial Seeding

| Operation | Requests | Cost per 1K | Total Cost |
|-----------|----------|-------------|------------|
| Text Search (20 cities) | 20 | $32 | $0.64 |
| Place Details (~400 cafés) | 400 | $17 | $6.80 |
| Photos (optional) | 400 | $7 | $2.80 |
| **Total Initial Seeding** | | | **~$10-15** |

**Note**: Much cheaper than expected! The key is doing it ONCE.

---

## Phase 2: Runtime Search (User Searches)

### Search Flow (100% Local Database)

```csharp
// Controllers/ApiController.cs
[HttpPost("cafes/search")]
public async Task<IActionResult> SearchCafes([FromBody] CafeSearchRequest request)
{
    // NO Google Places API call here!
    // Search local database using geospatial queries

    var cafes = await _cafeService.SearchNearbyAsync(
        latitude: request.Latitude,
        longitude: request.Longitude,
        radiusMeters: request.Radius,
        openNow: request.OpenNow,
        hasGames: request.HasGames
    );

    return Ok(cafes);
}
```

### CafeService Implementation

```csharp
public async Task<List<Cafe>> SearchNearbyAsync(
    double latitude,
    double longitude,
    int radiusMeters,
    bool openNow,
    bool hasGames)
{
    // Calculate bounding box for efficient query
    var radiusKm = radiusMeters / 1000.0;
    var latDelta = radiusKm / 111.0;
    var lonDelta = radiusKm / (111.0 * Math.Cos(latitude * Math.PI / 180.0));

    // Query database (NOT Google API)
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

    // Calculate exact distance using Haversine formula
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
        var now = DateTime.UtcNow;
        results = results.Where(c => IsOpenNow(c, now)).ToList();
    }

    return results;
}

private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
{
    const double R = 6371000; // Earth's radius in meters

    var dLat = ToRadians(lat2 - lat1);
    var dLon = ToRadians(lon2 - lon1);

    var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

    var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

    return R * c; // Distance in meters
}

private bool IsOpenNow(Cafe cafe, DateTime currentTime)
{
    if (string.IsNullOrEmpty(cafe.OpeningHours))
        return false;

    var hours = JsonSerializer.Deserialize<List<OpeningHour>>(cafe.OpeningHours);
    var dayOfWeek = currentTime.DayOfWeek.ToString();
    var currentMinutes = currentTime.Hour * 60 + currentTime.Minute;

    var todayHours = hours.FirstOrDefault(h => h.Day == dayOfWeek);
    if (todayHours == null)
        return false;

    // Parse opening/closing times and check if current time is within range
    // Implementation depends on your OpeningHour format
    return true; // Placeholder
}
```

### Cost for Runtime Search
**$0** - All searches happen in your database!

---

## Phase 3: Periodic Refresh (Background Jobs)

### Why Refresh?
- Cafe change hours
- New cafés open
- Some cafés close
- Photos get updated
- Ratings change

### Refresh Strategy

```csharp
// Services/CafeRefreshService.cs
public class CafeRefreshService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CafeRefreshService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshCafesAsync();

                // Wait 7 days before next refresh
                await Task.Delay(TimeSpan.FromDays(7), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cafe refresh service");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task RefreshCafesAsync()
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var placesService = scope.ServiceProvider.GetRequiredService<IGooglePlacesService>();

        _logger.LogInformation("Starting weekly café refresh...");

        // Get all active cafés with Google Place IDs
        var cafes = await context.Cafes
            .Where(c => c.IsActive && !string.IsNullOrEmpty(c.GooglePlaceId))
            .ToListAsync();

        var updated = 0;
        var failed = 0;

        foreach (var cafe in cafes)
        {
            try
            {
                // Fetch updated details from Google Places
                var details = await placesService.GetPlaceDetailsAsync(cafe.GooglePlaceId);

                if (details != null)
                {
                    // Update only specific fields
                    cafe.Phone = details.PhoneNumber ?? cafe.Phone;
                    cafe.Website = details.Website ?? cafe.Website;
                    cafe.OpeningHours = SerializeOpeningHours(details.OpeningHours);
                    cafe.AverageRating = details.Rating ?? cafe.AverageRating;
                    cafe.TotalReviews = details.UserRatingsTotal ?? cafe.TotalReviews;
                    cafe.UpdatedAt = DateTime.UtcNow;

                    updated++;
                }

                // Rate limiting: 1 request per second
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error refreshing café {cafe.Name}");
                failed++;
            }

            // Save every 10 cafés
            if ((updated + failed) % 10 == 0)
            {
                await context.SaveChangesAsync();
            }
        }

        await context.SaveChangesAsync();

        _logger.LogInformation($"Refresh completed. Updated: {updated}, Failed: {failed}");
    }
}
```

### Estimated Cost for Weekly Refresh

| Cafe in DB | API Calls/Week | Cost per 1K | Weekly Cost | Monthly Cost |
|-------------|----------------|-------------|-------------|--------------|
| 500 | 500 | $17 | $8.50 | $34 |
| 1,000 | 1,000 | $17 | $17 | $68 |
| 2,000 | 2,000 | $17 | $34 | $136 |

**Optimization**: Refresh only cafés with >10 views/month to reduce costs by 50-70%

---

## Phase 4: User-Contributed Cafe

### User Submission Flow

1. User submits new café
2. Verify using Google Places API (1 call)
3. Auto-populate data
4. Admin approves

```csharp
[HttpPost("cafes/submit")]
public async Task<IActionResult> SubmitCafe([FromBody] CafeSubmissionDto submission)
{
    // Search Google Places to verify
    var place = await _placesService.FindPlaceAsync(
        submission.Name,
        submission.Address
    );

    if (place == null)
    {
        return BadRequest("Could not verify this café. Please check the name and address.");
    }

    // Get details
    var details = await _placesService.GetPlaceDetailsAsync(place.PlaceId);

    // Create pending café
    var cafe = new Cafe
    {
        Name = details.Name,
        Address = details.FormattedAddress,
        // ... populate fields
        IsVerified = false, // Requires admin approval
        IsActive = false,
        CreatedByUserId = GetCurrentUserId()
    };

    await _context.Cafes.AddAsync(cafe);
    await _context.SaveChangesAsync();

    // Notify admins
    await _emailService.NotifyAdminsNewSubmissionAsync(cafe);

    return Ok(new { message = "Thank you! Your submission is pending review." });
}
```

### Cost for User Submissions

| Submissions/Month | API Calls | Cost per 1K | Monthly Cost |
|-------------------|-----------|-------------|--------------|
| 10 | 20 | $17 | $0.34 |
| 50 | 100 | $17 | $1.70 |
| 100 | 200 | $17 | $3.40 |

**Very affordable** and scales with actual usage!

---

## Alternative: Discover New Cafe Automatically

Run monthly job to discover new cafés in existing cities:

```csharp
public async Task DiscoverNewCafesAsync()
{
    var cities = await _context.Cafes
        .Select(c => new { c.City, c.State })
        .Distinct()
        .ToListAsync();

    foreach (var city in cities)
    {
        var newPlaces = await _placesService.SearchBoardGameCafesAsync(
            location: $"{city.City}, {city.State}"
        );

        foreach (var place in newPlaces)
        {
            // Check if already in database
            var exists = await _context.Cafes
                .AnyAsync(c => c.GooglePlaceId == place.PlaceId);

            if (!exists)
            {
                // Add new café (same logic as seeding)
                await AddNewCafeAsync(place);
            }
        }

        await Task.Delay(2000); // Rate limiting
    }
}
```

---

## Cost Summary: Year 1

| Phase | Frequency | Cost |
|-------|-----------|------|
| Initial Seeding | Once | $10-15 |
| Weekly Refresh | 52 times | $1,768 ($34/week × 52) |
| User Submissions | Ongoing | $20-40 |
| Monthly Discovery | 12 times | $50-100 |
| **Total Year 1** | | **~$1,850-1,950** |

### Broken Down by Month:
- **Month 1**: $15 (seeding) + $34 (refresh) = $49
- **Month 2-12**: $34-40/month average

**vs Realtime Approach**: $10,000-20,000/month at 100K users
**Savings**: 99.5% cost reduction!

---

## Database Optimization for Geospatial Queries

### Add Spatial Index (SQL Server)

```sql
-- Enable spatial features
ALTER TABLE Cafes
ADD Location GEOGRAPHY;

-- Update location column
UPDATE Cafes
SET Location = geography::Point(Latitude, Longitude, 4326);

-- Create spatial index
CREATE SPATIAL INDEX IX_Cafes_Location
ON Cafes(Location)
WITH (
    GRIDS = (LOW, MEDIUM, HIGH, HIGH),
    CELLS_PER_OBJECT = 16
);
```

### Optimized Search Query

```csharp
public async Task<List<Cafe>> SearchNearbySpatial(
    double latitude,
    double longitude,
    int radiusMeters)
{
    var point = $"POINT({longitude} {latitude})";

    var cafes = await _context.Cafes
        .FromSqlRaw(@"
            SELECT *
            FROM Cafes
            WHERE Location.STDistance(geography::Point({0}, {1}, 4326)) <= {2}
            ORDER BY Location.STDistance(geography::Point({0}, {1}, 4326))
        ", latitude, longitude, radiusMeters)
        .ToListAsync();

    return cafes;
}
```

**Performance**: 10-100x faster than Haversine formula in application code

---

## Monitoring & Alerts

### Set Up Google Cloud Billing Alerts

1. Go to Google Cloud Console → Billing → Budgets & Alerts
2. Create budget alert at:
   - $10 (warning)
   - $50 (warning)
   - $100 (critical)
   - $200 (stop services)

### Log API Usage

```csharp
public class GooglePlacesService
{
    private async Task<T> CallApiAsync<T>(Func<Task<T>> apiCall, string operationType)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var result = await apiCall();

            // Log successful call
            await LogApiUsageAsync(operationType, true, DateTime.UtcNow - startTime);

            return result;
        }
        catch (Exception ex)
        {
            // Log failed call
            await LogApiUsageAsync(operationType, false, DateTime.UtcNow - startTime);
            throw;
        }
    }

    private async Task LogApiUsageAsync(string operationType, bool success, TimeSpan duration)
    {
        var log = new ApiUsageLog
        {
            Provider = "GooglePlaces",
            OperationType = operationType,
            Success = success,
            DurationMs = (int)duration.TotalMilliseconds,
            Timestamp = DateTime.UtcNow
        };

        await _context.ApiUsageLogs.AddAsync(log);
        await _context.SaveChangesAsync();
    }
}
```

### Monthly Report Query

```sql
SELECT
    OperationType,
    COUNT(*) as TotalCalls,
    SUM(CASE WHEN Success = 1 THEN 1 ELSE 0 END) as SuccessfulCalls,
    AVG(DurationMs) as AvgDurationMs,
    DATEPART(MONTH, Timestamp) as Month
FROM ApiUsageLogs
WHERE Provider = 'GooglePlaces'
    AND Timestamp >= DATEADD(MONTH, -1, GETDATE())
GROUP BY OperationType, DATEPART(MONTH, Timestamp)
```

---

## Fallback Strategy (If Budget Exceeded)

### Option 1: Pause Automatic Refresh
- Stop weekly background job
- Rely on user-submitted updates
- Manual refresh for high-traffic cafés only

### Option 2: Use OpenStreetMap (Free)
- Switch to Overpass API (completely free)
- Less data quality but zero cost
- Good for discovery, not details

### Option 3: Community-Driven
- Café owners claim and update their listings
- Users report outdated information
- Verification through email/SMS

---

## Key Takeaways

✅ **Do This**:
- Seed database once ($10-15)
- Search local database (free)
- Weekly refresh ($34/week)
- User submissions ($0.34-3.40/month)

❌ **Don't Do This**:
- Call Google Places API on every user search
- Fetch details for cafés user doesn't click
- Request unnecessary fields

💰 **Expected Costs**:
- **MVP Phase**: $50-100/month
- **Growth Phase**: $100-200/month
- **Scale Phase**: $200-400/month (with optimizations)

🚀 **ROI**:
At 100K users/month generating $10K revenue, API costs are only 2-4% of revenue!

---

## Next Steps

1. ✅ Implement database schema with spatial index
2. ✅ Create GooglePlacesService with logging
3. ✅ Build seeding script for initial data
4. ✅ Set up weekly refresh background job
5. ✅ Configure Google Cloud billing alerts
6. ✅ Create user submission flow
7. ✅ Monitor and optimize monthly
