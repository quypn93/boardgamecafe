# Overpass API Queries - Board Game Cafés Seattle 🎲

## 🧪 Test Queries for Overpass Turbo

Mở [https://overpass-turbo.eu/](https://overpass-turbo.eu/) và paste các queries này:

---

## Query 1: Tìm Cafés Có "Game" Trong Tên (Broad Search) ⭐

**Mục đích**: Tìm bất kỳ địa điểm nào có "game" trong tên ở Seattle area

```
[out:json][timeout:25];
// Seattle bounding box
(
  // Nodes (points)
  node["name"~"game",i](47.5,-122.4,47.7,-122.2);
  // Ways (buildings)
  way["name"~"game",i](47.5,-122.4,47.7,-122.2);
  // Relations
  relation["name"~"game",i](47.5,-122.4,47.7,-122.2);
);
out body;
>;
out skel qt;
```

**Coordinates giải thích**:
- `47.5,-122.4,47.7,-122.2` = Seattle bounding box
- Format: `(min_lat, min_lon, max_lat, max_lon)`

---

## Query 2: Tất Cả Cafés Ở Seattle (General Search) ⭐⭐

**Mục đích**: Lấy TẤT CẢ cafés, sau đó manual filter

```
[out:json][timeout:25];
// Tìm tất cả cafés trong Seattle
(
  node["amenity"="cafe"](47.5,-122.4,47.7,-122.2);
  way["amenity"="cafe"](47.5,-122.4,47.7,-122.2);
);
out body;
>;
out skel qt;
```

**Kết quả mong đợi**: 100-300 cafés → Sau đó search "game" trong tên

---

## Query 3: Cafés + Bars + Pubs (Broad Entertainment) ⭐⭐⭐

**Mục đích**: Nhiều board game venues là bars/pubs, không chỉ cafés

```
[out:json][timeout:25];
(
  // Cafés
  node["amenity"="cafe"](47.5,-122.4,47.7,-122.2);
  way["amenity"="cafe"](47.5,-122.4,47.7,-122.2);

  // Bars
  node["amenity"="bar"](47.5,-122.4,47.7,-122.2);
  way["amenity"="bar"](47.5,-122.4,47.7,-122.2);

  // Pubs
  node["amenity"="pub"](47.5,-122.4,47.7,-122.2);
  way["amenity"="pub"](47.5,-122.4,47.7,-122.2);
);
out body;
>;
out skel qt;
```

---

## Query 4: Entertainment Venues (Rộng Nhất) ⭐⭐⭐

**Mục đích**: Tìm tất cả entertainment venues có thể có board games

```
[out:json][timeout:25];
(
  // Cafés, bars, pubs
  node["amenity"~"cafe|bar|pub"](47.5,-122.4,47.7,-122.2);
  way["amenity"~"cafe|bar|pub"](47.5,-122.4,47.7,-122.2);

  // Restaurants có thể có board games
  node["amenity"="restaurant"]["cuisine"~"^$|casual"](47.5,-122.4,47.7,-122.2);
  way["amenity"="restaurant"]["cuisine"~"^$|casual"](47.5,-122.4,47.7,-122.2);

  // Entertainment venues
  node["leisure"~"adult_gaming_centre|amusement_arcade"](47.5,-122.4,47.7,-122.2);
  way["leisure"~"adult_gaming_centre|amusement_arcade"](47.5,-122.4,47.7,-122.2);
);
out body;
>;
out skel qt;
```

---

## Query 5: Search By Specific Names (Known Cafés)

**Mục đích**: Tìm cafés cụ thể mà bạn biết tên

```
[out:json][timeout:25];
(
  // Mox Boarding House
  node["name"~"Mox|MOX",i](47.5,-122.4,47.7,-122.2);
  way["name"~"Mox|MOX",i](47.5,-122.4,47.7,-122.2);

  // Raygun Lounge
  node["name"~"Raygun",i](47.5,-122.4,47.7,-122.2);
  way["name"~"Raygun",i](47.5,-122.4,47.7,-122.2);

  // Cafe Mox
  node["name"~"Cafe Mox",i](47.5,-122.4,47.7,-122.2);
  way["name"~"Cafe Mox",i](47.5,-122.4,47.7,-122.2);

  // Any place with "board" or "game"
  node["name"~"board|game",i](47.5,-122.4,47.7,-122.2);
  way["name"~"board|game",i](47.5,-122.4,47.7,-122.2);
);
out body;
>;
out skel qt;
```

---

## Query 6: Geographic Search Around Point

**Mục đích**: Tìm trong radius từ một điểm (ví dụ: downtown Seattle)

```
[out:json][timeout:25];
// Center: Downtown Seattle (47.6062, -122.3321)
// Radius: 5000 meters (5km)
(
  node["amenity"="cafe"](around:5000,47.6062,-122.3321);
  way["amenity"="cafe"](around:5000,47.6062,-122.3321);

  node["amenity"="bar"](around:5000,47.6062,-122.3321);
  way["amenity"="bar"](around:5000,47.6062,-122.3321);
);
out body;
>;
out skel qt;
```

---

## Query 7: Filter By Opening Hours (Advanced)

**Mục đích**: Tìm venues có opening hours data

```
[out:json][timeout:25];
(
  node["amenity"~"cafe|bar"]["opening_hours"](47.5,-122.4,47.7,-122.2);
  way["amenity"~"cafe|bar"]["opening_hours"](47.5,-122.4,47.7,-122.2);
);
out body;
>;
out skel qt;
```

---

## Query 8: Multi-City Search (Seattle + Portland)

**Mục đích**: Tìm ở nhiều thành phố cùng lúc

```
[out:json][timeout:25];
(
  // Seattle area
  node["amenity"~"cafe|bar"](47.5,-122.4,47.7,-122.2);
  way["amenity"~"cafe|bar"](47.5,-122.4,47.7,-122.2);

  // Portland area
  node["amenity"~"cafe|bar"](45.4,-122.8,45.6,-122.5);
  way["amenity"~"cafe|bar"](45.4,-122.8,45.6,-122.5);
);
out body;
>;
out skel qt;
```

---

## 🎯 Hướng Dẫn Sử Dụng

### Bước 1: Mở Overpass Turbo
```
https://overpass-turbo.eu/
```

### Bước 2: Paste Query
- Copy một trong các queries trên
- Paste vào editor bên trái
- Click "Run" (hoặc Ctrl+Enter)

### Bước 3: Xem Kết Quả
- **Map view**: Markers hiện trên bản đồ
- **Data tab**: JSON response
- **Export**: Download GeoJSON, GPX, etc.

### Bước 4: Refine Search
- Zoom map vào khu vực khác
- Click ">" icon để convert map view thành query bbox
- Adjust query parameters

---

## 📊 Mã Chú Giải OSM Tags

### Amenity Types:
```
amenity=cafe       → Coffee shops, cafés
amenity=bar        → Bars, taverns
amenity=pub        → Pubs (British style)
amenity=restaurant → Restaurants
```

### Leisure Types:
```
leisure=adult_gaming_centre  → Gaming centres
leisure=amusement_arcade     → Arcades
leisure=gaming_hall          → Gaming halls
```

### Name Matching:
```
["name"~"pattern",i]  → Case-insensitive regex match
["name"="exact"]       → Exact match
["name"~"word1|word2"] → Match word1 OR word2
```

### Geographic Filters:
```
(min_lat, min_lon, max_lat, max_lon)  → Bounding box
(around:radius_meters,lat,lon)        → Radius search
```

---

## 🧪 Test Results - Dự Đoán

### Query 1 (game in name):
**Dự đoán**: 0-5 kết quả
**Lý do**: Ít cafés có "game" trong tên trong OSM

### Query 2 (all cafés):
**Dự đoán**: 100-300 kết quả
**Lý do**: Seattle có nhiều cafés, OSM có data tốt

### Query 3 (cafés + bars):
**Dự đoán**: 200-500 kết quả
**Lý do**: Nhiều venues hơn

### Query 4 (entertainment):
**Dự đoán**: 300-600 kết quả
**Lý do**: Broadest search

---

## 💡 Tips & Tricks

### 1. Use Wizard
- Click "Wizard" button in Overpass Turbo
- Type: "cafe in Seattle"
- Auto-generates query

### 2. Zoom to Area
- Pan map to desired area
- Query will use visible bbox
- Click ">" to update query

### 3. Export Results
- Click "Export" button
- Choose format: GeoJSON, GPX, KML, CSV
- Import vào database của bạn

### 4. Save Queries
- Click "Share" → "Save"
- Get permanent link
- Share với team

### 5. Performance
- Smaller bbox = faster query
- Use timeout for large queries
- Limit results với `out 100;` instead of `out body;`

---

## 🔧 Advanced Techniques

### Technique 1: Post-Filter in Code

Query lấy TẤT CẢ cafés, filter trong C#:

```csharp
public async Task<List<Cafe>> FindBoardGameCafesFromOSM(double lat, double lon, int radiusMeters)
{
    // 1. Query ALL cafés from Overpass
    var query = $@"
    [out:json];
    node[""amenity""=""cafe""](around:{radiusMeters},{lat},{lon});
    out body;
    ";

    var response = await _httpClient.GetStringAsync($"https://overpass-api.de/api/interpreter?data={Uri.EscapeDataString(query)}");
    var data = JsonSerializer.Deserialize<OverpassResponse>(response);

    // 2. Filter for board game related names
    var boardGameKeywords = new[] { "game", "board", "mox", "dice", "strategy", "play" };

    var cafes = data.Elements
        .Where(e => e.Tags.ContainsKey("name"))
        .Where(e => boardGameKeywords.Any(k =>
            e.Tags["name"].Contains(k, StringComparison.OrdinalIgnoreCase)))
        .Select(e => new Cafe
        {
            Name = e.Tags.GetValueOrDefault("name"),
            Latitude = e.Lat,
            Longitude = e.Lon,
            Address = e.Tags.GetValueOrDefault("addr:street"),
            Phone = e.Tags.GetValueOrDefault("phone"),
            Website = e.Tags.GetValueOrDefault("website")
        })
        .ToList();

    return cafes;
}
```

---

### Technique 2: Combine Multiple Sources

```csharp
public async Task<List<Cafe>> FindBoardGameCafes(string city)
{
    var cafes = new List<Cafe>();

    // 1. Check your database first
    var existingCafes = await _cafeService.GetByCityAsync(city);
    cafes.AddRange(existingCafes);

    // 2. Supplement with OSM data
    var osmCafes = await FindBoardGameCafesFromOSM(lat, lon, 50000);

    // 3. Deduplicate (same name + close location)
    var newCafes = osmCafes.Where(osm =>
        !cafes.Any(existing =>
            existing.Name.Equals(osm.Name, StringComparison.OrdinalIgnoreCase) &&
            CalculateDistance(existing.Latitude, existing.Longitude, osm.Latitude, osm.Longitude) < 100 // 100m threshold
        )
    );

    cafes.AddRange(newCafes);

    return cafes;
}
```

---

### Technique 3: Reverse Geocoding for Address

```csharp
public async Task<string> GetAddressFromOSM(double lat, double lon)
{
    var url = $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}&format=json";

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "BoardGameCafeFinder/1.0");

    var response = await client.GetStringAsync(url);
    var result = JsonSerializer.Deserialize<NominatimResult>(response);

    return result?.display_name ?? "";
}
```

---

## 📈 Realistic Expectations

### OSM Coverage for Board Game Cafés:

| City | Total Cafés (Est.) | Board Game Cafés (Real) | OSM Has | Coverage % |
|------|-------------------|------------------------|---------|------------|
| Seattle | 500+ | 5-8 | 0-2 | 0-25% |
| Portland | 400+ | 4-6 | 0-1 | 0-17% |
| Chicago | 1000+ | 8-12 | 0-3 | 0-25% |
| New York | 2000+ | 10-15 | 0-4 | 0-27% |

**Conclusion**: OSM không đủ, cần build database riêng!

---

## 🚀 Action Items

### Sau Khi Test Queries:

1. ✅ **Chạy Query 2** (all cafés) → Thấy OSM có data
2. ✅ **Chạy Query 1** (game cafés) → Thấy empty/minimal
3. ✅ **Realize**: OSM không đủ cho board game cafés
4. ✅ **Pivot**: Focus vào manual entry + community
5. ✅ **Use OSM**: Supplementary cho general café data

### Pivot Strategy:

```
Primary: Manual entry (100-300 cafés)
Secondary: Community submissions
Supplementary: OSM for general venue data
Never: Depend on OSM for board game specific data
```

---

## 🎯 Recommended Query to Start

**Chạy query này để thấy OSM CÓ data (general cafés)**:

```
[out:json][timeout:25];
(
  node["amenity"="cafe"](47.6,-122.4,47.7,-122.2);
  way["amenity"="cafe"](47.6,-122.4,47.7,-122.2);
);
out body;
>;
out skel qt;
```

**Kết quả mong đợi**: 50-150 cafés hiển thị trên map

**Sau đó search**: Ctrl+F "game", "board", "mox", "dice" trong kết quả → Thấy 0-2 matches

**Kết luận**: Cần build database riêng! 💪

---

## 📞 Resources

- [Overpass Turbo](https://overpass-turbo.eu/)
- [Overpass API Wiki](https://wiki.openstreetmap.org/wiki/Overpass_API)
- [OSM Tag Reference](https://wiki.openstreetmap.org/wiki/Map_features)
- [Nominatim](https://nominatim.org/)

---

**Bây giờ hãy thử chạy queries trên Overpass Turbo! 🧪**
