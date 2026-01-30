using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EscapeRoomFinder.Models.Domain
{
    /// <summary>
    /// Photo for an escape room venue
    /// </summary>
    public class Photo
    {
        [Key]
        public int PhotoId { get; set; }

        [Required]
        public int VenueId { get; set; }

        public int? UploadedByUserId { get; set; }

        [Required]
        [MaxLength(500)]
        [Url]
        public string Url { get; set; } = string.Empty;

        [MaxLength(500)]
        [Url]
        public string? ThumbnailUrl { get; set; }

        [MaxLength(500)]
        public string? Caption { get; set; }

        [MaxLength(500)]
        public string? LocalPath { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public bool IsApproved { get; set; } = false;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("VenueId")]
        public virtual EscapeRoomVenue Venue { get; set; } = null!;

        [ForeignKey("UploadedByUserId")]
        public virtual User? UploadedBy { get; set; }
    }

    /// <summary>
    /// Photo for a specific escape room
    /// </summary>
    public class RoomPhoto
    {
        [Key]
        public int RoomPhotoId { get; set; }

        [Required]
        public int RoomId { get; set; }

        public int? UploadedByUserId { get; set; }

        [Required]
        [MaxLength(500)]
        [Url]
        public string Url { get; set; } = string.Empty;

        [MaxLength(500)]
        [Url]
        public string? ThumbnailUrl { get; set; }

        [MaxLength(500)]
        public string? Caption { get; set; }

        [MaxLength(500)]
        public string? LocalPath { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public bool IsApproved { get; set; } = false;

        // Spoiler flag - room photos might reveal puzzles
        public bool ContainsSpoilers { get; set; } = false;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("RoomId")]
        public virtual EscapeRoom Room { get; set; } = null!;

        [ForeignKey("UploadedByUserId")]
        public virtual User? UploadedBy { get; set; }
    }
}
