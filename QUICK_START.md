# Quick Start Guide 🚀

Get Board Game Café Finder running in 5 minutes!

## Prerequisites Check

- [ ] .NET 8.0 SDK installed
- [ ] SQL Server or SQL Server Express running
- [ ] Google Maps API key (get one free at [Google Cloud Console](https://console.cloud.google.com/))

## Step-by-Step Setup

### 1. Configure Database (1 minute)

Open `appsettings.json` and update the connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER_NAME;Database=BoardGameCafeFinderDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

**Common server names**:
- Local SQL Server: `localhost` or `(localdb)\\mssqllocaldb`
- SQL Server Express: `localhost\\SQLEXPRESS` or `.\\SQLEXPRESS`
- Named instance: `YOUR_PC_NAME\\SQLEXPRESS01`

### 2. Add Google Maps API Key (1 minute)

In the same `appsettings.json` file:

```json
{
  "GooglePlaces": {
    "ApiKey": "YOUR_GOOGLE_MAPS_API_KEY_HERE"
  }
}
```

**Don't have an API key?**
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project
3. Enable "Maps JavaScript API" and "Places API"
4. Create credentials → API key
5. Copy the key

> **Note**: Free tier includes $200/month credit (enough for development)

### 3. Create Database (1 minute)

Open terminal in the project folder:

```bash
cd f:\QuyPham\BoardGameCFFinder\BoardGameCafeFinder
dotnet ef database update
```

This will:
- Create the database
- Apply all migrations
- Set up tables

### 4. Run the Application (30 seconds)

```bash
dotnet run
```

Wait for:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7xxx
```

### 5. Open in Browser (30 seconds)

Open your browser and go to:
```
https://localhost:7xxx/Map
```

Or click the URL shown in the terminal while holding Ctrl.

## ✅ Success Checklist

You should see:
- [ ] Interactive Google Map
- [ ] Sidebar with search controls
- [ ] "Use My Location" button
- [ ] Sample cafés loaded (if you allow location access)

## 🎯 First Actions

### Try These Features:

1. **Use Your Location**
   - Click "Use My Location" button
   - Allow location access
   - Map centers on you
   - Click "Search" to find nearby cafés

2. **Search a City**
   - Type "Seattle" in location input
   - Select from dropdown
   - Click "Search"
   - See 2 sample cafés appear

3. **Apply Filters**
   - Check "Open Now"
   - Change radius to 25km
   - See results update

4. **Click a Marker**
   - Click any café marker on map
   - Info window opens
   - Try "Directions" link

## 🐛 Troubleshooting

### Problem: Database connection failed

**Solution**: Check your connection string
```bash
# Test SQL Server connection
sqlcmd -S YOUR_SERVER_NAME -E
```

If that fails, update `Server=` in connection string.

### Problem: Map doesn't load

**Possible causes**:
1. **No API key**: Check `appsettings.json`
2. **Invalid API key**: Verify in Google Cloud Console
3. **API not enabled**: Enable "Maps JavaScript API" and "Places API"

**Check browser console** (F12) for errors.

### Problem: No cafés shown

**Solutions**:
1. **Sample data not seeded**: Check logs for "Seeding sample café data"
2. **Wrong location**: Try searching "Seattle" or "Portland"
3. **Radius too small**: Increase to 50km
4. **Database empty**: Run `dotnet ef database update` again

### Problem: Build errors

```bash
# Clean and rebuild
dotnet clean
dotnet build
```

### Problem: Port already in use

Stop any running instances:
- Press Ctrl+C in terminal
- Or kill process in Task Manager

## 📱 Test on Mobile

1. Find your local IP: `ipconfig` (Windows) or `ifconfig` (Mac/Linux)
2. Update `launchSettings.json` to allow external connections
3. Access from phone: `https://YOUR_IP:7xxx/Map`

## 🎓 Next Steps

### Learn More:
- **Full documentation**: [PROJECT_PLANNING.md](PROJECT_PLANNING.md)
- **Map feature details**: [MAP_FEATURE_COMPLETE.md](MAP_FEATURE_COMPLETE.md)
- **API optimization**: [GOOGLE_PLACES_STRATEGY.md](GOOGLE_PLACES_STRATEGY.md)

### Explore the Code:
- `Controllers/MapController.cs` - Map page controller
- `Services/CafeService.cs` - Geospatial search logic
- `wwwroot/js/map.js` - Interactive map JavaScript
- `Views/Map/Index.cshtml` - Map UI

### Add More Data:
- Check `Data/SampleDataSeeder.cs`
- Add your own sample cafés
- Or implement Google Places API seeding (see GOOGLE_PLACES_STRATEGY.md)

## 💡 Pro Tips

1. **Hot Reload**: Use `dotnet watch run` for auto-reload on code changes
2. **Debug Mode**: Set breakpoints in Visual Studio or VS Code
3. **Logs**: Check console output for helpful debug info
4. **API Costs**: Use aggressive caching to minimize Google API costs (see strategy doc)
5. **Sample Data**: Auto-seeded in Development environment only

## 🆘 Need Help?

- **Issues**: Check [GitHub Issues](https://github.com/yourusername/BoardGameCafeFinder/issues)
- **Docs**: Read comprehensive docs in project root
- **Community**: Join discussions

## ✨ Quick Demo Scenarios

### Scenario 1: Find Cafe in Seattle
1. Go to https://localhost:7xxx/Map?city=seattle
2. Map centers on Seattle
3. Shows 2 cafés: Mox Boarding House, Raygun Lounge
4. Click markers to see details

### Scenario 2: Filter by Rating
1. Search any city
2. Select "4.5+ ⭐" from Minimum Rating
3. Only highly-rated cafés shown

### Scenario 3: Check Opening Hours
1. Search cafés
2. Enable "Open Now" filter
3. See which cafés are currently open
4. Badge shows "Open Now" or "Closed"

---

**Ready to start?** Follow steps 1-5 above and you'll be running in 5 minutes! 🚀

Questions? Check the [README.md](README.md) or docs folder.
