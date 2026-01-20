# Board Game Café Finder - Project Setup Complete ✅

## Summary

Tôi đã hoàn thành setup project structure và database cho dự án Board Game Café Finder với .NET Core 8 MVC.

---

## ✅ Completed Tasks

### 1. Google Places API Strategy Analysis
**File**: [GOOGLE_PLACES_STRATEGY.md](GOOGLE_PLACES_STRATEGY.md)

**Key Decision**: **SEED DATABASE FIRST approach** (Tiết kiệm 95% chi phí!)

- ✅ Initial seeding: $10-15 (one-time)
- ✅ Runtime searches: $0 (search local DB)
- ✅ Weekly refresh: $34/week
- ✅ User submissions: $0.34-3.40/month

**vs Realtime API approach**: $10,000-20,000/month at scale ❌

### 2. Project Structure Created
```
BoardGameCafeFinder/
├── Controllers/         # MVC Controllers (default HomeController included)
├── Models/
│   ├── Domain/         # ✅ All domain models created
│   ├── ViewModels/     # Ready for implementation
│   └── DTOs/          # Ready for API responses
├── Views/             # Razor views
├── Services/          # Ready for business logic
├── Repositories/      # Ready for data access layer
├── Data/              # ✅ Database context configured
├── wwwroot/           # Static files (CSS, JS, images)
├── Migrations/        # ✅ Initial migration created
├── appsettings.json   # ✅ Configured
└── Program.cs         # ✅ Services registered
```

### 3. Domain Models Created (8 models)

All models are in: `Models/Domain/`

#### ✅ Cafe.cs
- Full location data (address, lat/long, city, state)
- Business info (hours, phone, website)
- External IDs (GooglePlaceId, YelpBusinessId)
- Ratings & stats
- Status flags (IsVerified, IsPremium, IsActive)
- SEO fields (Slug, MetaDescription)
- Helper methods: `IsOpenNow()`, `GetOpeningHours()`

#### ✅ BoardGame.cs
- Game metadata (name, publisher, description)
- Player info (MinPlayers, MaxPlayers)
- Playtime, age rating, complexity
- BGG integration (BGGId)
- Amazon affiliate URL
- Helper methods: `GetPlayerRange()`, `GetPlaytime()`

#### ✅ CafeGame.cs (Many-to-Many)
- Links Cafes ↔ BoardGames
- Tracks availability, quantity
- Rental pricing
- Crowdsourced verification
- Last verified date

#### ✅ User.cs (extends Identity)
- Profile info (name, bio, avatar)
- Location for recommendations
- Stats (reviews, bookings, reputation)
- Café owner flag
- Favorite game types

#### ✅ Review.cs
- Rating (1-5 stars with check constraint)
- Title & content
- Visit date
- Verified visit badge
- Helpful count
- Helper methods: `GetRatingStars()`, `GetTimeAgo()`

#### ✅ Event.cs
- Event details (title, description, type)
- DateTime (start, end)
- Capacity (max participants, current)
- Entry fee
- Helper methods: `IsUpcoming()`, `IsFull()`, `GetAvailableSeats()`

#### ✅ EventBooking.cs
- Booking status (Confirmed, Cancelled, Waitlist)
- Payment status (Pending, Paid, Refunded)
- Number of seats
- Cancellation support
- Helper method: `CanCancel()` (24h before event)

#### ✅ Photo.cs
- Photo URLs (full & thumbnail)
- Caption
- Display order
- Approval system
- User attribution

#### ✅ PremiumListing.cs
- Plan types: Basic ($50), Premium ($100), Featured ($200)
- Start/End dates
- Features: FeaturedPlacement, PhotoGallery, EventListings, etc.
- Helper methods: `IsExpired()`, `GetDaysRemaining()`

### 4. Database Configuration

#### ✅ ApplicationDbContext.cs
Located: `Data/ApplicationDbContext.cs`

**Features**:
- Extends `IdentityDbContext<User, IdentityRole<int>, int>`
- All 8 DbSets configured
- Comprehensive indexes:
  - Geospatial index on (Latitude, Longitude)
  - Unique indexes on Slug, GooglePlaceId
  - Performance indexes on City, CreatedAt, etc.
- Relationships configured:
  - One-to-Many (Cafe → Reviews, Events, Photos)
  - Many-to-Many (Cafe ↔ BoardGames via CafeGame)
  - Identity relationships
- Default values:
  - Timestamps (GETUTCDATE())
  - Flags (IsActive = true)
  - Counts (HelpfulCount = 0)
- Check constraints:
  - Review Rating between 1-5

#### ✅ Connection String (appsettings.json)
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BoardGameCafeFinderDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

**Database**: SQL Server LocalDB
**Name**: BoardGameCafeFinderDb

### 5. ASP.NET Core Identity Setup

#### ✅ Program.cs Configuration
**Services Registered**:
- ✅ Entity Framework Core with SQL Server
- ✅ ASP.NET Core Identity
  - Password requirements (8+ chars, upper/lower/digit)
  - Lockout after 5 failed attempts (15 min)
  - Unique email required
- ✅ Session support (30 min timeout)
- ✅ MVC Controllers + Views
- ✅ Razor Pages (for Identity UI)

**Middleware Pipeline**:
1. HTTPS Redirection
2. Static Files
3. Routing
4. Session
5. **Authentication** ✅
6. **Authorization** ✅
7. MVC Routes
8. Razor Pages

### 6. Migrations

#### ✅ Initial Migration Created
```bash
dotnet ef migrations add InitialCreate
```

**Migration includes**:
- AspNetUsers, AspNetRoles (Identity tables)
- Cafes
- BoardGames
- CafeGames
- Reviews
- Events
- EventBookings
- Photos
- PremiumListings
- All indexes and constraints

#### ✅ Database Created
```bash
dotnet ef database update
```

**Result**: Database "BoardGameCafeFinderDb" created with all tables!

---

## 📦 NuGet Packages Installed

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.11 | SQL Server provider |
| Microsoft.EntityFrameworkCore.Tools | 8.0.11 | Migrations CLI |
| Microsoft.EntityFrameworkCore.Design | 8.0.11 | Design-time support |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.11 | Identity + EF integration |
| StackExchange.Redis | 2.8.16 | Redis caching (commented out for MVP) |
| Newtonsoft.Json | 13.0.3 | JSON serialization |

---

## 🗂️ File Structure Created

```
f:\QuyPham\BoardGameCFFinder\
├── PROJECT_PLANNING.md                    # ✅ Complete planning doc
├── API_INTEGRATION_GUIDE.md               # ✅ API cost analysis
├── GOOGLE_PLACES_STRATEGY.md              # ✅ Cost optimization strategy
├── PROJECT_SETUP_COMPLETE.md              # ✅ This file
│
└── BoardGameCafeFinder/                   # ✅ .NET Core 8 MVC Project
    ├── Controllers/
    │   └── HomeController.cs
    ├── Models/
    │   ├── Domain/                        # ✅ 8 domain models
    │   │   ├── Cafe.cs
    │   │   ├── BoardGame.cs
    │   │   ├── CafeGame.cs
    │   │   ├── User.cs
    │   │   ├── Review.cs
    │   │   ├── Event.cs
    │   │   ├── EventBooking.cs
    │   │   ├── Photo.cs
    │   │   └── PremiumListing.cs
    │   ├── ViewModels/
    │   └── DTOs/
    ├── Views/
    │   ├── Home/
    │   └── Shared/
    ├── Services/                          # Ready for implementation
    ├── Repositories/                      # Ready for implementation
    ├── Data/
    │   └── ApplicationDbContext.cs        # ✅ Configured
    ├── Migrations/
    │   └── [timestamp]_InitialCreate.cs   # ✅ Created
    ├── wwwroot/
    │   ├── css/
    │   ├── js/
    │   └── lib/
    ├── appsettings.json                   # ✅ Configured
    ├── Program.cs                         # ✅ Services registered
    └── BoardGameCafeFinder.csproj
```

---

## 🚀 What's Ready

### ✅ Infrastructure
- [x] Project structure
- [x] Database schema
- [x] Entity Framework migrations
- [x] Identity authentication
- [x] Configuration files

### ✅ Domain Models
- [x] Cafe (with geospatial data)
- [x] BoardGame (with BGG integration)
- [x] CafeGame (many-to-many)
- [x] User (extended Identity)
- [x] Review (with ratings)
- [x] Event (with bookings)
- [x] EventBooking (with payment)
- [x] Photo (with approval)
- [x] PremiumListing (monetization)

### ✅ Documentation
- [x] Complete planning document (73 pages)
- [x] API integration guide (with 2026 pricing)
- [x] Google Places cost optimization strategy
- [x] Database schema design
- [x] Technical architecture

---

## 📋 Next Steps (MVP Implementation)

### Phase 1: Core Services (Week 1-2)
**Priority**: HIGH

1. **Create Service Layer**
   - [ ] `ICafeService` / `CafeService`
     - SearchNearbyAsync (geospatial queries)
     - GetByIdAsync, GetBySlugAsync
     - CreateAsync, UpdateAsync
   - [ ] `IGooglePlacesService` / `GooglePlacesService`
     - SearchBoardGameCafesAsync
     - GetPlaceDetailsAsync
     - (See GOOGLE_PLACES_STRATEGY.md for implementation)
   - [ ] `IReviewService` / `ReviewService`
     - GetReviewsForCafeAsync
     - CreateReviewAsync
     - UpdateCafeRatingAsync (aggregate)

2. **Create Repository Layer**
   - [ ] `ICafeRepository` / `CafeRepository`
   - [ ] `IReviewRepository` / `ReviewRepository`
   - [ ] `IEventRepository` / `EventRepository`
   - [ ] Unit of Work pattern (optional)

3. **Dependency Injection Setup**
   - [ ] Register services in Program.cs
   - [ ] Configure service lifetimes (Scoped, Singleton, Transient)

### Phase 2: Data Seeding (Week 2)
**Priority**: HIGH - Needed before MVP launch

1. **Create Seeding Service**
   - [ ] `DataSeedingService.cs`
   - [ ] Target 20 major US cities
   - [ ] ~300-500 cafés
   - [ ] Cost: $10-15 (see GOOGLE_PLACES_STRATEGY.md)

2. **Seeding Command**
   - [ ] CLI command: `dotnet run -- seed-data`
   - [ ] Progress logging
   - [ ] Error handling & retry logic

3. **Sample Data**
   - [ ] Create seed data for testing:
     - 5-10 sample cafés
     - 10-20 board games
     - Sample reviews
     - Sample events

### Phase 3: Map-Based Search (Week 3-4)
**Priority**: HIGH - Core MVP feature

1. **Backend API**
   - [ ] `MapController.cs` or extend `ApiController`
   - [ ] POST `/api/cafes/search`
     - Accept: latitude, longitude, radius, filters
     - Return: list of cafés with distance
   - [ ] Geospatial query optimization
   - [ ] Caching with Redis (optional)

2. **Frontend Views**
   - [ ] `Views/Map/Index.cshtml`
   - [ ] Sidebar with search & filters
   - [ ] Results list (dynamic)

3. **JavaScript Integration**
   - [ ] `wwwroot/js/map.js`
   - [ ] Google Maps JavaScript API
   - [ ] Interactive markers
   - [ ] Info windows
   - [ ] Autocomplete for location search
   - [ ] See PROJECT_PLANNING.md for complete code

### Phase 4: Café Listings (Week 4-5)
**Priority**: MEDIUM

1. **Café Pages**
   - [ ] `CafesController.cs`
     - Index (list view)
     - Details (individual café)
     - Search/filter actions
   - [ ] Views:
     - `Index.cshtml` (list with filters)
     - `Details.cshtml` (full info + map + reviews)

2. **View Models**
   - [ ] `CafeListViewModel`
   - [ ] `CafeDetailsViewModel`
   - [ ] `CafeSearchViewModel`

3. **Features**
   - [ ] Pagination
   - [ ] Sorting (distance, rating, name)
   - [ ] Filters (open now, has games, price range)
   - [ ] Breadcrumbs
   - [ ] SEO (meta tags, structured data)

### Phase 5: Reviews System (Week 5-6)
**Priority**: MEDIUM

1. **Review Features**
   - [ ] Create review form
   - [ ] Star rating component
   - [ ] Photo upload (optional)
   - [ ] Spam detection (basic)

2. **Review Display**
   - [ ] List reviews on café page
   - [ ] Sort (newest, highest rated, most helpful)
   - [ ] "Helpful" voting
   - [ ] Pagination

3. **Moderation**
   - [ ] Admin review approval
   - [ ] Report review functionality

### Phase 6: User Authentication (Week 6-7)
**Priority**: MEDIUM

1. **Identity UI**
   - [ ] Scaffold Identity pages (Register, Login, etc.)
   - [ ] Customize styling to match site
   - [ ] Email confirmation (optional)

2. **User Profile**
   - [ ] Profile page
   - [ ] Edit profile
   - [ ] My reviews
   - [ ] My bookings

3. **Authorization**
   - [ ] [Authorize] attributes
   - [ ] Role-based access (Admin, CafeOwner, User)
   - [ ] Claims-based authorization

### Phase 7: Admin Panel (Week 7-8)
**Priority**: LOW (can use database directly initially)

1. **Admin Dashboard**
   - [ ] `AdminController.cs`
   - [ ] Statistics (users, cafés, reviews)
   - [ ] Recent activity

2. **Café Management**
   - [ ] Approve/reject user submissions
   - [ ] Edit café details
   - [ ] Verify cafés
   - [ ] Mark as inactive/closed

3. **Content Moderation**
   - [ ] Review moderation
   - [ ] Photo approval
   - [ ] User management

### Phase 8: Weekly Refresh Job (Week 8)
**Priority**: MEDIUM

1. **Background Service**
   - [ ] `CafeRefreshService.cs` (BackgroundService)
   - [ ] Update café data from Google Places API
   - [ ] Run weekly
   - [ ] Logging & error handling

2. **Monitoring**
   - [ ] API usage tracking
   - [ ] Cost monitoring
   - [ ] Alert system

### Phase 9: Polish & Launch (Week 9-10)
**Priority**: HIGH before public launch

1. **UI/UX**
   - [ ] Responsive design (mobile, tablet)
   - [ ] Loading states
   - [ ] Error pages (404, 500)
   - [ ] Toast notifications

2. **Performance**
   - [ ] Enable Redis caching
   - [ ] Image optimization
   - [ ] Minify CSS/JS
   - [ ] CDN setup

3. **SEO**
   - [ ] Sitemap.xml
   - [ ] Robots.txt
   - [ ] Meta tags
   - [ ] Schema markup
   - [ ] Google Search Console

4. **Analytics**
   - [ ] Google Analytics 4
   - [ ] Track key events (searches, reviews, bookings)

5. **Deployment**
   - [ ] Azure App Service / AWS
   - [ ] Production database
   - [ ] SSL certificate
   - [ ] Environment variables
   - [ ] CI/CD pipeline

---

## 💰 Cost Recap

### Initial Investment
| Item | Cost |
|------|------|
| Initial data seeding (300-500 cafés) | $10-15 |
| Domain name (1 year) | $12-15 |
| SSL certificate | Free (Let's Encrypt) |
| **Total Initial** | **$22-30** |

### Monthly Operating Costs (MVP)

| Item | Cost |
|------|------|
| Hosting (Azure/AWS - Basic) | $10-30 |
| Database | $0-20 (LocalDB free, SQL Basic $5-20) |
| Google Places API (weekly refresh) | $35-50 |
| Email service (SendGrid - Free tier) | $0 |
| **Total Monthly (MVP)** | **$45-100** |

### Projected Revenue (Month 3-6)
- 10K visitors → $500/month
- Covers costs at ~1K visitors/month
- Break even: Week 6-8 after launch

---

## 🔧 Development Setup

### Prerequisites
- [x] .NET 8.0 SDK installed
- [x] SQL Server LocalDB installed
- [x] Visual Studio 2022 or VS Code
- [x] dotnet-ef tools installed (v8.0.11)

### Running the Project

1. **Clone/Navigate to project**
   ```bash
   cd f:\QuyPham\BoardGameCFFinder\BoardGameCafeFinder
   ```

2. **Restore packages** (if needed)
   ```bash
   dotnet restore
   ```

3. **Update database** (if not done)
   ```bash
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```
   or press F5 in Visual Studio

5. **Access in browser**
   ```
   https://localhost:7xxx  (port may vary)
   http://localhost:5xxx
   ```

### Useful Commands

```bash
# Build project
dotnet build

# Run project (watch mode for development)
dotnet watch run

# Create new migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update

# Rollback migration
dotnet ef database update PreviousMigrationName

# Remove last migration (if not applied)
dotnet ef migrations remove

# View database info
dotnet ef database info

# Generate SQL script
dotnet ef migrations script
```

---

## 🎯 MVP Success Criteria

### Week 10 Launch Goals
- [ ] 300+ cafés seeded (top 20 US cities)
- [ ] Map-based search working
- [ ] Café detail pages live
- [ ] User registration/login
- [ ] Review system functional
- [ ] Mobile responsive
- [ ] SEO optimized
- [ ] Deployed to production

### Month 3 Goals
- [ ] 1,000+ monthly visitors
- [ ] 50+ user reviews
- [ ] 5+ user-submitted cafés
- [ ] $100+ monthly revenue (café listings)
- [ ] Social media presence (Reddit, Instagram)

### Month 6 Goals
- [ ] 10,000+ monthly visitors
- [ ] 500+ user reviews
- [ ] 50+ café owners claimed listings
- [ ] $1,000+ monthly revenue
- [ ] 10+ premium listings
- [ ] First event booking

---

## 📚 Documentation Index

1. **[PROJECT_PLANNING.md](PROJECT_PLANNING.md)** (73 pages)
   - Market analysis
   - Full technical architecture
   - Database schema with SQL
   - Map-based search implementation
   - Revenue projections
   - 12-month roadmap

2. **[API_INTEGRATION_GUIDE.md](API_INTEGRATION_GUIDE.md)**
   - Google Places API (updated 2026 pricing)
   - Yelp Fusion API (NO FREE TIER - updated info)
   - BoardGameGeek API (free)
   - Cost comparison tables
   - Implementation code samples

3. **[GOOGLE_PLACES_STRATEGY.md](GOOGLE_PLACES_STRATEGY.md)**
   - Complete cost optimization strategy
   - Seed vs Realtime comparison
   - Implementation code for seeding
   - Weekly refresh background job
   - User submission flow
   - Cost breakdown (95% savings!)

4. **[PROJECT_SETUP_COMPLETE.md](PROJECT_SETUP_COMPLETE.md)** (This file)
   - Setup completion summary
   - File structure
   - Next steps
   - Development guide

---

## 🤝 Ready to Code!

**Current Status**: ✅ Foundation Complete (Database, Models, Configuration)

**Next Action**: Implement services layer and start building the map-based search feature!

**Estimated Time to MVP**: 8-10 weeks (working part-time)

---

## ❓ Need Help?

### Common Issues

**Issue**: `dotnet ef` not found
```bash
dotnet tool install --global dotnet-ef --version 8.0.11
```

**Issue**: Database connection failed
- Check SQL Server LocalDB is running
- Verify connection string in appsettings.json

**Issue**: Migration failed
```bash
# Remove migration
dotnet ef migrations remove

# Drop database and start fresh
dotnet ef database drop
dotnet ef database update
```

**Issue**: Package restore failed
```bash
dotnet clean
dotnet restore
dotnet build
```

---

## 📞 Contact & Feedback

- **GitHub Issues**: (setup when repo is created)
- **Documentation**: All markdown files in project root
- **API Keys**: Update in appsettings.json before running seeding

---

## 🎉 Congratulations!

Bạn đã có một project foundation hoàn chỉnh và ready để bắt đầu xây dựng MVP!

**Key Achievements**:
✅ .NET Core 8 MVC project structure
✅ 8 domain models with relationships
✅ Database created with migrations
✅ Identity authentication configured
✅ Cost-optimized API strategy (95% savings)
✅ Comprehensive documentation (3 detailed guides)

**Next Milestone**: Implement map-based search and seed initial café data!

Good luck! 🚀🎲☕
