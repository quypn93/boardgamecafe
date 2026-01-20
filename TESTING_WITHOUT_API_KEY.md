# Testing Without Google Maps API Key 🧪

Don't have a Google Maps API key yet? No problem! You can still test most features.

## ✅ What You Can Test

### 1. Backend Functionality
- ✅ Database connection
- ✅ Sample data seeding
- ✅ Geospatial search algorithm
- ✅ Distance calculations (Haversine formula)
- ✅ API endpoints
- ✅ Service layer logic
- ✅ Filters (OpenNow, HasGames, MinRating)

### 2. UI Components
- ✅ Navigation
- ✅ Layout
- ✅ Responsive design
- ✅ Card displays
- ✅ Badges and icons

### 3. API Testing
- ✅ POST /api/cafes/search
- ✅ GET /api/cafes/{id}
- ✅ GET /api/cities

## 🚀 How to Test

### Step 1: Run the Application

```bash
cd f:\QuyPham\BoardGameCFFinder\BoardGameCafeFinder
dotnet run
```

### Step 2: Access Test Pages

Open your browser and go to:

#### Main Test Page - All Cafés List
```
https://localhost:7xxx/Test/Cafes
```
**What it shows:**
- All 10 sample cafés in a card grid
- No map required!
- Statistics (total, cities, premium, verified)
- Each café shows: name, address, rating, distance, status
- Proves backend is working

#### API Testing Interface
```
https://localhost:7xxx/Test/Api
```
**What you can test:**
- Click buttons to test each API endpoint
- See real JSON responses
- Test custom search parameters
- Verify API is working correctly

#### Distance Calculator
```
https://localhost:7xxx/Test/Distance
```
**What it shows:**
- Distance between major cities
- JSON response with calculations
- Proves Haversine formula is working

#### Database Inspector
```
https://localhost:7xxx/Test/Database
```
**What it shows:**
- Total cafés in database
- All cities
- Complete café data as JSON

---

## 🧪 Test Scenarios

### Scenario 1: Verify Sample Data

1. Go to `/Test/Cafes`
2. **Expected**: See 10 cafés displayed
3. **Check**:
   - Seattle: 2 cafés (Mox Boarding House, Raygun Lounge)
   - Portland: 2 cafés (Ground Kontrol, Guardian Games)
   - Chicago: 2 cafés (Dice Dojo, Gamers' Lodge)
   - New York: 1 café (Brooklyn Strategist)
   - San Francisco: 1 café (Game Parlour)
   - Los Angeles: 1 café (Game Haus Café)
   - Austin: 1 café (Vigilante Gaming)

### Scenario 2: Test Search API

1. Go to `/Test/Api`
2. Click "Test This Endpoint" under POST /api/cafes/search
3. **Expected**: JSON response with 10 cafés
4. **Check**:
   - `"success": true`
   - `"count": 10`
   - `"data": [...]` array with cafés

### Scenario 3: Test Geospatial Search

1. Go to `/Test/Api`
2. Scroll to "Custom Search"
3. Enter Seattle coordinates:
   - Latitude: `47.6062`
   - Longitude: `--122.3321`
   - Radius: `10000` (10km)
4. Click "Custom Search"
5. **Expected**: See cafés near Seattle with distance

### Scenario 4: Test Distance Calculations

1. Go to `/Test/Distance`
2. **Expected**: JSON with distances between cities
3. **Verify accuracy**:
   - Seattle to Portland: ~233 km
   - Seattle to Chicago: ~2,787 km
   - New York to Los Angeles: ~3,936 km

### Scenario 5: Test Individual Café

1. Go to `/Test/Api`
2. Enter café ID: `1`
3. Click "Test This Endpoint" under GET /api/cafes/{id}
4. **Expected**: Detailed JSON for Café ID 1
5. **Check fields**:
   - name, description, address
   - latitude, longitude
   - averageRating, totalReviews
   - photos, openingHours

---

## 📊 What Each Test Proves

| Test | What It Proves | Status |
|------|----------------|--------|
| `/Test/Cafes` | Database seeded, UI works | ✅ Ready |
| `/Test/Api` | API endpoints functional | ✅ Ready |
| `/Test/Distance` | Haversine formula correct | ✅ Ready |
| `/Test/Database` | Database connection works | ✅ Ready |
| API Search | Geospatial search works | ✅ Ready |
| API GetCafe | Entity relationships work | ✅ Ready |

---

## 🐛 Troubleshooting

### Problem: No cafés shown on /Test/Cafes

**Solution:**
1. Check console logs for "Seeding sample café data"
2. Restart application (sample data seeds on startup in Development mode)
3. Check database: `/Test/Database`

### Problem: API returns 0 cafés

**Possible causes:**
1. Sample data not seeded
2. Radius too small (try 50,000m = 50km)
3. Wrong coordinates

**Solution:**
```
POST /api/cafes/search
{
  "latitude": 47.6062,
  "longitude": -122.3321,
  "radius": 50000,
  "openNow": false,
  "hasGames": false,
  "limit": 50
}
```

### Problem: 404 errors on /Test routes

**Solution:**
Build and restart:
```bash
dotnet build
dotnet run
```

---

## 🎯 What You'll See

### /Test/Cafes Page:
```
📍 All Sample Cafés (No Map Required)

Statistics:
• Total Cafés: 10
• Cities: 7
• Premium: 3
• Verified: 9
• With Games: 0 (not seeded yet)

[Card Grid with all cafés]
```

### Each Café Card Shows:
- Name & Featured badge (if premium)
- Address & City, State
- Rating & review count
- Open Now / Closed status
- Verified badge
- Distance from Seattle
- Phone number
- Coordinates

### /Test/Api Page:
```
🔌 API Endpoint Testing

[Button] Test Search API
[Button] Test Get Café
[Button] Test Get Cities
[Custom Search Form]

Results appear below buttons as JSON
```

---

## ✨ Advanced Testing

### Test Filters

Go to `/Test/Api` → Custom Search and try:

**1. Filter by Distance:**
- Radius: 5000 (5km from Seattle)
- **Expected**: Only Seattle cafés

**2. Filter by Status:**
- Check "Open Now"
- **Expected**: Cafés that are currently open

**3. Different Cities:**
```javascript
// Chicago
Latitude: 41.8781
Longitude: -87.6298
Radius: 50000

// Portland
Latitude: 45.5152
Longitude: -122.6784
Radius: 50000
```

### Test Edge Cases

**1. Invalid Coordinates:**
- Latitude: 99 (invalid)
- **Expected**: 400 Bad Request

**2. Huge Radius:**
- Radius: 100000 (100km)
- **Expected**: All cafés in nearby states

**3. Zero Radius:**
- Radius: 100 (100m)
- **Expected**: Likely 0 results (unless standing at a café!)

---

## 📈 Performance Testing

### Test Response Times

1. Go to `/Test/Api`
2. Open browser DevTools (F12) → Network tab
3. Click "Test Search API"
4. **Check timing**:
   - Should be < 500ms for 10 cafés
   - Should be < 1000ms for 50 cafés

### Test Different Queries

```javascript
// Small radius (fast)
{ radius: 5000 }  // ~2-3 cafés, <100ms

// Medium radius (medium)
{ radius: 25000 } // ~5-7 cafés, <300ms

// Large radius (all data)
{ radius: 50000 } // All 10 cafés, <500ms
```

---

## 🔄 When You Get API Key

Once you have Google Maps API key:

1. Add to `appsettings.json`:
```json
{
  "GooglePlaces": {
    "ApiKey": "YOUR_ACTUAL_KEY_HERE"
  }
}
```

2. Test the map:
```
https://localhost:7xxx/Map
```

3. Keep test pages:
- `/Test/*` pages still useful for debugging
- Can compare map results with test results
- Good for troubleshooting

---

## 💡 Pro Tips

### 1. Use Test Pages for Development
- Faster than loading map
- See exact JSON responses
- No API quota used

### 2. Check Console Logs
```bash
dotnet run
```
Look for:
- `Seeding sample café data...`
- `Successfully seeded 10 sample cafés`
- API request logs

### 3. Use Browser DevTools
- F12 → Network tab to see API calls
- Console tab to see JavaScript errors
- Application tab to check storage

### 4. Export Test Data
- Click `/Test/Database`
- Copy JSON response
- Save for documentation/testing

---

## 📝 Test Checklist

Before getting API key, verify:

- [ ] `/Test/Cafes` shows 10 cafés
- [ ] API search returns cafés
- [ ] Distance calculations look correct
- [ ] All 7 cities represented
- [ ] Premium/Verified badges show
- [ ] No console errors
- [ ] Responsive on mobile (resize browser)
- [ ] All navigation links work

Once checklist complete → Ready for API key!

---

## 🆘 Still Having Issues?

### Quick Fixes:

```bash
# Clean and rebuild
dotnet clean
dotnet build
dotnet run

# Check database
# Go to: /Test/Database

# Check logs
# Look for errors in console
```

### Common Issues:

1. **"Database already contains café data"**
   - ✅ This is GOOD! Data already seeded
   - Go to `/Test/Cafes` to see it

2. **404 on /Test/Cafes**
   - Build project: `dotnet build`
   - Restart: `dotnet run`

3. **Empty JSON responses**
   - Check `/Test/Database`
   - If empty, restart app (seeding runs on startup)

---

## 🎉 Success Criteria

You'll know everything is working when:

✅ `/Test/Cafes` displays 10 cafés beautifully
✅ `/Test/Api` shows successful JSON responses
✅ `/Test/Distance` calculates distances correctly
✅ `/Test/Database` shows all café data
✅ No errors in browser console
✅ No errors in terminal console
✅ API responses < 500ms

**If all ✅ above → Backend is PERFECT! Just need API key for map!**

---

## 🚀 Next Steps

After testing:

1. **Get Google Maps API Key** → See [QUICK_START.md](QUICK_START.md)
2. **Test Map Feature** → `/Map` with real API key
3. **Add More Sample Data** → Edit `Data/SampleDataSeeder.cs`
4. **Implement Café Details** → Next phase
5. **Deploy to Production** → When ready

---

**Remember**: These test pages are valuable even after you have an API key. Keep them for debugging and development! 🧪✨
