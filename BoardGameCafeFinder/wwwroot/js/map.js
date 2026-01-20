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
    map = L.map('map').setView(defaultCenter, defaultZoom);

    // Add OpenStreetMap tile layer
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
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
        html: '<div style="background-color: #4285F4; width: 16px; height: 16px; border-radius: 50%; border: 3px solid white; box-shadow: 0 2px 5px rgba(0,0,0,0.3);"></div>',
        iconSize: [16, 16],
        iconAnchor: [8, 8]
    });

    userLocationMarker = L.marker([location.lat, location.lng], {
        icon: userIcon,
        title: 'Your Location'
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

    // Location search button
    document.getElementById('searchLocationBtn').addEventListener('click', searchLocation);

    // Location input enter key
    document.getElementById('locationInput').addEventListener('keypress', (e) => {
        if (e.key === 'Enter') {
            searchLocation();
        }
    });

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

    // Update results container
    const container = document.getElementById('resultsContainer');

    if (cafes.length === 0) {
        container.innerHTML = '<div class="text-center p-4 text-muted"><p>No cafés found in this area</p></div>';
        return;
    }

    // Create result items HTML
    container.innerHTML = cafes.map((cafe, index) => `
        <div class="result-item" data-cafe-id="${cafe.id}" data-index="${index}">
            <h6>${cafe.name}</h6>
            <p class="text-muted small mb-1">
                <i class="bi bi-geo-alt"></i> ${cafe.address}
                ${cafe.city ? `<br/>${cafe.city}${cafe.state ? ', ' + cafe.state : ''}` : ''}
            </p>
            <div class="d-flex justify-content-between align-items-center mb-1">
                <span class="small">
                    ${cafe.averageRating ? `${cafe.averageRating.toFixed(1)} ★` : 'No ratings'}
                    ${cafe.totalReviews > 0 ? `(${cafe.totalReviews})` : ''}
                </span>
                ${cafe.isOpenNow
                    ? '<span class="badge bg-success">Open Now</span>'
                    // : '<span class="badge bg-secondary">Closed</span>'}
                    : ''}
            </div>
            <div class="d-flex justify-content-between align-items-center">
                <span class="text-primary small">
                    <i class="bi bi-arrow-right-circle"></i> ${cafe.distanceDisplay || ''}
                </span>
                ${cafe.totalGames > 0
                    ? `<span class="text-success small">${cafe.totalGames} games</span>`
                    : ''}
            </div>
            ${cafe.isPremium ? '<span class="badge bg-warning text-dark mt-1">Featured</span>' : ''}
        </div>
    `).join('');

    // Add markers to map
    cafes.forEach((cafe, index) => {
        const marker = createCafeMarker(cafe, index);
        markers.push({ marker, cafe, index });
    });

    // Add click listeners to result items
    document.querySelectorAll('.result-item').forEach(item => {
        item.addEventListener('click', function () {
            const cafeId = parseInt(this.dataset.cafeId);
            const index = parseInt(this.dataset.index);
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
        html: `<div style="background-color: ${color}; width: 24px; height: 24px; border-radius: 50%; border: 3px solid white; box-shadow: 0 2px 5px rgba(0,0,0,0.3); display: flex; align-items: center; justify-content: center; color: white; font-size: 12px; font-weight: bold;">${index + 1}</div>`,
        iconSize: [24, 24],
        iconAnchor: [12, 12],
        popupAnchor: [0, -12]
    });

    const marker = L.marker([cafe.latitude, cafe.longitude], {
        icon: cafeIcon,
        title: cafe.name
    }).addTo(markerGroup);

    // Create popup content
    const popupContent = `
        <div class="cafe-popup" style="min-width: 200px;">
            <h5>${cafe.name}</h5>
            ${cafe.isPremium ? '<span class="badge bg-warning text-dark mb-2">Featured</span>' : ''}
            <p class="text-muted mb-2" style="font-size: 0.875rem;">${cafe.address}</p>
            ${cafe.isOpenNow
                ? '<span class="badge bg-success mb-2">Open Now</span>'
                : '<span class="badge bg-danger mb-2">Closed</span>'}
            <div class="mt-2" style="font-size: 0.875rem;">
                <strong>Rating:</strong> ${cafe.averageRating ? cafe.averageRating.toFixed(1) + ' ★' : 'No ratings'}
                ${cafe.totalReviews > 0 ? `(${cafe.totalReviews} reviews)` : ''}
            </div>
            ${cafe.totalGames > 0 ? `<div class="mt-1" style="font-size: 0.875rem;"><strong>${cafe.totalGames}</strong> games available</div>` : ''}
            ${cafe.phone ? `<div class="mt-1" style="font-size: 0.875rem;"><i class="bi bi-telephone"></i> ${cafe.phone}</div>` : ''}
            <div class="mt-3">
                <a href="/cafe/${cafe.slug}" class="btn btn-sm btn-primary" target="_blank">View Details</a>
                <a href="https://www.openstreetmap.org/directions?from=&to=${cafe.latitude},${cafe.longitude}"
                   target="_blank" class="btn btn-sm btn-outline-primary">Directions</a>
            </div>
        </div>
    `;

    marker.bindPopup(popupContent);

    // Highlight list item when marker is clicked
    marker.on('click', () => {
        highlightListItem(cafe.id);
    });

    return marker;
}

// Highlight selected item in results list
function highlightListItem(cafeId) {
    document.querySelectorAll('.result-item').forEach(item => {
        item.classList.remove('active');
    });

    const selectedItem = document.querySelector(`.result-item[data-cafe-id="${cafeId}"]`);
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
        <div class="alert alert-danger m-3" role="alert">
            <i class="bi bi-exclamation-triangle"></i> ${message}
        </div>
    `;
}

// Export for testing
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { initMap, searchNearby };
}
