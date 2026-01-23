// Board Game Café Finder - Interactive Map using Leaflet + OpenStreetMap
// Global variables
let map;
let markers = [];
let markerGroup;
let currentLocation = null;
let userLocationMarker = null;

// Initialize map when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    initMap();
});

// Initialize map
function initMap() {
    console.log('Initializing Leaflet map...');

    // Get config from window object (set in view)
    const config = window.mapConfig || {};
    const defaultCenter = [
        config.defaultLat || 39.8283,
        config.defaultLon || -98.5795
    ];
    const defaultZoom = config.defaultZoom || 4;

    // Create map with OpenStreetMap tiles
    map = L.map('map', {
        zoomControl: false // We'll use custom controls
    }).setView(defaultCenter, defaultZoom);

    // Add OpenStreetMap tile layer with better styling
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    }).addTo(map);

    // Create marker group for easy management
    markerGroup = L.layerGroup().addTo(map);

    // Try to get user's location
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(
            (position) => {
                currentLocation = {
                    lat: position.coords.latitude,
                    lng: position.coords.longitude
                };
                map.setView([currentLocation.lat, currentLocation.lng], 12);

                // Add marker for user's location
                addUserLocationMarker(currentLocation);

                // Auto-search
                searchNearby();
            },
            (error) => {
                console.log('Location access denied or unavailable');
                // Use initial location if provided
                if (config.initialLat && config.initialLon) {
                    const initialLat = parseFloat(config.initialLat);
                    const initialLon = parseFloat(config.initialLon);
                    map.setView([initialLat, initialLon], 12);
                }
            }
        );
    }

    // Add event listeners
    setupEventListeners();
}

// Add marker for user's location
function addUserLocationMarker(location) {
    if (userLocationMarker) {
        map.removeLayer(userLocationMarker);
    }

    const userIcon = L.divIcon({
        className: 'user-location-icon',
        html: `<div style="
            background-color: #4285F4;
            width: 18px;
            height: 18px;
            border-radius: 50%;
            border: 3px solid white;
            box-shadow: 0 2px 8px rgba(66, 133, 244, 0.5);
            animation: pulse 2s infinite;
        "></div>
        <style>
            @keyframes pulse {
                0% { box-shadow: 0 0 0 0 rgba(66, 133, 244, 0.4); }
                70% { box-shadow: 0 0 0 15px rgba(66, 133, 244, 0); }
                100% { box-shadow: 0 0 0 0 rgba(66, 133, 244, 0); }
            }
        </style>`,
        iconSize: [18, 18],
        iconAnchor: [9, 9]
    });

    userLocationMarker = L.marker([location.lat, location.lng], {
        icon: userIcon,
        title: 'Your Location',
        zIndexOffset: 1000
    }).addTo(map);

    userLocationMarker.bindPopup('<strong>Your Location</strong>');
}

// Setup event listeners
function setupEventListeners() {
    document.getElementById('searchBtn').addEventListener('click', () => {
        const center = map.getCenter();
        if (center) {
            searchNearby();
        } else {
            alert('Please select a location first');
        }
    });

    document.getElementById('useLocationBtn').addEventListener('click', useMyLocation);

    // Empty state location button
    const emptyStateBtn = document.getElementById('emptyStateLocationBtn');
    if (emptyStateBtn) {
        emptyStateBtn.addEventListener('click', useMyLocation);
    }

    // Location search button
    document.getElementById('searchLocationBtn').addEventListener('click', searchLocation);

    // Location input enter key
    document.getElementById('locationInput').addEventListener('keypress', (e) => {
        if (e.key === 'Enter') {
            searchLocation();
        }
    });

    // Custom map controls
    const zoomInBtn = document.getElementById('zoomInBtn');
    const zoomOutBtn = document.getElementById('zoomOutBtn');
    const centerMapBtn = document.getElementById('centerMapBtn');

    if (zoomInBtn) {
        zoomInBtn.addEventListener('click', () => map.zoomIn());
    }
    if (zoomOutBtn) {
        zoomOutBtn.addEventListener('click', () => map.zoomOut());
    }
    if (centerMapBtn) {
        centerMapBtn.addEventListener('click', () => {
            if (currentLocation) {
                map.setView([currentLocation.lat, currentLocation.lng], 13);
            }
        });
    }

    // Filters - auto-search on change
    document.getElementById('radiusSelect').addEventListener('change', () => {
        if (map.getCenter()) searchNearby();
    });
    document.getElementById('openNowFilter').addEventListener('change', () => {
        if (map.getCenter()) searchNearby();
    });
    document.getElementById('hasGamesFilter').addEventListener('change', () => {
        if (map.getCenter()) searchNearby();
    });
    document.getElementById('minRatingSelect').addEventListener('change', () => {
        if (map.getCenter()) searchNearby();
    });

    // Country/City filters
    const countrySelect = document.getElementById('countrySelect');
    const citySelect = document.getElementById('citySelect');

    if (countrySelect) {
        countrySelect.addEventListener('change', () => {
            // When country changes, filter cities or search
            searchByFilter();
        });
    }

    if (citySelect) {
        citySelect.addEventListener('change', () => {
            searchByFilter();
        });
    }

    // Note: Category Select2 change event is handled in the view
}

// Get selected categories from Select2
function getSelectedCategories() {
    const categoriesSelect = document.getElementById('categoriesSelect');
    if (categoriesSelect && typeof $ !== 'undefined' && $(categoriesSelect).data('select2')) {
        return $(categoriesSelect).val() || [];
    }
    // Fallback for non-Select2
    if (categoriesSelect) {
        return Array.from(categoriesSelect.selectedOptions).map(opt => opt.value);
    }
    return [];
}

// Search by country/city/category filter (without requiring location)
async function searchByFilter() {
    const country = document.getElementById('countrySelect')?.value || '';
    const city = document.getElementById('citySelect')?.value || '';

    // Get selected categories from Select2
    const selectedCategories = getSelectedCategories();

    // If all filters are empty, do nothing special
    if (!country && !city && selectedCategories.length === 0) {
        return;
    }

    showLoading(true);

    try {
        // Build query string
        let queryParams = new URLSearchParams();
        if (country) queryParams.append('country', country);
        if (city) queryParams.append('city', city);

        const openNow = document.getElementById('openNowFilter').checked;
        const hasGames = document.getElementById('hasGamesFilter').checked;
        const minRating = document.getElementById('minRatingSelect').value;

        if (openNow) queryParams.append('openNow', 'true');
        if (hasGames) queryParams.append('hasGames', 'true');
        if (minRating) queryParams.append('minRating', minRating);

        // Add categories as comma-separated string
        if (selectedCategories.length > 0) {
            queryParams.append('categories', selectedCategories.join(','));
        }

        const response = await fetch(`/api/cafes/filter?${queryParams.toString()}`);

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        console.log('Filter results:', result);

        if (result.success) {
            displayResults(result.data);

            // Center map on first result if available
            if (result.data && result.data.length > 0) {
                const firstCafe = result.data[0];
                map.setView([firstCafe.latitude, firstCafe.longitude], 10);
            }
        } else {
            console.error('Filter failed:', result.message);
            displayError('Filter failed. Please try again.');
        }
    } catch (error) {
        console.error('Error filtering cafés:', error);
        displayError('An error occurred while filtering. Please try again.');
    } finally {
        showLoading(false);
    }
}

// Search for a location using Nominatim (OpenStreetMap geocoding)
async function searchLocation() {
    const query = document.getElementById('locationInput').value.trim();
    if (!query) {
        alert('Please enter a location to search');
        return;
    }

    showLoading(true);

    try {
        // Use Nominatim for geocoding (free, no API key needed)
        const response = await fetch(`https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}&limit=1`, {
            headers: {
                'Accept': 'application/json',
                'User-Agent': 'BoardGameCafeFinder/1.0'
            }
        });

        const results = await response.json();

        if (results && results.length > 0) {
            const result = results[0];
            const lat = parseFloat(result.lat);
            const lon = parseFloat(result.lon);

            currentLocation = { lat, lng: lon };
            map.setView([lat, lon], 13);

            // Add or update user location marker
            addUserLocationMarker(currentLocation);

            // Search for cafes nearby
            searchNearby();
        } else {
            alert('Location not found. Please try a different search term.');
        }
    } catch (error) {
        console.error('Error searching location:', error);
        alert('Error searching for location. Please try again.');
    } finally {
        showLoading(false);
    }
}

// Use user's current location
function useMyLocation() {
    if (navigator.geolocation) {
        showLoading(true);
        navigator.geolocation.getCurrentPosition(
            (position) => {
                currentLocation = {
                    lat: position.coords.latitude,
                    lng: position.coords.longitude
                };
                map.setView([currentLocation.lat, currentLocation.lng], 12);

                // Add user location marker
                addUserLocationMarker(currentLocation);

                searchNearby();
                showLoading(false);
            },
            (error) => {
                showLoading(false);
                alert('Unable to get your location. Please enter a location manually.');
                console.error('Geolocation error:', error);
            }
        );
    } else {
        alert('Geolocation is not supported by your browser');
    }
}

// Search for nearby cafés
async function searchNearby() {
    const center = map.getCenter();
    if (!center) {
        console.error('Map center not available');
        return;
    }

    const radius = parseInt(document.getElementById('radiusSelect').value);
    const openNow = document.getElementById('openNowFilter').checked;
    const hasGames = document.getElementById('hasGamesFilter').checked;
    const minRating = parseFloat(document.getElementById('minRatingSelect').value) || null;

    const requestBody = {
        latitude: center.lat,
        longitude: center.lng,
        radius: radius,
        openNow: openNow,
        hasGames: hasGames,
        minRating: minRating,
        limit: 50
    };

    console.log('Searching cafés:', requestBody);
    showLoading(true);

    try {
        const response = await fetch('/api/cafes/search', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(requestBody)
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        console.log('Search results:', result);

        if (result.success) {
            displayResults(result.data);
        } else {
            console.error('Search failed:', result.message);
            displayError('Search failed. Please try again.');
        }
    } catch (error) {
        console.error('Error searching cafés:', error);
        displayError('An error occurred while searching. Please try again.');
    } finally {
        showLoading(false);
    }
}

// Display search results
function displayResults(cafes) {
    // Clear existing markers
    clearMarkers();

    // Update counter badge
    const countBadge = document.getElementById('cafesCountBadge');
    if (countBadge) {
        countBadge.textContent = cafes.length;
    }

    // Update mobile results count (for mobile toggle button)
    if (typeof window.updateMobileResultsCount === 'function') {
        window.updateMobileResultsCount(cafes.length);
    }

    // Update results container
    const container = document.getElementById('resultsContainer');

    if (cafes.length === 0) {
        container.innerHTML = `
            <div class="empty-state text-center p-5">
                <div class="empty-icon mb-3">
                    <i class="bi bi-search"></i>
                </div>
                <h5 class="text-muted">No Cafés Found</h5>
                <p class="text-muted small mb-3">Try expanding your search radius or adjusting filters</p>
            </div>
        `;
        return;
    }

    // Create result cards HTML
    container.innerHTML = `
        <div class="results-header">
            <i class="bi bi-list-ul me-2"></i>${cafes.length} café${cafes.length !== 1 ? 's' : ''} found
        </div>
        ${cafes.map((cafe, index) => createResultCardHTML(cafe, index)).join('')}
    `;

    // Add markers to map
    cafes.forEach((cafe, index) => {
        const marker = createCafeMarker(cafe, index);
        markers.push({ marker, cafe, index });
    });

    // Add click listeners to result cards
    document.querySelectorAll('.result-card').forEach(item => {
        item.addEventListener('click', function () {
            const cafeId = parseInt(this.dataset.cafeId);
            const markerData = markers.find(m => m.cafe.id === cafeId);

            if (markerData) {
                map.setView([markerData.cafe.latitude, markerData.cafe.longitude], 15);
                markerData.marker.openPopup();
                highlightListItem(cafeId);
            }
        });
    });

    // Fit map to show all markers
    if (cafes.length > 0) {
        fitMapBounds();
    }
}

// Create HTML for a result card
function createResultCardHTML(cafe, index) {
    const ratingHtml = cafe.averageRating
        ? `<span class="rating"><i class="bi bi-star-fill"></i> ${cafe.averageRating.toFixed(1)}</span>`
        : '';

    const gamesHtml = cafe.totalGames > 0
        ? `<span class="games-count"><i class="bi bi-controller"></i> ${cafe.totalGames}</span>`
        : '';

    const statusHtml = cafe.isOpenNow
        ? '<span class="status-badge bg-success text-white">Open</span>'
        : '';

    return `
        <div class="result-card" data-cafe-id="${cafe.id}" data-index="${index}">
            <div class="d-flex align-items-start gap-3">
                <div class="card-number">${index + 1}</div>
                <div class="flex-grow-1">
                    <div class="d-flex justify-content-between align-items-start">
                        <div class="cafe-name">${cafe.name}</div>
                        ${statusHtml}
                    </div>
                    <div class="cafe-address">
                        <i class="bi bi-geo-alt-fill text-danger"></i>
                        ${cafe.address || ''}${cafe.city ? `, ${cafe.city}` : ''}
                    </div>
                    <div class="cafe-meta">
                        ${ratingHtml}
                        ${gamesHtml}
                        <span class="distance">
                            <i class="bi bi-signpost-2"></i> ${cafe.distanceDisplay || ''}
                        </span>
                    </div>
                </div>
            </div>
        </div>
    `;
}

// Create a marker for a café
function createCafeMarker(cafe, index) {
    // Determine marker color based on café properties
    let color = '#667eea'; // Default purple
    if (cafe.isPremium) {
        color = '#FFD700'; // Gold for premium
    } else if (cafe.isVerified) {
        color = '#4CAF50'; // Green for verified
    }

    const cafeIcon = L.divIcon({
        className: 'cafe-marker-icon',
        html: `<div style="
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            width: 32px;
            height: 32px;
            border-radius: 50%;
            border: 3px solid white;
            box-shadow: 0 3px 10px rgba(102, 126, 234, 0.4);
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-size: 12px;
            font-weight: bold;
            transition: transform 0.2s;
        " onmouseover="this.style.transform='scale(1.15)'" onmouseout="this.style.transform='scale(1)'">${index + 1}</div>`,
        iconSize: [32, 32],
        iconAnchor: [16, 16],
        popupAnchor: [0, -16]
    });

    const marker = L.marker([cafe.latitude, cafe.longitude], {
        icon: cafeIcon,
        title: cafe.name
    }).addTo(markerGroup);

    // Create enhanced popup content
    const popupContent = createPopupHTML(cafe);
    marker.bindPopup(popupContent, {
        maxWidth: 300,
        className: 'cafe-popup-container'
    });

    // Highlight list item when marker is clicked
    marker.on('click', () => {
        highlightListItem(cafe.id);
    });

    return marker;
}

// Create HTML for popup
function createPopupHTML(cafe) {
    const ratingHtml = cafe.averageRating
        ? `<span class="popup-meta-item"><i class="bi bi-star-fill text-warning"></i> ${cafe.averageRating.toFixed(1)} (${cafe.totalReviews || 0})</span>`
        : '';

    const gamesHtml = cafe.totalGames > 0
        ? `<span class="popup-meta-item"><i class="bi bi-controller text-primary"></i> ${cafe.totalGames} games</span>`
        : '';

    const statusHtml = cafe.isOpenNow
        ? '<span class="badge bg-success">Open Now</span>'
        : '<span class="badge bg-secondary">Closed</span>';

    return `
        <div class="cafe-popup">
            <div class="popup-title">${cafe.name}</div>
            <div class="popup-address">${cafe.address || ''}${cafe.city ? `, ${cafe.city}` : ''}</div>
            <div class="popup-meta mt-2">
                
                ${ratingHtml}
                ${gamesHtml}
            </div>
            ${cafe.phone ? `<div class="mt-2" style="font-size: 0.85rem;"><i class="bi bi-telephone"></i> ${cafe.phone}</div>` : ''}
            <div class="popup-actions mt-3">
                <a href="/cafe/${cafe.slug}" class="btn btn-primary btn-sm text-white">
                    <i class="bi bi-eye"></i> View
                </a>
                <a href="https://www.google.com/maps/dir/?api=1&destination=${cafe.latitude},${cafe.longitude}"
                   target="_blank" class="btn btn-outline-primary btn-sm">
                    <i class="bi bi-signpost-2"></i> Directions
                </a>
            </div>
        </div>
    `;
}

// Highlight selected item in results list
function highlightListItem(cafeId) {
    document.querySelectorAll('.result-card').forEach(item => {
        item.classList.remove('active');
    });

    const selectedItem = document.querySelector(`.result-card[data-cafe-id="${cafeId}"]`);
    if (selectedItem) {
        selectedItem.classList.add('active');
        selectedItem.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
}

// Clear all markers from map
function clearMarkers() {
    markerGroup.clearLayers();
    markers = [];
}

// Fit map bounds to show all markers
function fitMapBounds() {
    if (markers.length === 0) return;

    const group = L.featureGroup(markers.map(m => m.marker));
    map.fitBounds(group.getBounds().pad(0.1));

    // Don't zoom in too much for single marker
    if (markers.length === 1) {
        map.setZoom(14);
    }
}

// Show/hide loading overlay
function showLoading(show) {
    const overlay = document.getElementById('loadingOverlay');
    overlay.style.display = show ? 'flex' : 'none';
}

// Display error message
function displayError(message) {
    const container = document.getElementById('resultsContainer');
    container.innerHTML = `
        <div class="empty-state text-center p-5">
            <div class="empty-icon mb-3" style="background: #fee2e2;">
                <i class="bi bi-exclamation-triangle" style="color: #dc3545;"></i>
            </div>
            <h5 class="text-muted">Something went wrong</h5>
            <p class="text-muted small mb-3">${message}</p>
            <button class="btn btn-primary btn-sm" onclick="searchNearby()">
                <i class="bi bi-arrow-clockwise me-1"></i> Try Again
            </button>
        </div>
    `;
}

// Export for testing
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { initMap, searchNearby };
}
