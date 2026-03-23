// CookingClassFinder JavaScript

(function() {
    'use strict';

    // Initialize tooltips
    document.addEventListener('DOMContentLoaded', function() {
        // Bootstrap tooltips
        var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function(tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });

        // Auto-hide alerts after 5 seconds
        var alerts = document.querySelectorAll('.alert:not(.alert-permanent)');
        alerts.forEach(function(alert) {
            setTimeout(function() {
                var bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
                bsAlert.close();
            }, 5000);
        });
    });

    // Search form location autocomplete (if Google Places API is loaded)
    window.initPlacesAutocomplete = function() {
        var locationInput = document.getElementById('locationSearch');
        if (locationInput && typeof google !== 'undefined' && google.maps && google.maps.places) {
            var autocomplete = new google.maps.places.Autocomplete(locationInput, {
                types: ['(cities)']
            });

            autocomplete.addListener('place_changed', function() {
                var place = autocomplete.getPlace();
                if (place.geometry) {
                    // Update hidden fields with lat/lng if they exist
                    var latInput = document.getElementById('latitude');
                    var lngInput = document.getElementById('longitude');
                    if (latInput) latInput.value = place.geometry.location.lat();
                    if (lngInput) lngInput.value = place.geometry.location.lng();
                }
            });
        }
    };

    // Rating stars interaction
    window.initRatingStars = function(containerId, inputId) {
        var container = document.getElementById(containerId);
        var input = document.getElementById(inputId);
        if (!container || !input) return;

        var stars = container.querySelectorAll('.star');
        stars.forEach(function(star, index) {
            star.addEventListener('click', function() {
                var rating = index + 1;
                input.value = rating;
                updateStars(stars, rating);
            });

            star.addEventListener('mouseenter', function() {
                updateStars(stars, index + 1, true);
            });
        });

        container.addEventListener('mouseleave', function() {
            updateStars(stars, parseInt(input.value) || 0);
        });

        function updateStars(stars, rating, isHover) {
            stars.forEach(function(s, i) {
                if (i < rating) {
                    s.classList.remove('bi-star');
                    s.classList.add('bi-star-fill');
                } else {
                    s.classList.remove('bi-star-fill');
                    s.classList.add('bi-star');
                }
            });
        }
    };

    // Lazy load images
    if ('IntersectionObserver' in window) {
        var lazyImages = document.querySelectorAll('img[data-src]');
        var imageObserver = new IntersectionObserver(function(entries, observer) {
            entries.forEach(function(entry) {
                if (entry.isIntersecting) {
                    var img = entry.target;
                    img.src = img.dataset.src;
                    img.removeAttribute('data-src');
                    imageObserver.unobserve(img);
                }
            });
        });

        lazyImages.forEach(function(img) {
            imageObserver.observe(img);
        });
    }

    // Form validation
    var forms = document.querySelectorAll('.needs-validation');
    forms.forEach(function(form) {
        form.addEventListener('submit', function(event) {
            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopPropagation();
            }
            form.classList.add('was-validated');
        });
    });

    // Smooth scroll for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(function(anchor) {
        anchor.addEventListener('click', function(e) {
            var targetId = this.getAttribute('href');
            if (targetId === '#') return;

            var target = document.querySelector(targetId);
            if (target) {
                e.preventDefault();
                target.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });

    // Debug mode
    if (window.location.search.includes('debug=true')) {
        console.log('CookingClassFinder Debug Mode Enabled');
    }

})();
