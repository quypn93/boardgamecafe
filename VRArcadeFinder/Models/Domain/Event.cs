using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VRArcadeFinder.Models.Domain
{
    public class Event
    {
        [Key]
        public int EventId { get; set; }

        [Required]
        public int ArcadeId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(5000)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? EventType { get; set; } // Tournament, VR Party, Workshop, Demo Day, League Night, etc.

        [Required]
        public DateTime StartDateTime { get; set; }

        public DateTime? EndDateTime { get; set; }

        public int? MaxParticipants { get; set; }

        public int CurrentParticipants { get; set; } = 0;

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? EntryFee { get; set; }

        [MaxLength(500)]
        [Url]
        public string? ImageUrl { get; set; }

        // VR-specific fields
        [MaxLength(200)]
        public string? FeaturedGame { get; set; } // The VR game featured in this event

        [MaxLength(50)]
        public string? SkillLevel { get; set; } // Beginner, Intermediate, Advanced, All Levels

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("ArcadeId")]
        public virtual Arcade Arcade { get; set; } = null!;

        public virtual ICollection<EventBooking> Bookings { get; set; } = new List<EventBooking>();

        // Helper methods
        public bool IsUpcoming()
        {
            return StartDateTime > DateTime.UtcNow;
        }

        public bool IsFull()
        {
            return MaxParticipants.HasValue && CurrentParticipants >= MaxParticipants.Value;
        }

        public int GetAvailableSeats()
        {
            if (!MaxParticipants.HasValue)
                return int.MaxValue;

            return Math.Max(0, MaxParticipants.Value - CurrentParticipants);
        }

        public string GetEventDate()
        {
            return StartDateTime.ToString("MMMM dd, yyyy");
        }

        public string GetEventTime()
        {
            var start = StartDateTime.ToString("h:mm tt");
            var end = EndDateTime?.ToString("h:mm tt");

            return end != null ? $"{start} - {end}" : start;
        }
    }
}
