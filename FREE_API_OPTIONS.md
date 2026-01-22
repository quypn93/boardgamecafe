# Các API Miễn Phí Thay Thế Google Places 🆓

**Cập nhật**: Tháng 1/2026

Dưới đây là danh sách các API hoàn toàn miễn phí hoặc có free tier hào phóng để thay thế Google Places API.

---

## 🎯 Tóm Tắt Nhanh

| API | Free Tier | Không Cần Thẻ Tín Dụng | Tốt Nhất Cho |
|-----|-----------|------------------------|--------------|
| **OpenStreetMap Overpass** | ✅ Unlimited* | ✅ | Tìm cafés, restaurants |
| **Nominatim** | ✅ Unlimited* | ✅ | Geocoding (địa chỉ → tọa độ) |
| **Geocodio** | 2,500/ngày | ✅ | Geocoding US/Canada |
| **OpenCage** | 2,500/ngày | ✅ | Geocoding toàn cầu |
| **LocationIQ** | 5,000/ngày | ✅ | Geocoding + Places |
| **Foursquare** | 10,000 calls + $200/tháng credit | ❌ | Places database (100M+ địa điểm) |
| **Mapbox** | 100,000/tháng | ❌ | Bản đồ + geocoding |

*Có giới hạn rate limit, không dùng cho commercial high-volume

---

## 🏆 Khuyến Nghị Cho Board Game Café Finder

### Chiến Lược Tối Ưu (100% MIỄN PHÍ):

```
1. OpenStreetMap Overpass API → Tìm board game cafés
2. Nominatim → Geocoding (địa chỉ → tọa độ)
3. Leaflet.js → Hiển thị bản đồ (thay Google Maps)
```

**Chi phí**: $0/tháng ✅
**Yêu cầu thẻ tín dụng**: Không ❌
**Rate limit**: Đủ cho MVP và development

---

## 📋 Chi Tiết Từng API

### 1. OpenStreetMap Overpass API ⭐ (KHUYẾN NGHỊ)

**Mô tả**: API mạnh mẽ để query địa điểm từ dữ liệu OpenStreetMap

**Free Tier**:
- ✅ Hoàn toàn miễn phí
- ✅ Không cần API key
- ✅ Không cần đăng ký
- ⚠️ Rate limit: Đừng spam requests

**Cách Dùng Cho Board Game Cafe**:
```javascript
// Query tìm board game cafés ở Seattle
const query = `
[out:json];
(
  node["amenity"="cafe"]["name"~"game|board",i](47.5,-122.4,47.7,-122.2);
  way["amenity"="cafe"]["name"~"game|board",i](47.5,-122.4,47.7,-122.2);
);
out body;
`;

const url = `https://overpass-api.de/api/interpreter?data=${encodeURIComponent(query)}`;
const response = await fetch(url);
const data = await response.json();
```

**Dữ liệu trả về**:
- Tên café
- Địa chỉ
- Tọa độ (latitude, longitude)
- Giờ mở cửa (nếu có)
- Số điện thoại (nếu có)
- Website (nếu có)

**Ưu điểm**:
- ✅ 100% miễn phí
- ✅ Dữ liệu crowdsourced (community-driven)
- ✅ Cập nhật liên tục
- ✅ Không cần API key

**Nhược điểm**:
- ❌ Dữ liệu không đầy đủ bằng Google Places
- ❌ Phải tự filter kết quả
- ❌ Không có reviews/ratings

**Tài liệu**:
- [Overpass API Wiki](https://wiki.openstreetmap.org/wiki/Overpass_API)
- [Overpass API Examples](https://wiki.openstreetmap.org/wiki/Overpass_API/Overpass_API_by_Example)
- [Overpass Turbo (Testing tool)](https://overpass-turbo.eu/)

---

### 2. Nominatim (OpenStreetMap) ⭐

**Mô tả**: Geocoding API miễn phí từ OpenStreetMap

**Free Tier**:
- ✅ Hoàn toàn miễn phí
- ✅ Không cần API key
- ✅ Không cần đăng ký
- ⚠️ Usage policy: 1 request/giây, phải có User-Agent

**Cách Dùng**:
```csharp
// Geocoding: Địa chỉ → Tọa độ
public async Task<(double lat, double lon)> GeocodeAddress(string address)
{
    var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(address)}&format=json&limit=1";

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "BoardGameCafeFinder/1.0");

    var response = await client.GetStringAsync(url);
    var results = JsonSerializer.Deserialize<List<NominatimResult>>(response);

    if (results?.Any() == true)
    {
        return (double.Parse(results[0].lat), double.Parse(results[0].lon));
    }

    return (0, 0);
}

// Reverse geocoding: Tọa độ → Địa chỉ
public async Task<string> ReverseGeocode(double lat, double lon)
{
    var url = $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}&format=json";

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("User-Agent", "BoardGameCafeFinder/1.0");

    var response = await client.GetStringAsync(url);
    var result = JsonSerializer.Deserialize<NominatimResult>(response);

    return result?.display_name ?? "";
}
```

**Ưu điểm**:
- ✅ Hoàn toàn miễn phí
- ✅ Global coverage
- ✅ Reverse geocoding
- ✅ Không cần API key

**Nhược điểm**:
- ❌ Rate limit: 1 request/giây
- ❌ Không dùng cho autocomplete
- ❌ Không phù hợp cho high-volume

**Usage Policy**:
- Phải có User-Agent header
- Max 1 request/giây
- Không dùng cho autocomplete search
- Không bulk download

**Tài liệu**:
- [Nominatim API](https://nominatim.org/)
- [Usage Policy](https://operations.osmfoundation.org/policies/nominatim/)

---

### 3. Geocodio ⭐

**Mô tả**: Geocoding API cho US & Canada

**Free Tier**:
- ✅ 2,500 requests/ngày
- ✅ **KHÔNG cần thẻ tín dụng**
- ✅ Tất cả features (spreadsheet upload, API, data appends)
- ✅ Dùng commercial được

**Cách Dùng**:
```csharp
// Cần đăng ký account để lấy API key (miễn phí, không cần thẻ)
public async Task<(double lat, double lon)> GeocodeWithGeocodio(string address)
{
    var apiKey = "YOUR_FREE_API_KEY"; // Lấy từ https://www.geocod.io
    var url = $"https://api.geocod.io/v1.7/geocode?q={Uri.EscapeDataString(address)}&api_key={apiKey}";

    using var client = new HttpClient();
    var response = await client.GetStringAsync(url);
    var result = JsonSerializer.Deserialize<GeocodioResult>(response);

    return (result.results[0].location.lat, result.results[0].location.lng);
}
```

**Ưu điểm**:
- ✅ Không cần thẻ tín dụng
- ✅ 2,500/ngày đủ cho development
- ✅ Commercial use allowed
- ✅ Batch geocoding

**Nhược điểm**:
- ❌ Chỉ US & Canada
- ❌ Không có places search

**Pricing sau khi hết free tier**:
- $0.50 per 1,000 lookups
- Rất rẻ so với Google

**Website**: [https://www.geocod.io](https://www.geocod.io/free-geocoding/)

---

### 4. OpenCage Geocoding API

**Mô tả**: Geocoding API toàn cầu

**Free Tier**:
- ✅ 2,500 requests/ngày
- ✅ **KHÔNG cần thẻ tín dụng**
- ✅ Global coverage
- ✅ Testing vô thời hạn

**Cách Dùng**:
```csharp
public async Task<(double lat, double lon)> GeocodeWithOpenCage(string address)
{
    var apiKey = "YOUR_FREE_API_KEY"; // Lấy từ https://opencagedata.com
    var url = $"https://api.opencagedata.com/geocode/v1/json?q={Uri.EscapeDataString(address)}&key={apiKey}";

    using var client = new HttpClient();
    var response = await client.GetStringAsync(url);
    var result = JsonSerializer.Deserialize<OpenCageResult>(response);

    return (result.results[0].geometry.lat, result.results[0].geometry.lng);
}
```

**Ưu điểm**:
- ✅ Toàn cầu
- ✅ Không cần thẻ tín dụng
- ✅ Forward + reverse geocoding
- ✅ Annotations (timezone, currency, etc.)

**Nhược điểm**:
- ❌ Không có places search
- ❌ 2,500/ngày có thể không đủ cho production

**Pricing**:
- Free: 2,500/day
- Starter: €50/month (10K/day)

**Website**: [https://opencagedata.com](https://opencagedata.com/)

---

### 5. LocationIQ

**Mô tả**: Geocoding + Maps API

**Free Tier**:
- ✅ 5,000 requests/ngày
- ✅ 2 requests/giây
- ✅ Enterprise-grade APIs

**Cách Dùng**:
```csharp
public async Task<(double lat, double lon)> GeocodeWithLocationIQ(string address)
{
    var apiKey = "YOUR_FREE_API_KEY";
    var url = $"https://us1.locationiq.com/v1/search?key={apiKey}&q={Uri.EscapeDataString(address)}&format=json";

    using var client = new HttpClient();
    var response = await client.GetStringAsync(url);
    var results = JsonSerializer.Deserialize<List<LocationIQResult>>(response);

    return (double.Parse(results[0].lat), double.Parse(results[0].lon));
}
```

**Ưu điểm**:
- ✅ 5,000/ngày (cao hơn các đối thủ)
- ✅ Maps API included
- ✅ Autocomplete

**Nhược điểm**:
- ❌ Cần đăng ký account

**Website**: [https://locationiq.com](https://locationiq.com)

---

### 6. Foursquare Places API

**Mô tả**: Database 100 triệu địa điểm toàn cầu

**Free Tier**:
- ✅ 10,000 free calls
- ✅ $200/tháng credit
- ❌ **Cần thẻ tín dụng**

**Cách Dùng**:
```csharp
public async Task<List<Cafe>> SearchCafesWithFoursquare(double lat, double lon, int radius)
{
    var apiKey = "YOUR_API_KEY";
    var url = $"https://api.foursquare.com/v3/places/search?ll={lat},{lon}&radius={radius}&categories=13032"; // 13032 = Board Game Cafe

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Authorization", apiKey);

    var response = await client.GetStringAsync(url);
    var result = JsonSerializer.Deserialize<FoursquareResult>(response);

    return result.results;
}
```

**Ưu điểm**:
- ✅ Database lớn (100M+ places)
- ✅ 1,500+ categories
- ✅ Reviews, photos, tips
- ✅ Real business data

**Nhược điểm**:
- ❌ Cần thẻ tín dụng
- ❌ Phức tạp hơn OSM

**Website**: [https://foursquare.com/products/places-api/](https://foursquare.com/products/places-api/)

---

### 7. Mapbox

**Mô tả**: Maps + Geocoding + Places

**Free Tier**:
- ✅ 100,000 requests/tháng
- ❌ **Cần thẻ tín dụng**
- ✅ $0.75 per 1,000 sau đó

**Ưu điểm**:
- ✅ Beautiful maps
- ✅ 100K/tháng free
- ✅ Geocoding + places

**Nhược điểm**:
- ❌ Cần thẻ tín dụng

**Website**: [https://www.mapbox.com](https://www.mapbox.com)

---

## 🗺️ Maps Display (Thay Google Maps)

### Leaflet.js ⭐ (KHUYẾN NGHỊ)

**Mô tả**: Thư viện JavaScript mã nguồn mở để hiển thị bản đồ

**Free Tier**:
- ✅ Hoàn toàn miễn phí
- ✅ Open source
- ✅ Không cần API key
- ✅ Mobile-friendly

**Cách Dùng**:
```html
<!-- Add to your layout -->
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>

<div id="map" style="height: 500px;"></div>

<script>
// Initialize map
const map = L.map('map').setView([47.6062, -122.3321], 13);

// Add tile layer (free from OpenStreetMap)
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap contributors'
}).addTo(map);

// Add marker
L.marker([47.6062, -122.3321])
    .addTo(map)
    .bindPopup('Mox Boarding House')
    .openPopup();
</script>
```

**Ưu điểm**:
- ✅ 100% miễn phí
- ✅ Lightweight (39kb)
- ✅ Nhiều plugins
- ✅ Mobile-optimized

**Nhược điểm**:
- ❌ Không đẹp bằng Google Maps
- ❌ Ít features hơn

**Website**: [https://leafletjs.com](https://leafletjs.com)

---

## 🎯 Chiến Lược Cho Project Của Bạn

### Option 1: 100% MIỄN PHÍ (Khuyến nghị cho MVP)

**Stack**:
```
- Leaflet.js: Hiển thị bản đồ
- OpenStreetMap Overpass API: Tìm cafés
- Nominatim: Geocoding
- Your own database: Lưu dữ liệu đã crawl
```

**Chi phí**: $0/tháng
**Effort**: Trung bình (phải integrate 3 APIs)
**Data quality**: Tốt (crowdsourced)

**Implementation**:
1. Thay Google Maps bằng Leaflet.js trong Views/Map/Index.cshtml
2. Tạo service để crawl data từ Overpass API
3. Lưu vào database của bạn
4. Dùng Nominatim cho geocoding khi cần

---

### Option 2: HYBRID (Tốt nhất cho Production)

**Stack**:
```
- Leaflet.js: Bản đồ miễn phí
- Foursquare API: Tìm cafés (10K calls + $200 credit/tháng)
- OpenCage: Geocoding (2,500/day free)
- Your database: Cache results
```

**Chi phí**: ~$0-10/tháng (dưới free tier)
**Effort**: Thấp (APIs có sẵn)
**Data quality**: Tuyệt vời (Foursquare có 100M+ places)

---

### Option 3: COMMUNITY-DRIVEN (Tốt nhất cho Long-term)

**Stack**:
```
- Leaflet.js: Bản đồ
- User submissions: Người dùng tự thêm cafés
- Nominatim: Geocoding
- Your database: Source of truth
```

**Chi phí**: $0/tháng
**Effort**: Cao (phải build submission system)
**Data quality**: Phụ thuộc community (như Wikipedia)

**Benefits**:
- ✅ Không phụ thuộc external APIs
- ✅ Community engagement
- ✅ Unique data (board game inventory từ café owners)

---

## 📊 So Sánh Chi Tiết

### Geocoding APIs

| API | Free Tier | Rate Limit | Coverage | Card Required | Best For |
|-----|-----------|------------|----------|---------------|----------|
| Nominatim | Unlimited | 1/sec | Global | ❌ | Low-volume, testing |
| Geocodio | 2,500/day | Generous | US/Canada | ❌ | US/Canada only |
| OpenCage | 2,500/day | 1/sec | Global | ❌ | Global, testing |
| LocationIQ | 5,000/day | 2/sec | Global | ✅ | Medium volume |
| Google | $200 credit | High | Global | ✅ | Production (expensive) |

### Places Search APIs

| API | Free Tier | Database Size | Reviews | Card Required | Best For |
|-----|-----------|---------------|---------|---------------|----------|
| Overpass API | Unlimited* | Huge (OSM) | ❌ | ❌ | Free solution |
| Foursquare | 10K + $200 | 100M+ | ✅ | ✅ | Quality data |
| Google Places | $200 credit | Huge | ✅ | ✅ | Best quality (expensive) |

*Subject to rate limiting

### Map Display

| Solution | Cost | API Key | Features | Best For |
|----------|------|---------|----------|----------|
| Leaflet + OSM | $0 | ❌ | Basic | Free solution |
| Mapbox | 100K/mo | ✅ | Advanced | Beautiful maps |
| Google Maps | $200 credit | ✅ | Best | Familiar UX |

---

## 💡 Khuyến Nghị Cuối Cùng

### Cho MVP (Hiện tại):

```
✅ Dùng Leaflet.js + OpenStreetMap tiles (free)
✅ Dùng Nominatim cho geocoding (free)
✅ Manual data entry cho 100-300 cafés đầu tiên
✅ Overpass API để tìm thêm cafés (free)
```

**Chi phí**: $0/tháng
**Time to implement**: 2-3 ngày

### Khi Có Users (Sau 3-6 tháng):

```
✅ Giữ Leaflet.js (free)
✅ Upgrade lên Foursquare API ($200 credit/tháng)
✅ Dùng OpenCage cho geocoding (2,500/day free)
✅ User submissions cho unique data
```

**Chi phí**: $0-20/tháng
**Data quality**: Tuyệt vời

### Khi Scale (1 năm+):

```
✅ Xem xét Google Maps nếu revenue đủ
✅ Hoặc giữ Leaflet + tự build data
✅ Community-driven content
```

---

## 📚 Tài Liệu & Resources

### Geocoding:
- [Nominatim](https://nominatim.org/)
- [Geocodio](https://www.geocod.io/free-geocoding/)
- [OpenCage](https://opencagedata.com/)
- [LocationIQ](https://locationiq.com)

### Places Search:
- [Overpass API](https://wiki.openstreetmap.org/wiki/Overpass_API)
- [Overpass Turbo (Testing)](https://overpass-turbo.eu/)
- [Foursquare Places API](https://foursquare.com/products/places-api/)

### Maps:
- [Leaflet.js](https://leafletjs.com)
- [OpenLayers](https://openlayers.org/)
- [Mapbox](https://www.mapbox.com)

### Tutorials:
- [OpenStreetMap with Python](https://janakiev.com/blog/openstreetmap-with-python-and-overpass-api/)
- [Leaflet Quick Start](https://leafletjs.com/examples/quick-start/)

---

## 🚀 Next Steps

1. **Test Overpass API** với board game cafés ở Seattle
2. **Thay Google Maps bằng Leaflet.js** trong project
3. **Implement Nominatim** cho geocoding
4. **Build data seeder** từ Overpass API
5. **Test thoroughly** với free tier

Tất cả đều miễn phí và không cần thẻ tín dụng! 🎉

---

**Sources:**
- [Google Places API Alternatives](https://www.safegraph.com/guides/google-places-api-alternatives)
- [Free API Maps Alternatives](https://blog.hubspot.com/website/free-api-maps)
- [Nominatim Usage Policy](https://operations.osmfoundation.org/policies/nominatim/)
- [Overpass API Wiki](https://wiki.openstreetmap.org/wiki/Overpass_API)
- [Geocodio Free Tier](https://www.geocod.io/free-geocoding/)
- [OpenCage Pricing](https://opencagedata.com/pricing)
- [LocationIQ](https://locationiq.com)
- [Foursquare Places API](https://foursquare.com/products/places-api/)
