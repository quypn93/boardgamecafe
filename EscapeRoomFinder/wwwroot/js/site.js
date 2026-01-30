// Escape Room Finder - Custom JavaScript

// Initialize tooltips
document.addEventListener('DOMContentLoaded', function() {
    // Bootstrap tooltips
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipTriggerList.forEach(el => new bootstrap.Tooltip(el));

    // Bootstrap popovers
    const popoverTriggerList = document.querySelectorAll('[data-bs-toggle="popover"]');
    popoverTriggerList.forEach(el => new bootstrap.Popover(el));
});

// Geolocation helper
function getCurrentLocation(callback) {
    if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(
            position => callback(null, {
                lat: position.coords.latitude,
                lng: position.coords.longitude
            }),
            error => callback(error, null)
        );
    } else {
        callback(new Error('Geolocation not supported'), null);
    }
}

// Search near me
function searchNearMe() {
    getCurrentLocation((error, coords) => {
        if (error) {
            alert('Unable to get your location. Please allow location access or search manually.');
            return;
        }
        window.location.href = `/?lat=${coords.lat}&lng=${coords.lng}&radius=50`;
    });
}

// Format distance
function formatDistance(km) {
    if (km < 1) {
        return `${Math.round(km * 1000)}m`;
    }
    return `${km.toFixed(1)} km`;
}

// Format rating
function formatRating(rating) {
    if (!rating) return 'No rating';
    const fullStars = Math.floor(rating);
    const halfStar = rating % 1 >= 0.5;
    let stars = '★'.repeat(fullStars);
    if (halfStar) stars += '½';
    stars += '☆'.repeat(5 - fullStars - (halfStar ? 1 : 0));
    return `${stars} ${rating.toFixed(1)}`;
}

// Debounce function
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Lazy load images
function lazyLoadImages() {
    const images = document.querySelectorAll('img[data-src]');
    const imageObserver = new IntersectionObserver((entries, observer) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const img = entry.target;
                img.src = img.dataset.src;
                img.removeAttribute('data-src');
                observer.unobserve(img);
            }
        });
    });

    images.forEach(img => imageObserver.observe(img));
}

// Toggle spoiler content
function toggleSpoiler(element) {
    element.classList.toggle('revealed');
}

// Copy to clipboard
function copyToClipboard(text) {
    navigator.clipboard.writeText(text).then(() => {
        // Show success feedback
        const toast = document.createElement('div');
        toast.className = 'position-fixed bottom-0 end-0 p-3';
        toast.innerHTML = `
            <div class="toast show" role="alert">
                <div class="toast-body">
                    <i class="bi bi-check-circle text-success"></i> Copied to clipboard!
                </div>
            </div>
        `;
        document.body.appendChild(toast);
        setTimeout(() => toast.remove(), 2000);
    });
}

// Share venue
function shareVenue(venueName, venueUrl) {
    if (navigator.share) {
        navigator.share({
            title: `${venueName} - Escape Room Finder`,
            url: venueUrl
        });
    } else {
        copyToClipboard(venueUrl);
    }
}

// Form validation
function validateForm(form) {
    const inputs = form.querySelectorAll('input[required], textarea[required], select[required]');
    let isValid = true;

    inputs.forEach(input => {
        if (!input.value.trim()) {
            input.classList.add('is-invalid');
            isValid = false;
        } else {
            input.classList.remove('is-invalid');
        }
    });

    return isValid;
}

// Star rating input
function initStarRating(container) {
    const stars = container.querySelectorAll('label');
    const inputs = container.querySelectorAll('input');

    stars.forEach((star, index) => {
        star.addEventListener('mouseover', () => {
            stars.forEach((s, i) => {
                s.classList.toggle('text-warning', i >= (stars.length - index - 1));
            });
        });

        star.addEventListener('mouseout', () => {
            const checkedInput = container.querySelector('input:checked');
            if (checkedInput) {
                const checkedValue = parseInt(checkedInput.value);
                stars.forEach((s, i) => {
                    s.classList.toggle('text-warning', i >= (stars.length - checkedValue));
                });
            } else {
                stars.forEach(s => s.classList.remove('text-warning'));
            }
        });
    });
}

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', function() {
    lazyLoadImages();

    // Initialize star ratings
    document.querySelectorAll('.rating-input').forEach(initStarRating);

    // Form validation
    document.querySelectorAll('form[data-validate]').forEach(form => {
        form.addEventListener('submit', (e) => {
            if (!validateForm(form)) {
                e.preventDefault();
            }
        });
    });
});
