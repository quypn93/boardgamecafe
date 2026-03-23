using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CookingClassFinder.Models.Domain
{
    /// <summary>
    /// Photo for a cooking school
    /// </summary>
    public class Photo
    {
        [Key]
        public int PhotoId { get; set; }

        [Required]
        public int SchoolId { get; set; }

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
        [ForeignKey("SchoolId")]
        public virtual CookingSchool School { get; set; } = null!;

        [ForeignKey("UploadedByUserId")]
        public virtual User? UploadedBy { get; set; }
    }

    /// <summary>
    /// Photo for a specific cooking class
    /// </summary>
    public class ClassPhoto
    {
        [Key]
        public int ClassPhotoId { get; set; }

        [Required]
        public int ClassId { get; set; }

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

        // Photo type - kitchen, dishes, students, instructor
        [MaxLength(50)]
        public string? PhotoType { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("ClassId")]
        public virtual CookingClass Class { get; set; } = null!;

        [ForeignKey("UploadedByUserId")]
        public virtual User? UploadedBy { get; set; }
    }
}
