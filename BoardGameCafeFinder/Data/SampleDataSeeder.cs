using BoardGameCafeFinder.Models.Domain;
using System.Text.Json;

namespace BoardGameCafeFinder.Data
{
    /// <summary>
    /// Seeds sample café data for testing
    /// </summary>
    public class SampleDataSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SampleDataSeeder> _logger;

        public SampleDataSeeder(ApplicationDbContext context, ILogger<SampleDataSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            // Check if data already exists
            if (_context.Cafes.Any())
            {
                _logger.LogInformation("Database already contains café data. Skipping seed.");
                return;
            }

            _logger.LogInformation("Seeding sample café data...");

            var cafes = GetSampleCafes();

            _context.Cafes.AddRange(cafes);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Successfully seeded {cafes.Count} sample cafés");
        }

        private List<Cafe> GetSampleCafes()
        {
            var openingHours = GetStandardOpeningHours();

            return new List<Cafe>
            {
                // Seattle
                new Cafe
                {
                    Name = "Mox Boarding House",
                    Description = "A beloved Seattle board game café with an extensive library of over 1000 games, full bar, and restaurant.",
                    Address = "5105 Leary Ave NW",
                    City = "Seattle",
                    State = "WA",
                    Country = "United States",
                    PostalCode = "98107",
                    Latitude = 47.6647,
                    Longitude = -122.3831,
                    Phone = "(206) 523-6299",
                    Website = "https://www.moxboardinghouse.com",
                    OpeningHours = openingHours,
                    PriceRange = "$$",
                    AverageRating = 4.7m,
                    TotalReviews = 123,
                    IsVerified = true,
                    IsActive = true,
                    Slug = "mox-boarding-house-seattle",
                    MetaDescription = "Seattle's premier board game café with 1000+ games, full bar, and restaurant."
                },

                new Cafe
                {
                    Name = "Raygun Lounge",
                    Description = "Capitol Hill's cozy board game bar featuring craft cocktails and a curated game selection.",
                    Address = "501 E Pine St",
                    City = "Seattle",
                    State = "WA",
                    Country = "United States",
                    PostalCode = "98122",
                    Latitude = 47.6145,
                    Longitude = -122.3212,
                    Phone = "(206) 588-0352",
                    Website = "https://www.raygunlounge.com",
                    OpeningHours = openingHours,
                    PriceRange = "$$",
                    AverageRating = 4.5m,
                    TotalReviews = 87,
                    IsVerified = true,
                    IsActive = true,
                    Slug = "raygun-lounge-seattle",
                    MetaDescription = "Capitol Hill's board game bar with craft cocktails and curated games."
                },

                // Portland
                new Cafe
                {
                    Name = "Ground Kontrol",
                    Description = "Arcade bar with classic games and a great selection of craft beers.",
                    Address = "115 NW 5th Ave",
                    City = "Portland",
                    State = "OR",
                    Country = "United States",
                    PostalCode = "97209",
                    Latitude = 45.5235,
                    Longitude = -122.6756,
                    Phone = "(503) 796-9364",
                    Website = "https://www.groundkontrol.com",
                    OpeningHours = openingHours,
                    PriceRange = "$$",
                    AverageRating = 4.6m,
                    TotalReviews = 156,
                    IsVerified = true,
                    IsActive = true,
                    Slug = "ground-kontrol-portland",
                    MetaDescription = "Portland's premier arcade bar with classic games and craft beer."
                },

                new Cafe
                {
                    Name = "Guardian Games",
                    Description = "Massive game store and play space with tournaments and events.",
                    Address = "303 SE 3rd Ave",
                    City = "Portland",
                    State = "OR",
                    Country = "United States",
                    PostalCode = "97214",
                    Latitude = 45.5206,
                    Longitude = -122.6634,
                    Phone = "(503) 238-4000",
                    Website = "https://www.ggportland.com",
                    OpeningHours = openingHours,
                    PriceRange = "$",
                    AverageRating = 4.8m,
                    TotalReviews = 234,
                    IsVerified = true,
                    IsPremium = true,
                    IsActive = true,
                    Slug = "guardian-games-portland",
                    MetaDescription = "Portland's largest game store with tournaments and events."
                },

                // Chicago
                new Cafe
                {
                    Name = "Dice Dojo",
                    Description = "Chicago's friendly neighborhood board game café with 500+ games.",
                    Address = "1920 W Irving Park Rd",
                    City = "Chicago",
                    State = "IL",
                    Country = "United States",
                    PostalCode = "60613",
                    Latitude = 41.9541,
                    Longitude = -87.6785,
                    Phone = "(773) 525-5050",
                    Website = "https://www.dicedojochicago.com",
                    OpeningHours = openingHours,
                    PriceRange = "$$",
                    AverageRating = 4.4m,
                    TotalReviews = 98,
                    IsVerified = true,
                    IsActive = true,
                    Slug = "dice-dojo-chicago",
                    MetaDescription = "Chicago board game café with 500+ games and friendly atmosphere."
                },

                new Cafe
                {
                    Name = "The Gamers' Lodge",
                    Description = "Cozy board game café in Lincoln Park with great coffee and snacks.",
                    Address = "1147 W Diversey Pkwy",
                    City = "Chicago",
                    State = "IL",
                    Country = "United States",
                    PostalCode = "60614",
                    Latitude = 41.9320,
                    Longitude = -87.6567,
                    Phone = "(312) 555-GAME",
                    OpeningHours = openingHours,
                    PriceRange = "$$",
                    AverageRating = 4.3m,
                    TotalReviews = 67,
                    IsVerified = false,
                    IsActive = true,
                    Slug = "gamers-lodge-chicago",
                    MetaDescription = "Lincoln Park board game café with coffee and snacks."
                },

                // New York
                new Cafe
                {
                    Name = "The Brooklyn Strategist",
                    Description = "Brooklyn's board game café and store with gaming events and workshops.",
                    Address = "333 Court St",
                    City = "Brooklyn",
                    State = "NY",
                    Country = "United States",
                    PostalCode = "11231",
                    Latitude = 40.6806,
                    Longitude = -73.9987,
                    Phone = "(718) 576-3035",
                    Website = "https://www.thebrooklynstrategist.com",
                    OpeningHours = openingHours,
                    PriceRange = "$$",
                    AverageRating = 4.6m,
                    TotalReviews = 189,
                    IsVerified = true,
                    IsPremium = true,
                    IsActive = true,
                    Slug = "brooklyn-strategist-new-york",
                    MetaDescription = "Brooklyn board game café with events and workshops."
                },

                // San Francisco
                new Cafe
                {
                    Name = "The Game Parlour",
                    Description = "San Francisco's destination for board games, coffee, and community.",
                    Address = "3525 20th St",
                    City = "San Francisco",
                    State = "CA",
                    Country = "United States",
                    PostalCode = "94110",
                    Latitude = 37.7576,
                    Longitude = -122.4211,
                    Phone = "(415) 920-2550",
                    Website = "https://www.thegameparlour.com",
                    OpeningHours = openingHours,
                    PriceRange = "$$",
                    AverageRating = 4.5m,
                    TotalReviews = 142,
                    IsVerified = true,
                    IsActive = true,
                    Slug = "game-parlour-san-francisco",
                    MetaDescription = "San Francisco board game café with coffee and community."
                },

                // Los Angeles
                new Cafe
                {
                    Name = "Game Haus Café",
                    Description = "LA's board game café with 750+ games, full kitchen, and bar.",
                    Address = "1800 S Brand Blvd #107",
                    City = "Glendale",
                    State = "CA",
                    Country = "United States",
                    PostalCode = "91204",
                    Latitude = 34.1392,
                    Longitude = -118.2554,
                    Phone = "(818) 937-2489",
                    Website = "https://www.gamehausla.com",
                    OpeningHours = openingHours,
                    PriceRange = "$$",
                    AverageRating = 4.7m,
                    TotalReviews = 276,
                    IsVerified = true,
                    IsPremium = true,
                    IsActive = true,
                    Slug = "game-haus-cafe-los-angeles",
                    MetaDescription = "LA board game café with 750+ games, full kitchen, and bar."
                },

                // Austin
                new Cafe
                {
                    Name = "Vigilante Gaming",
                    Description = "Austin's board game bar with craft beer and an impressive game library.",
                    Address = "7108 Woodrow Ave #100",
                    City = "Austin",
                    State = "TX",
                    Country = "United States",
                    PostalCode = "78757",
                    Latitude = 30.3394,
                    Longitude = -97.7382,
                    Phone = "(512) 609-5995",
                    Website = "https://www.vigilantebar.com",
                    OpeningHours = openingHours,
                    PriceRange = "$$",
                    AverageRating = 4.6m,
                    TotalReviews = 198,
                    IsVerified = true,
                    IsActive = true,
                    Slug = "vigilante-gaming-austin",
                    MetaDescription = "Austin board game bar with craft beer and game library."
                }
            };
        }

        private string GetStandardOpeningHours()
        {
            // Monday-Thursday: 12PM-11PM (720-1380 minutes from midnight)
            // Friday: 12PM-12AM (720-1440)
            // Saturday: 11AM-12AM (660-1440)
            // Sunday: 11AM-10PM (660-1320)
            var hours = new List<OpeningHourPeriod>
            {
                new OpeningHourPeriod { DayOfWeek = 1, OpenMinutes = 720, CloseMinutes = 1380 },  // Monday
                new OpeningHourPeriod { DayOfWeek = 2, OpenMinutes = 720, CloseMinutes = 1380 },  // Tuesday
                new OpeningHourPeriod { DayOfWeek = 3, OpenMinutes = 720, CloseMinutes = 1380 },  // Wednesday
                new OpeningHourPeriod { DayOfWeek = 4, OpenMinutes = 720, CloseMinutes = 1380 },  // Thursday
                new OpeningHourPeriod { DayOfWeek = 5, OpenMinutes = 720, CloseMinutes = 1440 },  // Friday
                new OpeningHourPeriod { DayOfWeek = 6, OpenMinutes = 660, CloseMinutes = 1440 },  // Saturday
                new OpeningHourPeriod { DayOfWeek = 0, OpenMinutes = 660, CloseMinutes = 1320 }   // Sunday
            };

            return JsonSerializer.Serialize(hours);
        }
    }
}
