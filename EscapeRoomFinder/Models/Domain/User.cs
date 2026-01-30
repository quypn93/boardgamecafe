using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace EscapeRoomFinder.Models.Domain
{
    /// <summary>
    /// Extended user model based on ASP.NET Core Identity
    /// </summary>
    public class User : IdentityUser<int>
    {
        // Profile Information
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [MaxLength(100)]
        public string? DisplayName { get; set; }

        [MaxLength(1000)]
        public string? Bio { get; set; }

        [MaxLength(500)]
        [Url]
        public string? AvatarUrl { get; set; }

        // Location (optional for personalized recommendations)
        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        // Preferences - stored as JSON array
        public string? FavoriteThemes { get; set; } // Horror, Adventure, Mystery, Sci-Fi, etc.

        // Stats
        public int TotalReviews { get; set; } = 0;
        public int TotalRoomsPlayed { get; set; } = 0;
        public int TotalEscapes { get; set; } = 0;
        public int ReputationScore { get; set; } = 0;

        // Account Type
        public bool IsVenueOwner { get; set; } = false;
        public int? VenueId { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // Navigation Properties
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
        public virtual ICollection<RoomReview> RoomReviews { get; set; } = new List<RoomReview>();
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public virtual ICollection<Photo> Photos { get; set; } = new List<Photo>();
        public virtual ICollection<EscapeRoomVenue> CreatedVenues { get; set; } = new List<EscapeRoomVenue>();

        // Helper method
        public string GetFullName()
        {
            if (!string.IsNullOrEmpty(FirstName) && !string.IsNullOrEmpty(LastName))
                return $"{FirstName} {LastName}";

            return DisplayName ?? UserName ?? Email ?? "Anonymous";
        }

        public double GetEscapeRate()
        {
            if (TotalRoomsPlayed == 0) return 0;
            return Math.Round((double)TotalEscapes / TotalRoomsPlayed * 100, 1);
        }
    }
}
