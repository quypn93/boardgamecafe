# Board Game Café Finder 🎲☕

A modern web application for discovering board game cafés with interactive map search, reviews, and event booking.

![.NET Core](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-green)
![Status](https://img.shields.io/badge/status-MVP-yellow)

## 🚀 Quick Start - Test Now Without API Key!

Don't have a Google Maps API key? **No problem!** Test everything right now:

```bash
cd BoardGameCafeFinder
dotnet run
```

Then open: **https://localhost:7xxx/Test/Cafes**

See all 10 sample cafés, test API endpoints, verify distance calculations - everything works without the API key!

📖 **See [START_TESTING.md](START_TESTING.md) for complete testing guide**

## 🌟 Features

### ✅ Implemented (MVP)
- **Interactive Map Search** - Find cafés near you with Google Maps integration
- **Geospatial Search** - Distance-based search with filters
- **Real-time Filters**:
  - Open Now
  - Has Game Library
  - Minimum Rating
  - Search Radius (5-50km)
- **Café Details** - Address, hours, ratings, photos
- **Responsive Design** - Works on desktop, tablet, and mobile
- **Sample Data** - 10 cafés across 7 major US cities

### 🚧 Planned Features
- User reviews and ratings
- Event discovery and booking
- Game inventory at each café
- User authentication
- Café owner dashboard
- Premium café listings
- Mobile app

## 🏗️ Architecture

### Technology Stack
- **Backend**: .NET Core 8 MVC
- **Frontend**: Razor Views, Bootstrap 5, JavaScript
- **Database**: SQL Server / SQL Server Express
- **Map**: Google Maps JavaScript API
- **Cache**: Redis (planned)
- **Authentication**: ASP.NET Core Identity

### Project Structure
```
BoardGameCafeFinder/
├── Controllers/        # MVC Controllers + API
├── Models/
│   ├── Domain/        # Entity models
│   ├── DTOs/          # Data transfer objects
│   └── ViewModels/    # View models
├── Views/             # Razor views
├── Services/          # Business logic
├── Data/              # Database context
├── wwwroot/           # Static files (CSS, JS, images)
└── Migrations/        # EF Core migrations
```

## 🚀 Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
- [SQL Server](https://www.microsoft.com/sql-server) or SQL Server Express
- [Google Maps API Key](https://developers.google.com/maps/documentation/javascript/get-api-key)

### Installation

1. **Clone the repository**
```bash
git clone https://github.com/yourusername/BoardGameCafeFinder.git
cd BoardGameCafeFinder
```

2. **Update Database Connection**

Edit `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=BoardGameCafeFinderDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

3. **Add Google Maps API Key**

Edit `appsettings.json`:
```json
{
  "GooglePlaces": {
    "ApiKey": "YOUR_GOOGLE_MAPS_API_KEY"
  }
}
```

4. **Apply Database Migrations**
```bash
cd BoardGameCafeFinder
dotnet ef database update
```

5. **Run the Application**
```bash
dotnet run
```

6. **Open in Browser**
```
https://localhost:7xxx
```

Navigate to `/Map` to see the interactive map!

## 📖 Documentation

Comprehensive documentation is available in the following files:

- **[PROJECT_PLANNING.md](PROJECT_PLANNING.md)** - Complete project planning and architecture (73 pages)
- **[MAP_FEATURE_COMPLETE.md](MAP_FEATURE_COMPLETE.md)** - Map feature implementation guide
- **[GOOGLE_PLACES_STRATEGY.md](GOOGLE_PLACES_STRATEGY.md)** - Google Places API cost optimization
- **[API_INTEGRATION_GUIDE.md](API_INTEGRATION_GUIDE.md)** - API integration details with 2026 pricing
- **[COMPETITOR_ANALYSIS.md](COMPETITOR_ANALYSIS.md)** - Market and competitor analysis
- **[PROJECT_SETUP_COMPLETE.md](PROJECT_SETUP_COMPLETE.md)** - Setup completion summary

## 🗺️ Using the Map Feature

### Search for Cafés
1. Click "Find Cafés" in the navigation bar
2. Allow location access or enter a city name
3. Adjust filters (radius, open now, ratings)
4. Click "Search"

### Interact with Results
- Click markers on the map to see café details
- Click cafés in the sidebar list to center map
- Use filters to refine results
- Click "Directions" to get Google Maps directions

## 🎯 Roadmap

### Phase 1: MVP (Weeks 1-10) ✅
- [x] Project setup and database
- [x] Interactive map with search
- [x] Sample data seeding
- [ ] Café detail pages
- [ ] Mobile testing

### Phase 2: Core Features (Weeks 11-20)
- [ ] User authentication
- [ ] Review system
- [ ] Photo uploads
- [ ] Event listings
- [ ] Seed 300+ real cafés

### Phase 3: Monetization (Weeks 21-30)
- [ ] Premium café listings
- [ ] Event booking with payments
- [ ] Café owner dashboard
- [ ] Analytics

### Phase 4: Scale (Months 7-12)
- [ ] Mobile app (React Native)
- [ ] API for third parties
- [ ] International expansion
- [ ] AI recommendations

## 💰 Business Model

### Revenue Streams
1. **Premium Listings** - $50-200/month for cafés
2. **Event Booking Commissions** - 10-15% per booking
3. **Affiliate Links** - Board game sales (Amazon, publishers)
4. **B2B SaaS** - Reservation/waitlist system for cafés

### Market Opportunity
- **Target Market**: 15-20 million hobby gamers in US
- **Global Reach**: 2,000+ board game cafés worldwide
- **Industry Size**: $15B+ global board game market

## 🏆 Competitive Advantages

1. **Interactive Map** - No competitor has this feature
2. **Game Inventory** - See what games are available before visiting
3. **Event Booking** - Book seats for tournaments and game nights
4. **Modern Tech** - Real-time data, mobile-responsive
5. **Community-Driven** - User reviews, crowdsourced data

### vs Competitors
- **boardgamecafenearme.com** - Static HTML, no map, no filters
- **BoardGameGeek** - Focuses on games, not venues
- **Board Game Wikia** - Game database, not café finder

## 🧪 Testing

### Manual Testing
```bash
# Run tests (when implemented)
dotnet test

# Run with watch mode
dotnet watch run
```

### Test Scenarios
1. Search Seattle → Should show 2 cafés
2. Use "Open Now" filter → Shows only open cafés
3. Click marker → Info window opens
4. Mobile view → Responsive layout

## 📊 Sample Data

The application includes 10 sample cafés across 7 cities:
- **Seattle**: Mox Boarding House, Raygun Lounge
- **Portland**: Ground Kontrol, Guardian Games
- **Chicago**: Dice Dojo, Gamers' Lodge
- **New York**: Brooklyn Strategist
- **San Francisco**: Game Parlour
- **Los Angeles**: Game Haus Café
- **Austin**: Vigilante Gaming

Sample data is automatically seeded on first run (Development environment only).

## 🔧 Configuration

### Environment Variables
Create `appsettings.Development.json` for local development:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Your local DB connection"
  },
  "GooglePlaces": {
    "ApiKey": "Your development API key"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### Google Maps API Setup
1. Create a project in [Google Cloud Console](https://console.cloud.google.com/)
2. Enable **Maps JavaScript API** and **Places API**
3. Create an API key
4. Restrict key to your domain/localhost
5. Add to `appsettings.json`

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Code Style
- Follow C# coding conventions
- Use meaningful variable names
- Add comments for complex logic
- Write unit tests for new features

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with [.NET Core 8](https://dotnet.microsoft.com/)
- Maps by [Google Maps Platform](https://developers.google.com/maps)
- Icons by [Bootstrap Icons](https://icons.getbootstrap.com/)
- Inspired by the board game community

## 📞 Contact & Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/BoardGameCafeFinder/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/BoardGameCafeFinder/discussions)
- **Email**: your.email@example.com

## 🎉 Status

**Current Status**: MVP Complete - Interactive Map Feature ✅

**Last Updated**: January 2026

**Next Milestone**: Implement café detail pages and review system

---

**Made with ❤️ for the board game community**

*Find. Play. Connect.* 🎲
