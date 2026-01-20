using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardGameCafeFinder.Models.Domain
{
    public class Photo
    {
        [Key]
        public int PhotoId { get; set; }

        [Required]
        public int CafeId { get; set; }

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
        [ForeignKey("CafeId")]
        public virtual Cafe Cafe { get; set; } = null!;

        [ForeignKey("UploadedByUserId")]
        public virtual User? UploadedBy { get; set; }
    }
}
