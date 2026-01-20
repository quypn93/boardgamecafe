# Migration Guide: Google Maps → Leaflet.js 🗺️

**Mục tiêu**: Thay thế Google Maps bằng Leaflet.js (100% miễn phí, không cần API key)

**Thời gian**: 30-60 phút

**Chi phí**: $0 ✅

---

## 🎯 Tại Sao Leaflet.js?

- ✅ **Hoàn toàn miễn phí** - Không cần API key
- ✅ **Open source** - MIT License
- ✅ **Lightweight** - Chỉ 39kb
- ✅ **Mobile-friendly** - Touch gestures
- ✅ **Nhiều plugins** - Clustering, heatmaps, routing...
- ✅ **Tiles miễn phí** - OpenStreetMap
- ✅ **Dễ customize** - CSS, markers, popups

---

## 📋 Các Bước Migration

### Bước 1: Update Layout (Thêm Leaflet CSS & JS)

**File**: `Views/Shared/_Layout.cshtml`

Tìm dòng:
```html
@await RenderSectionAsync("Styles", required: false)
```

Thêm trước đó:
```html
<!-- Leaflet CSS -->
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"
      integrity="sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY="
      crossorigin=""/>
```

Tìm dòng:
```html
@await RenderSectionAsync("Scripts", required: false)
```

Thêm trước đó:
```html
<!-- Leaflet JS -->
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"
        integrity="sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo="
        crossorigin=""></script>
```

---

### Bước 2: Update Map View

**File**: `Views/Map/Index.cshtml`

**Thay đổi phần Styles**:

```html
@section Styles {
    <style>
        /* Leaflet map styles */
        #map {
            height: 100vh;
            width: 100%;
        }

        .map-container {
            display: flex;
            height: 100vh;
            overflow: hidden;
        }

        .sidebar-panel {
            width: 400px;
            height: 100vh;
            overflow-y: auto;
            background: white;
            border-right: 1px solid #ddd;
            padding: 20px;
        }

        .map-panel {
            flex: 1;
            height: 100vh;
        }

        /* Custom marker styles */
        .cafe-marker {
            background-color: #4CAF50;
            color: white;
            border-radius: 50%;
            width: 30px;
            height: 30px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            border: 2px solid white;
            box-shadow: 0 2px 5px rgba(0,0,0,0.3);
        }

        .cafe-marker.premium {
            background-color: #FFD700;
            color: #333;
        }

        .cafe-marker.closed {
            background-color: #999;
        }

        /* Leaflet popup custom styles */
        .leaflet-popup-content {
            margin: 15px;
            min-width: 250px;
        }

        .popup-cafe-name {
            font-size: 18px;
            font-weight: bold;
            margin-bottom: 10px;
            color: #333;
        }

        .popup-address {
            color: #666;
            margin-bottom: 8px;
        }

        .popup-rating {
            margin: 8px 0;
        }

        .popup-actions {
            margin-top: 10px;
            padding-top: 10px;
            border-top: 1px solid #eee;
        }

        .popup-actions a {
            margin-right: 10px;
        }

        /* Responsive */
        @media (max-width: 768px) {
            .map-container {
                flex-direction: column;
            }
            .sidebar-panel {
                width: 100%;
                height: 40vh;
                border-right: none;
                border-bottom: 1px solid #ddd;
            }
            .map-panel {
                height: 60vh;
            }
        }
    </style>
}
```

**HTML không thay đổi** (giữ nguyên như cũ):
```html
<div class="map-container">
    <div class="sidebar-panel">
        <!-- Search form giữ nguyên -->
    </div>
    <div class="map-panel">
        <div id="map"></div>
    </div>
</div>
```

---

### Bước 3: Rewrite JavaScript

**File**: `wwwroot/js/map.js`

**HOÀN TOÀN thay thế bằng code mới**:

```javascript
// ========================================
// Leaflet.js Map Implementation
// Free & Open Source - No API Key Required
// ========================================

let map;
let markers = [];
let currentLocation = null;
const markerGroup = L.layerGroup();

// ========================================
// Initialize Map
// ========================================
function initMap() {
    // Create map centered on US
    map = L.map('map').setView([39.8283, -98.5795], 4);

    // Add OpenStreetMap tiles (FREE!)
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(map);

    // Add marker group to map
    markerGroup.addTo(map);

    // Initialize geolocation
    initGeolocation();

    // Load initial cafes
    searchNearby();
}

// ========================================
// Geolocation
// ========================================
function initGeolocation() {
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(
            (position) => {
                currentLocation = {
                    lat: position.coords.latitude,
                    lng: position.coords.longitude
                };

                // Center map on user location
                map.setView([currentLocation.lat, currentLocation.lng], 13);

                // Add "You are here" marker
                L.marker([currentLocation.lat, currentLocation.lng], {
                    icon: L.divIcon({
                        className: 'user-location-marker',
                        html: '<div style="background: #2196F3; width: 15px; height: 15px; border-radius: 50%; border: 3px solid white; box-shadow: 0 0 10px rgba(33,150,243,0.5);"></div>',
                        iconSize: [15, 15]
                    })
                }).addTo(map).bindPopup('You are here');

                // Auto search nearby
                searchNearby();
            },
            (error) => {
                console.log('Geolocation error:', error);
                // Default to Seattle if geolocation fails
                currentLocation = { lat: 47.6062, lng: -122.3321 };
                map.setView([currentLocation.lat, currentLocation.lng], 13);
            }
        );
    }
}

function useMyLocation() {
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(
            (position) => {
                currentLocation = {
                    lat: position.coords.latitude,
                    lng: position.coords.longitude
                };
                map.setView([currentLocation.lat, currentLocation.lng], 13);
                document.getElementById('locationInput').value = `${currentLocation.lat.toFixed(4)}, ${currentLocation.lng.toFixed(4)}`;
                searchNearby();
            },
            (error) => {
                alert('Could not get your location. Please enter a city name.');
            }
        );
    } else {
        alert('Geolocation is not supported by your browser.');
    }
}

// ========================================
// Search Functionality
// ========================================
async function searchNearby() {
    const resultsContainer = document.getElementById('resultsContainer');
    resultsContainer.innerHTML = '<div class="text-center"><div class="spinner-border spinner-border-sm"></div> Searching...</div>';

    if (!currentLocation) {
        resultsContainer.innerHTML = '<div class="alert alert-warning">Please allow location access or enter a city.</div>';
        return;
    }

    const radius = parseInt(document.getElementById('radiusSelect').value);
    const openNow = document.getElementById('openNowFilter').checked;
    const hasGames = document.getElementById('hasGamesFilter')?.checked || false;
    const minRating = parseFloat(document.getElementById('minRatingSelect')?.value || 0);

    try {
        const response = await fetch('/api/cafes/search', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                latitude: currentLocation.lat,
                longitude: currentLocation.lng,
                radius: radius,
                openNow: openNow,
                hasGames: hasGames,
                minRating: minRating > 0 ? minRating : null,
                limit: 50
            })
        });

        const result = await response.json();

        if (result.success) {
            displayResults(result.data);
        } else {
            resultsContainer.innerHTML = `<div class="alert alert-danger">${result.message}</div>`;
        }
    } catch (error) {
        console.error('Search error:', error);
        resultsContainer.innerHTML = '<div class="alert alert-danger">Error searching cafés. Please try again.</div>';
    }
}

// ========================================
// Display Results
// ========================================
function displayResults(cafes) {
    const resultsContainer = document.getElementById('resultsContainer');

    // Clear existing markers
    markerGroup.clearLayers();

    if (cafes.length === 0) {
        resultsContainer.innerHTML = '<div class="alert alert-info">No cafés found. Try increasing the search radius.</div>';
        return;
    }

    // Display count
    resultsContainer.innerHTML = `<h5 class="mb-3">Found ${cafes.length} café${cafes.length !== 1 ? 's' : ''}</h5>`;

    // Add each cafe
    cafes.forEach((cafe, index) => {
        // Add marker to map
        addMarker(cafe);

        // Add to results list
        const cafeCard = createCafeCard(cafe, index);
        resultsContainer.innerHTML += cafeCard;
    });

    // Fit map to show all markers
    if (cafes.length > 0) {
        const bounds = cafes.map(c => [c.latitude, c.longitude]);
        if (currentLocation) {
            bounds.push([currentLocation.lat, currentLocation.lng]);
        }
        map.fitBounds(bounds, { padding: [50, 50] });
    }
}

// ========================================
// Add Marker
// ========================================
function addMarker(cafe) {
    // Determine marker color
    let markerColor = '#4CAF50'; // Default green
    let markerClass = 'cafe-marker';

    if (cafe.isPremium) {
        markerColor = '#FFD700'; // Gold for premium
        markerClass += ' premium';
    } else if (!cafe.isOpenNow) {
        markerColor = '#999'; // Gray for closed
        markerClass += ' closed';
    }

    // Create custom icon
    const customIcon = L.divIcon({
        className: markerClass,
        html: `<div style="background: ${markerColor}; color: ${cafe.isPremium ? '#333' : 'white'}; border-radius: 50%; width: 35px; height: 35px; display: flex; align-items: center; justify-content: center; font-weight: bold; border: 3px solid white; box-shadow: 0 2px 8px rgba(0,0,0,0.3); font-size: 12px;">
                ${cafe.isPremium ? '⭐' : '🎲'}
               </div>`,
        iconSize: [35, 35],
        iconAnchor: [17, 35],
        popupAnchor: [0, -35]
    });

    // Create marker
    const marker = L.marker([cafe.latitude, cafe.longitude], {
        icon: customIcon
    });

    // Create popup content
    const popupContent = createPopupContent(cafe);
    marker.bindPopup(popupContent, {
        maxWidth: 300,
        className: 'cafe-popup'
    });

    // Add click event to highlight in list
    marker.on('click', () => {
        highlightCafeInList(cafe.cafeId);
    });

    // Add to marker group
    marker.addTo(markerGroup);
}

// ========================================
// Create Popup Content
// ========================================
function createPopupContent(cafe) {
    const rating = cafe.averageRating ? `${cafe.averageRating.toFixed(1)} ⭐` : 'No rating';
    const reviews = cafe.totalReviews > 0 ? `(${cafe.totalReviews} reviews)` : '';
    const status = cafe.isOpenNow
        ? '<span class="badge bg-success">Open Now</span>'
        : '<span class="badge bg-secondary">Closed</span>';
    const premium = cafe.isPremium
        ? '<span class="badge bg-warning text-dark ms-1"><i class="bi bi-star-fill"></i> Featured</span>'
        : '';
    const verified = cafe.isVerified
        ? '<span class="badge bg-primary ms-1"><i class="bi bi-check-circle"></i> Verified</span>'
        : '';

    return `
        <div class="popup-cafe-name">
            ${cafe.name}
            ${premium}
        </div>
        <div class="popup-address">
            <i class="bi bi-geo-alt"></i> ${cafe.address}<br>
            ${cafe.city}, ${cafe.state}
        </div>
        <div class="popup-rating">
            <strong>${rating}</strong> ${reviews}
        </div>
        <div class="mb-2">
            ${status} ${verified}
        </div>
        <div class="text-muted small">
            <i class="bi bi-pin-map"></i> ${cafe.distanceDisplay} away
        </div>
        ${cafe.phone ? `<div class="text-muted small"><i class="bi bi-telephone"></i> ${cafe.phone}</div>` : ''}
        <div class="popup-actions">
            <a href="https://www.google.com/maps/dir/?api=1&destination=${cafe.latitude},${cafe.longitude}"
               target="_blank" class="btn btn-sm btn-primary">
                <i class="bi bi-compass"></i> Directions
            </a>
            <a href="/cafes/${cafe.slug || cafe.cafeId}" class="btn btn-sm btn-outline-secondary">
                Details
            </a>
        </div>
    `;
}

// ========================================
// Create Cafe Card (Sidebar)
// ========================================
function createCafeCard(cafe, index) {
    const rating = cafe.averageRating ? `${cafe.averageRating.toFixed(1)} ⭐` : 'No rating';
    const status = cafe.isOpenNow
        ? '<span class="badge bg-success">Open Now</span>'
        : '<span class="badge bg-secondary">Closed</span>';
    const premium = cafe.isPremium
        ? '<span class="badge bg-warning text-dark"><i class="bi bi-star-fill"></i> Featured</span>'
        : '';

    return `
        <div class="card mb-2 cafe-result-card" id="cafe-${cafe.cafeId}" onclick="focusCafe(${cafe.latitude}, ${cafe.longitude}, ${cafe.cafeId})" style="cursor: pointer;">
            <div class="card-body">
                <h6 class="card-title mb-1">
                    ${index + 1}. ${cafe.name}
                    ${premium}
                </h6>
                <p class="text-muted small mb-1">
                    ${cafe.address}<br>
                    ${cafe.city}, ${cafe.state}
                </p>
                <div class="mb-1">
                    <small><strong>${rating}</strong></small>
                    ${status}
                </div>
                <p class="text-primary small mb-0">
                    <i class="bi bi-pin-map"></i> ${cafe.distanceDisplay}
                </p>
            </div>
        </div>
    `;
}

// ========================================
// Helper Functions
// ========================================
function focusCafe(lat, lng, cafeId) {
    map.setView([lat, lng], 16);
    highlightCafeInList(cafeId);

    // Find and open popup for this marker
    markerGroup.eachLayer(layer => {
        if (layer.getLatLng().lat === lat && layer.getLatLng().lng === lng) {
            layer.openPopup();
        }
    });
}

function highlightCafeInList(cafeId) {
    // Remove previous highlights
    document.querySelectorAll('.cafe-result-card').forEach(card => {
        card.classList.remove('border-primary');
    });

    // Highlight selected
    const card = document.getElementById(`cafe-${cafeId}`);
    if (card) {
        card.classList.add('border-primary');
        card.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
}

// ========================================
// Event Listeners
// ========================================
document.addEventListener('DOMContentLoaded', () => {
    initMap();

    // Search button
    document.getElementById('searchButton')?.addEventListener('click', searchNearby);

    // Use my location button
    document.getElementById('useLocationBtn')?.addEventListener('click', useMyLocation);

    // Filter changes
    document.getElementById('radiusSelect')?.addEventListener('change', searchNearby);
    document.getElementById('openNowFilter')?.addEventListener('change', searchNearby);
    document.getElementById('hasGamesFilter')?.addEventListener('change', searchNearby);
    document.getElementById('minRatingSelect')?.addEventListener('change', searchNearby);
});

// ========================================
// Location Autocomplete (Manual Implementation)
// Note: Without Google Places, we use Nominatim
// ========================================
let autocompleteTimeout;
const locationInput = document.getElementById('locationInput');

if (locationInput) {
    locationInput.addEventListener('input', (e) => {
        clearTimeout(autocompleteTimeout);
        const query = e.target.value;

        if (query.length < 3) return;

        autocompleteTimeout = setTimeout(async () => {
            try {
                // Use Nominatim for free geocoding
                const response = await fetch(
                    `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(query)}&format=json&limit=5&countrycodes=us`,
                    {
                        headers: {
                            'User-Agent': 'BoardGameCafeFinder/1.0'
                        }
                    }
                );
                const results = await response.json();

                // Display suggestions (you can enhance this with a dropdown)
                console.log('Autocomplete results:', results);

                // For first result, update location
                if (results.length > 0) {
                    const first = results[0];
                    currentLocation = {
                        lat: parseFloat(first.lat),
                        lng: parseFloat(first.lon)
                    };
                }
            } catch (error) {
                console.error('Autocomplete error:', error);
            }
        }, 500);
    });
}

console.log('Leaflet map initialized successfully! 🗺️');
```

---

### Bước 4: Update MapController.cs

**File**: `Controllers/MapController.cs`

**Xóa hoặc comment out** phần Google Maps API key:

```csharp
public IActionResult Index(string? city, double? lat, double? lon)
{
    var model = new MapViewModel
    {
        // GoogleMapsApiKey = _configuration["GooglePlaces:ApiKey"], // KHÔNG CẦN NỮA!
        InitialLatitude = lat,
        InitialLongitude = lon,
        CityName = city
    };

    // Remove hardcoded cities if you want
    // Or keep for quick access

    return View(model);
}
```

---

### Bước 5: Update MapViewModel.cs (Optional)

**File**: `Models/ViewModels/MapViewModel.cs`

```csharp
public class MapViewModel
{
    // public string? GoogleMapsApiKey { get; set; } // KHÔNG CẦN NỮA
    public double? InitialLatitude { get; set; }
    public double? InitialLongitude { get; set; }
    public string? CityName { get; set; }
}
```

---

## ✅ Testing

### 1. Run Application

```bash
cd BoardGameCafeFinder
dotnet run
```

### 2. Navigate to Map

```
https://localhost:7xxx/Map
```

### 3. Kiểm Tra

- [ ] Map hiển thị đúng (OpenStreetMap tiles)
- [ ] Markers xuất hiện cho sample cafés
- [ ] Click marker → popup hiển thị
- [ ] "Use My Location" button hoạt động
- [ ] Filters (radius, open now) hoạt động
- [ ] Responsive trên mobile

---

## 🎨 Customization

### Thay Đổi Map Style

Leaflet hỗ trợ nhiều tile providers miễn phí:

**1. CartoDB (Sáng hơn)**:
```javascript
L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
    attribution: '© OpenStreetMap, © CartoDB'
}).addTo(map);
```

**2. CartoDB Dark Mode**:
```javascript
L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
    attribution: '© OpenStreetMap, © CartoDB'
}).addTo(map);
```

**3. Stamen Terrain**:
```javascript
L.tileLayer('https://stamen-tiles-{s}.a.ssl.fastly.net/terrain/{z}/{x}/{y}.jpg', {
    attribution: 'Map tiles by Stamen Design'
}).addTo(map);
```

### Custom Marker Icons

```javascript
const customIcon = L.icon({
    iconUrl: '/images/marker-icon.png',
    iconSize: [25, 41],
    iconAnchor: [12, 41],
    popupAnchor: [1, -34],
    shadowUrl: '/images/marker-shadow.png',
    shadowSize: [41, 41]
});

L.marker([lat, lng], { icon: customIcon }).addTo(map);
```

### Add Marker Clustering (Plugin)

**Include plugin**:
```html
<link rel="stylesheet" href="https://unpkg.com/leaflet.markercluster@1.4.1/dist/MarkerCluster.css" />
<link rel="stylesheet" href="https://unpkg.com/leaflet.markercluster@1.4.1/dist/MarkerCluster.Default.css" />
<script src="https://unpkg.com/leaflet.markercluster@1.4.1/dist/leaflet.markercluster.js"></script>
```

**Use in code**:
```javascript
const markerGroup = L.markerClusterGroup();
// Add markers to markerGroup as before
map.addLayer(markerGroup);
```

---

## 🚀 Performance Tips

1. **Limit markers**: Chỉ hiển thị cafés trong viewport
2. **Use marker clustering**: Khi có >50 markers
3. **Lazy load tiles**: Leaflet tự động làm điều này
4. **Cache geocoding results**: Lưu vào localStorage

---

## 📊 So Sánh: Before vs After

### Before (Google Maps):
```
❌ Cần API key
❌ $7 per 1,000 map loads sau $200 credit
❌ Phức tạp hơn
❌ Phụ thuộc Google
✅ Đẹp hơn một chút
✅ Street View, Directions built-in
```

### After (Leaflet.js):
```
✅ Không cần API key
✅ Hoàn toàn miễn phí
✅ Open source
✅ Lightweight (39kb)
✅ Nhiều tile providers
✅ Dễ customize
❌ Không có Street View
❌ Phải integrate Directions manually
```

---

## 🆘 Troubleshooting

### Map không hiển thị

**Kiểm tra**:
1. Console errors (F12)
2. Leaflet CSS/JS đã load chưa
3. `#map` div có height chưa

### Markers không xuất hiện

**Kiểm tra**:
1. API `/api/cafes/search` có trả data không
2. Coordinates đúng format chưa (lat, lng)
3. Console logs

### Tiles không load

**Thử tile provider khác**:
```javascript
// Instead of OSM, try CartoDB
L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png').addTo(map);
```

---

## 🎉 Hoàn Thành!

Bạn đã thành công migrate từ Google Maps sang Leaflet.js!

**Benefits**:
- ✅ Tiết kiệm $100-1000+/tháng
- ✅ Không phụ thuộc Google
- ✅ Tự do customize
- ✅ Open source community

**Next Steps**:
1. Test thoroughly trên mobile
2. Add marker clustering nếu cần
3. Implement Nominatim geocoding
4. Customize map style

---

**Tài liệu thêm**:
- [Leaflet Documentation](https://leafletjs.com/reference.html)
- [Leaflet Plugins](https://leafletjs.com/plugins.html)
- [Free Tile Providers](https://leaflet-extras.github.io/leaflet-providers/preview/)
