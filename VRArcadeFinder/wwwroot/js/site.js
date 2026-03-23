// VR Arcade Finder - Site JavaScript

// Enable Bootstrap tooltips
document.addEventListener('DOMContentLoaded', function() {
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    var tooltipList = tooltipTriggerList.map(function(tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });
});

// Search functionality
function searchArcades(query) {
    if (!query || query.length < 2) return;

    fetch(`/api/arcades/search?q=${encodeURIComponent(query)}&limit=5`)
        .then(response => response.json())
        .then(data => {
            console.log('Search results:', data);
            // Handle search results
        })
        .catch(error => console.error('Search error:', error));
}

// Geolocation
function getUserLocation(callback) {
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(
            position => {
                callback(null, {
                    lat: position.coords.latitude,
                    lng: position.coords.longitude
                });
            },
            error => {
                callback(error, null);
            }
        );
    } else {
        callback(new Error('Geolocation not supported'), null);
    }
}

// Find nearby arcades
function findNearbyArcades() {
    getUserLocation((error, location) => {
        if (error) {
            console.error('Location error:', error);
            alert('Unable to get your location. Please enter a city manually.');
            return;
        }

        window.location.href = `/?lat=${location.lat}&lng=${location.lng}&radius=50000`;
    });
}

// Format distance
function formatDistance(meters) {
    if (meters < 1000) {
        return `${Math.round(meters)}m`;
    }
    return `${(meters / 1000).toFixed(1)}km`;
}

// Rating stars HTML
function ratingStars(rating) {
    let stars = '';
    const fullStars = Math.floor(rating);
    const hasHalf = rating % 1 >= 0.5;

    for (let i = 0; i < fullStars; i++) {
        stars += '<i class="bi bi-star-fill text-warning"></i>';
    }
    if (hasHalf) {
        stars += '<i class="bi bi-star-half text-warning"></i>';
    }
    for (let i = fullStars + (hasHalf ? 1 : 0); i < 5; i++) {
        stars += '<i class="bi bi-star text-warning"></i>';
    }

    return stars;
}
