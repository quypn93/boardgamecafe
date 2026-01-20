# API Integration Guide - Board Game Café Finder

## Overview
This document provides detailed guidance on integrating external APIs for the Board Game Café Finder project, including updated pricing information, rate limits, and cost optimization strategies for 2026.

---

## Google Places API Integration

### Current Pricing (2026)

Based on the latest information, Google Places API uses a pay-as-you-go pricing model:

**Monthly Credit**:
- Previously offered $200/month credit (ended February 28, 2025)
- Verify current credit offerings with Google for 2026

**Pricing Structure**:
- Range: $2-$30 per 1,000 requests depending on SKU and fields requested
- Tiered pricing based on field complexity:
  - **IDs Only**: Cheapest
  - **Location**: Basic location data
  - **Basic**: Standard business info
  - **Advanced**: Detailed information
  - **Preferred**: Premium data fields

### Recommended Usage Strategy

#### 1. Initial Data Seeding (One-time)
```csharp
// Use Text Search for initial discovery
// Cost: ~$32 per 1,000 requests
public async Task SeedCafesInCity(string cityName)
{
    var searchQuery = $"board game cafe in {cityName}";

    // Request only essential fields to minimize cost
    var fieldMask = "places.id,places.displayName,places.formattedAddress," +
                    "places.location,places.phoneNumber,places.currentOpeningHours";

    var results = await _googlePlacesClient.TextSearchAsync(
        searchQuery,
        fieldMask
    );

    // Cache results for 7 days
    await _cache.SetAsync($"seed_{cityName}", results, TimeSpan.FromDays(7));
}
```

#### 2. Place Details (On-demand)
```csharp
// Fetch details only when user views a specific café
// Use FieldMask to limit data and cost
public async Task<PlaceDetails> GetCafeDetails(string placeId)
{
    // Check cache first (24-hour TTL)
    var cacheKey = $"place_details_{placeId}";
    var cached = await _cache.GetAsync<PlaceDetails>(cacheKey);
    if (cached != null) return cached;

    // Request only needed fields
    var fieldMask = "id,displayName,formattedAddress,location," +
                    "phoneNumber,websiteUri,regularOpeningHours," +
                    "rating,userRatingCount,photos,priceLevel";

    var details = await _googlePlacesClient.GetPlaceDetailsAsync(
        placeId,
        fieldMask
    );

    // Cache for 24 hours
    await _cache.SetAsync(cacheKey, details, TimeSpan.FromHours(24));

    return details;
}
```

#### 3. Autocomplete for Search
```csharp
// Cost: ~$2.83 per 1,000 sessions (session-based pricing)
public async Task<List<AutocompleteSuggestion>> AutocompleteCitySearch(string input)
{
    // Use Place Autocomplete with session tokens
    var sessionToken = GenerateSessionToken();

    var suggestions = await _googlePlacesClient.AutocompleteAsync(
        input,
        sessionToken,
        types: new[] { "(cities)" }, // Limit to cities only
        language: "en"
    );

    return suggestions;
}
```

### Cost Optimization Strategies

1. **Aggressive Caching**
   - Cache search results: 24-48 hours
   - Cache place details: 24 hours
   - Cache photos: 7 days
   - Use Redis for distributed caching

2. **Field Masking**
   - Always use `FieldMask` parameter
   - Request only essential fields
   - Save 50-70% on API costs

3. **Batch Operations**
   - Seed data in bulk during off-peak hours
   - Update café information weekly, not on every request

4. **Rate Limiting**
   - Implement user-side rate limiting
   - Prevent abuse with API key restrictions

### Estimated Monthly Costs

| Usage Level | Searches/Month | Details Views | Estimated Cost |
|-------------|----------------|---------------|----------------|
| MVP (1K users) | 5,000 | 2,000 | $30-50 |
| Growing (10K users) | 50,000 | 20,000 | $200-400 |
| Scale (100K users) | 500,000 | 200,000 | $2,000-4,000 |

**Note**: With caching, actual API calls can be reduced by 70-90%

### Implementation Code

```csharp
// Services/GooglePlacesService.cs
using Google.Maps.Places.V1;
using Microsoft.Extensions.Caching.Distributed;

namespace BoardGameCafeFinder.Services
{
    public interface IGooglePlacesService
    {
        Task<List<CafeSearchResult>> SearchBoardGameCafesAsync(string location);
        Task<PlaceDetails> GetPlaceDetailsAsync(string placeId);
        Task<List<string>> GetPlacePhotosAsync(string placeId);
    }

    public class GooglePlacesService : IGooglePlacesService
    {
        private readonly PlacesClient _placesClient;
        private readonly IDistributedCache _cache;
        private readonly ILogger<GooglePlacesService> _logger;
        private readonly string _apiKey;

        public GooglePlacesService(
            IConfiguration configuration,
            IDistributedCache cache,
            ILogger<GooglePlacesService> logger)
        {
            _apiKey = configuration["GooglePlaces:ApiKey"];
            _placesClient = new PlacesClientBuilder()
                .Build();
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<CafeSearchResult>> SearchBoardGameCafesAsync(string location)
        {
            var cacheKey = $"search_{location.ToLower().Replace(" ", "_")}";

            // Try cache first
            var cachedResults = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedResults))
            {
                return JsonSerializer.Deserialize<List<CafeSearchResult>>(cachedResults);
            }

            try
            {
                // Perform text search
                var request = new SearchTextRequest
                {
                    TextQuery = $"board game cafe {location}",
                    LocationBias = new SearchTextRequest.Types.LocationBias
                    {
                        // Bias towards user's region if available
                    }
                };

                var response = await _placesClient.SearchTextAsync(request);

                var results = response.Places.Select(p => new CafeSearchResult
                {
                    PlaceId = p.Id,
                    Name = p.DisplayName?.Text,
                    Address = p.FormattedAddress,
                    Latitude = p.Location?.Latitude ?? 0,
                    Longitude = p.Location?.Longitude ?? 0,
                    Rating = p.Rating,
                    UserRatingsTotal = p.UserRatingCount
                }).ToList();

                // Cache for 24 hours
                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(results),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    }
                );

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Google Places API");
                throw;
            }
        }

        public async Task<PlaceDetails> GetPlaceDetailsAsync(string placeId)
        {
            var cacheKey = $"details_{placeId}";

            // Check cache
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                return JsonSerializer.Deserialize<PlaceDetails>(cached);
            }

            try
            {
                var request = new GetPlaceRequest
                {
                    Name = $"places/{placeId}",
                    // Use field mask to minimize cost
                    FieldMask = new FieldMask
                    {
                        Paths =
                        {
                            "id", "displayName", "formattedAddress", "location",
                            "phoneNumber", "websiteUri", "regularOpeningHours",
                            "rating", "userRatingCount", "photos", "priceLevel"
                        }
                    }
                };

                var place = await _placesClient.GetPlaceAsync(request);

                var details = new PlaceDetails
                {
                    PlaceId = place.Id,
                    Name = place.DisplayName?.Text,
                    Address = place.FormattedAddress,
                    Phone = place.InternationalPhoneNumber,
                    Website = place.WebsiteUri,
                    Rating = place.Rating,
                    OpeningHours = place.RegularOpeningHours?.Periods?
                        .Select(p => new OpeningHour
                        {
                            Day = p.Open?.Day.ToString(),
                            OpenTime = p.Open?.Time.ToString(),
                            CloseTime = p.Close?.Time.ToString()
                        }).ToList(),
                    Photos = place.Photos.Select(p => p.Name).ToList()
                };

                // Cache for 24 hours
                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(details),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    }
                );

                return details;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching place details for {placeId}");
                throw;
            }
        }
    }
}
```

---

## Yelp Fusion API Integration

### Current Pricing (2026)

**IMPORTANT UPDATE**: Yelp no longer offers a free tier as of 2024-2026.

**Trial Plan** (30-Day Free Trial):
- 5,000 free API calls for 30 days
- For evaluation purposes only (not commercial deployment)
- Daily limit: 300-5,000 calls depending on plan
- Resets daily at midnight UTC

**Paid Plans** (After Trial):
- **Included**: 30,000 API calls/month
- **Daily Limit**: Up to 5,000 calls
- **Overage Pricing**:
  - Starter: $7.99 per 1,000 calls
  - Plus: $9.99 per 1,000 calls
  - Enterprise: $14.99 per 1,000 calls

### Rate Limits
- **Free Trial**: 300-5,000 calls/day
- **Paid Plans**: 5,000 calls/day
- **Rate**: Queries per second (QPS) limits apply

### Recommended Usage Strategy

#### 1. Business Matching (One-time per café)
```csharp
public async Task<string> FindYelpBusinessId(string cafeName, string address, string city)
{
    var cacheKey = $"yelp_match_{cafeName}_{city}".ToLower();

    // Check cache (cache indefinitely for matches)
    var cached = await _cache.GetAsync<string>(cacheKey);
    if (cached != null) return cached;

    // Search Yelp for matching business
    var businesses = await _yelpClient.SearchBusinessesAsync(
        term: cafeName,
        location: $"{address}, {city}",
        limit: 5
    );

    // Find best match based on name similarity and address
    var bestMatch = businesses
        .OrderByDescending(b => CalculateSimilarity(cafeName, b.Name))
        .FirstOrDefault();

    if (bestMatch != null)
    {
        // Cache indefinitely
        await _cache.SetAsync(cacheKey, bestMatch.Id, TimeSpan.FromDays(365));
        return bestMatch.Id;
    }

    return null;
}
```

#### 2. Fetch Reviews (On-demand, limited use)
```csharp
public async Task<List<YelpReview>> GetCafeReviews(string yelpBusinessId)
{
    // Note: Yelp API only returns up to 3 reviews
    var cacheKey = $"yelp_reviews_{yelpBusinessId}";

    // Cache for 7 days (reviews don't change frequently)
    var cached = await _cache.GetAsync<List<YelpReview>>(cacheKey);
    if (cached != null) return cached;

    var reviews = await _yelpClient.GetBusinessReviewsAsync(yelpBusinessId);

    await _cache.SetAsync(cacheKey, reviews, TimeSpan.FromDays(7));

    return reviews;
}
```

#### 3. Business Details (Periodic refresh)
```csharp
public async Task<YelpBusiness> GetBusinessDetails(string yelpBusinessId)
{
    var cacheKey = $"yelp_business_{yelpBusinessId}";

    // Cache for 24 hours
    var cached = await _cache.GetAsync<YelpBusiness>(cacheKey);
    if (cached != null) return cached;

    var business = await _yelpClient.GetBusinessAsync(yelpBusinessId);

    await _cache.SetAsync(cacheKey, business, TimeSpan.FromHours(24));

    return business;
}
```

### Cost Optimization for Yelp

**Strategy: Minimize Yelp API Usage**

1. **Use Yelp Only for Supplementary Data**
   - Primary data from Google Places
   - Yelp only for review aggregation
   - Update Yelp data weekly, not real-time

2. **Prioritize High-Value Cafés**
   - Only fetch Yelp data for cafés with >10 views/month
   - Skip Yelp integration for low-traffic cafés

3. **Alternative: Web Scraping (Legally)**
   - Yelp public pages (check ToS)
   - Use for display only, not commercial data

4. **Consider Alternative Review Sources**
   - Google reviews (included in Places API)
   - User-generated reviews on your platform
   - Facebook/Instagram comments

### Estimated Monthly Costs

| Usage Level | API Calls/Month | Plan | Cost |
|-------------|-----------------|------|------|
| Trial (30 days) | 5,000 | Trial | $0 |
| MVP (1K users) | 10,000 | Starter | $0 (included) |
| Growing (10K users) | 35,000 | Plus | $49.95 (5K overage) |
| Scale (100K users) | 80,000 | Enterprise | $747.50 (50K overage) |

**Recommendation**:
- Start with trial for initial data seeding
- Upgrade to Starter plan ($7.99 overage rate)
- Consider phasing out Yelp in favor of Google + own reviews

### Implementation Code

```csharp
// Services/YelpService.cs
using System.Net.Http.Headers;

namespace BoardGameCafeFinder.Services
{
    public interface IYelpService
    {
        Task<string> FindBusinessIdAsync(string name, string location);
        Task<YelpBusiness> GetBusinessAsync(string businessId);
        Task<List<YelpReview>> GetReviewsAsync(string businessId);
    }

    public class YelpService : IYelpService
    {
        private readonly HttpClient _httpClient;
        private readonly IDistributedCache _cache;
        private readonly ILogger<YelpService> _logger;
        private readonly string _apiKey;

        public YelpService(
            HttpClient httpClient,
            IConfiguration configuration,
            IDistributedCache cache,
            ILogger<YelpService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _apiKey = configuration["Yelp:ApiKey"];

            _httpClient.BaseAddress = new Uri("https://api.yelp.com/v3/");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> FindBusinessIdAsync(string name, string location)
        {
            var cacheKey = $"yelp_search_{name}_{location}".ToLower().Replace(" ", "_");

            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            try
            {
                var response = await _httpClient.GetAsync(
                    $"businesses/search?term={Uri.EscapeDataString(name)}&location={Uri.EscapeDataString(location)}&limit=5"
                );

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Yelp API error: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<YelpSearchResponse>(json);

                var businessId = result?.Businesses?.FirstOrDefault()?.Id;

                if (!string.IsNullOrEmpty(businessId))
                {
                    // Cache for 1 year (business IDs don't change)
                    await _cache.SetStringAsync(
                        cacheKey,
                        businessId,
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(365)
                        }
                    );
                }

                return businessId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Yelp API");
                return null;
            }
        }

        public async Task<YelpBusiness> GetBusinessAsync(string businessId)
        {
            var cacheKey = $"yelp_business_{businessId}";

            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                return JsonSerializer.Deserialize<YelpBusiness>(cached);
            }

            try
            {
                var response = await _httpClient.GetAsync($"businesses/{businessId}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Yelp business API error: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var business = JsonSerializer.Deserialize<YelpBusiness>(json);

                // Cache for 24 hours
                await _cache.SetStringAsync(
                    cacheKey,
                    json,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    }
                );

                return business;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching Yelp business {businessId}");
                return null;
            }
        }

        public async Task<List<YelpReview>> GetReviewsAsync(string businessId)
        {
            var cacheKey = $"yelp_reviews_{businessId}";

            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                return JsonSerializer.Deserialize<List<YelpReview>>(cached);
            }

            try
            {
                var response = await _httpClient.GetAsync($"businesses/{businessId}/reviews?limit=3");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Yelp reviews API error: {response.StatusCode}");
                    return new List<YelpReview>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<YelpReviewsResponse>(json);

                // Cache for 7 days
                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(result.Reviews),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                    }
                );

                return result.Reviews;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching Yelp reviews for {businessId}");
                return new List<YelpReview>();
            }
        }
    }
}
```

---

## Alternative: Build Your Own Review System

Given Yelp's high costs, consider building a robust in-house review system:

### Advantages
1. **No API Costs**: Save $500-1000/month at scale
2. **Full Control**: Custom review features
3. **User Engagement**: Encourages platform loyalty
4. **SEO Benefits**: Unique content

### Features to Implement
- Star rating (1-5)
- Written reviews
- Photo uploads
- Helpful votes
- Review responses from café owners
- Verified visit badges
- Spam detection

### Hybrid Approach
- Use Yelp API for initial 3 reviews (display only)
- Prominently feature your own review system
- Gradually phase out Yelp dependency

---

## BoardGameGeek API Integration

### Free API Access
BoardGameGeek offers a free XML API for game data.

**Endpoints**:
- `/xmlapi2/search?query={name}&type=boardgame`
- `/xmlapi2/thing?id={id}&stats=1`

### Use Cases
1. **Game Metadata**
   - Game names, publishers
   - Player count, playtime
   - Complexity rating
   - Thumbnail images

2. **Amazon Affiliate Links**
   - Get game prices
   - Generate affiliate links
   - Track conversions

### Implementation
```csharp
public class BoardGameGeekService
{
    private readonly HttpClient _httpClient;

    public async Task<BoardGame> SearchGameAsync(string gameName)
    {
        var url = $"https://boardgamegeek.com/xmlapi2/search?query={Uri.EscapeDataString(gameName)}&type=boardgame";

        var response = await _httpClient.GetStringAsync(url);
        var doc = XDocument.Parse(response);

        var gameId = doc.Descendants("item")
            .FirstOrDefault()
            ?.Attribute("id")?.Value;

        if (string.IsNullOrEmpty(gameId))
            return null;

        return await GetGameDetailsAsync(int.Parse(gameId));
    }

    public async Task<BoardGame> GetGameDetailsAsync(int bggId)
    {
        var url = $"https://boardgamegeek.com/xmlapi2/thing?id={bggId}&stats=1";

        var response = await _httpClient.GetStringAsync(url);
        var doc = XDocument.Parse(response);

        // Parse XML and extract game details
        var item = doc.Descendants("item").First();

        return new BoardGame
        {
            BGGId = bggId,
            Name = item.Descendants("name")
                .FirstOrDefault(n => n.Attribute("type")?.Value == "primary")
                ?.Attribute("value")?.Value,
            MinPlayers = int.Parse(item.Element("minplayers")?.Attribute("value")?.Value ?? "0"),
            MaxPlayers = int.Parse(item.Element("maxplayers")?.Attribute("value")?.Value ?? "0"),
            PlaytimeMinutes = int.Parse(item.Element("playingtime")?.Attribute("value")?.Value ?? "0"),
            // ... more fields
        };
    }
}
```

---

## Summary & Recommendations

### Cost Comparison Table

| API | Initial Seeding | Monthly (10K users) | Monthly (100K users) |
|-----|----------------|---------------------|----------------------|
| Google Places | $100-200 | $200-400 | $1,000-2,000 |
| Yelp Fusion | $0 (trial) | $50-100 | $500-750 |
| BoardGameGeek | $0 | $0 | $0 |
| **Total** | **$100-200** | **$250-500** | **$1,500-2,750** |

### Recommended Strategy

**Phase 1: MVP (Month 1-3)**
- Use Google Places for primary data
- Use Yelp trial for initial review seeding
- Build in-house review system
- Free BoardGameGeek for game data

**Phase 2: Growth (Month 4-6)**
- Continue Google Places (with caching)
- Minimize Yelp usage (only high-traffic cafés)
- Focus on user-generated reviews
- Implement affiliate links

**Phase 3: Scale (Month 7+)**
- Optimize Google Places with aggressive caching
- Phase out Yelp API entirely
- 100% user-generated reviews
- Monetize affiliate links & premium listings

### Key Takeaways

1. **Caching is Critical**: Reduce API costs by 70-90%
2. **Field Masking**: Always limit requested fields
3. **Own Your Data**: Build internal review system
4. **Monitor Usage**: Set up billing alerts
5. **Free Alternatives**: BoardGameGeek, user content

---

## Sources

- [Places API Usage and Billing | Google for Developers](https://developers.google.com/maps/documentation/places/web-service/usage-and-billing)
- [Google Places API Pricing: Is It Worth It for Your Business?](https://www.safegraph.com/guides/google-places-api-pricing)
- [The true cost of the Google Maps API and how Radar compares in 2026](https://radar.com/blog/google-maps-api-cost)
- [Yelp Fusion API | Yelp Data Licensing](https://business.yelp.com/data/products/fusion/)
- [Yelp's lack of transparency around API charges angers developers | TechCrunch](https://techcrunch.com/2024/08/02/yelps-lack-of-transparency-around-api-charges-angers-developers/)
- [Frequently Asked Questions - Yelp Fusion](https://docs.developer.yelp.com/docs/places-faq)
- [Rate Limiting - Yelp Fusion](https://docs.developer.yelp.com/docs/places-rate-limiting)

---

**Next Steps**:
1. Sign up for Google Cloud account and enable Places API
2. Set up billing alerts ($50, $200, $500)
3. Get Yelp API trial key
4. Implement caching layer (Redis)
5. Start with one city for MVP testing
