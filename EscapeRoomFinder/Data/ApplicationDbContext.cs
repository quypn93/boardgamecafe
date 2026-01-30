using EscapeRoomFinder.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoomFinder.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets
        public DbSet<EscapeRoomVenue> Venues { get; set; }
        public DbSet<EscapeRoom> Rooms { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<RoomReview> RoomReviews { get; set; }
        public DbSet<Photo> Photos { get; set; }
        public DbSet<RoomPhoto> RoomPhotos { get; set; }
        public DbSet<PremiumListing> PremiumListings { get; set; }
        public DbSet<BlogPost> BlogPosts { get; set; }
        public DbSet<ClaimRequest> ClaimRequests { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<AffiliateClick> AffiliateClicks { get; set; }
        public DbSet<Booking> Bookings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // EscapeRoomVenue Configuration
            modelBuilder.Entity<EscapeRoomVenue>(entity =>
            {
                entity.HasKey(e => e.VenueId);

                entity.HasIndex(e => e.Slug).IsUnique();
                entity.HasIndex(e => new { e.Latitude, e.Longitude });
                entity.HasIndex(e => e.City);
                entity.HasIndex(e => e.GooglePlaceId);
                entity.HasIndex(e => e.IsActive);

                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Address).IsRequired();
                entity.Property(e => e.City).IsRequired();
                entity.Property(e => e.Country).HasDefaultValue("United States");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

                // Relationships
                entity.HasOne(e => e.CreatedBy)
                    .WithMany(u => u.CreatedVenues)
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.PremiumListing)
                    .WithOne(p => p.Venue)
                    .HasForeignKey<PremiumListing>(p => p.VenueId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // EscapeRoom Configuration
            modelBuilder.Entity<EscapeRoom>(entity =>
            {
                entity.HasKey(e => e.RoomId);

                entity.HasIndex(e => e.VenueId);
                entity.HasIndex(e => e.Slug).IsUnique();
                entity.HasIndex(e => e.Theme);
                entity.HasIndex(e => e.Difficulty);
                entity.HasIndex(e => e.IsActive);

                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Theme).HasDefaultValue("Mystery");
                entity.Property(e => e.Difficulty).HasDefaultValue(3);
                entity.Property(e => e.MinPlayers).HasDefaultValue(2);
                entity.Property(e => e.MaxPlayers).HasDefaultValue(6);
                entity.Property(e => e.DurationMinutes).HasDefaultValue(60);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(e => e.Venue)
                    .WithMany(v => v.Rooms)
                    .HasForeignKey(e => e.VenueId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Check constraints
                entity.ToTable(t => t.HasCheckConstraint("CK_EscapeRoom_Difficulty", "[Difficulty] >= 1 AND [Difficulty] <= 5"));
                entity.ToTable(t => t.HasCheckConstraint("CK_EscapeRoom_Players", "[MinPlayers] <= [MaxPlayers]"));
            });

            // Review Configuration (Venue-level reviews)
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasKey(e => e.ReviewId);

                entity.HasIndex(e => e.VenueId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.Venue)
                    .WithMany(v => v.Reviews)
                    .HasForeignKey(e => e.VenueId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Reviews)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Rating).IsRequired();
                entity.Property(e => e.HelpfulCount).HasDefaultValue(0);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "[Rating] >= 1 AND [Rating] <= 5"));
            });

            // RoomReview Configuration (Room-level reviews)
            modelBuilder.Entity<RoomReview>(entity =>
            {
                entity.HasKey(e => e.RoomReviewId);

                entity.HasIndex(e => e.RoomId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.Room)
                    .WithMany(r => r.Reviews)
                    .HasForeignKey(e => e.RoomId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.RoomReviews)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Rating).IsRequired();
                entity.Property(e => e.HelpfulCount).HasDefaultValue(0);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.ToTable(t => t.HasCheckConstraint("CK_RoomReview_Rating", "[Rating] >= 1 AND [Rating] <= 5"));
            });

            // Photo Configuration (Venue photos)
            modelBuilder.Entity<Photo>(entity =>
            {
                entity.HasKey(e => e.PhotoId);

                entity.HasIndex(e => e.VenueId);
                entity.HasIndex(e => e.IsApproved);

                entity.HasOne(e => e.Venue)
                    .WithMany(v => v.Photos)
                    .HasForeignKey(e => e.VenueId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.UploadedBy)
                    .WithMany(u => u.Photos)
                    .HasForeignKey(e => e.UploadedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
                entity.Property(e => e.IsApproved).HasDefaultValue(false);
                entity.Property(e => e.UploadedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // RoomPhoto Configuration
            modelBuilder.Entity<RoomPhoto>(entity =>
            {
                entity.HasKey(e => e.RoomPhotoId);

                entity.HasIndex(e => e.RoomId);
                entity.HasIndex(e => e.IsApproved);

                entity.HasOne(e => e.Room)
                    .WithMany(r => r.Photos)
                    .HasForeignKey(e => e.RoomId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.UploadedBy)
                    .WithMany()
                    .HasForeignKey(e => e.UploadedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
                entity.Property(e => e.IsApproved).HasDefaultValue(false);
                entity.Property(e => e.UploadedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // PremiumListing Configuration
            modelBuilder.Entity<PremiumListing>(entity =>
            {
                entity.HasKey(e => e.ListingId);

                entity.HasIndex(e => e.VenueId).IsUnique();
                entity.HasIndex(e => e.IsActive);

                entity.Property(e => e.PlanType).HasDefaultValue("Basic");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // User Configuration (extends Identity)
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.TotalReviews).HasDefaultValue(0);
                entity.Property(e => e.TotalRoomsPlayed).HasDefaultValue(0);
                entity.Property(e => e.TotalEscapes).HasDefaultValue(0);
                entity.Property(e => e.ReputationScore).HasDefaultValue(0);
                entity.Property(e => e.IsVenueOwner).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // BlogPost Configuration
            modelBuilder.Entity<BlogPost>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.Slug).IsUnique();
                entity.HasIndex(e => e.IsPublished);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.PublishedAt);
                entity.HasIndex(e => e.RelatedCity);

                entity.Property(e => e.Title).IsRequired();
                entity.Property(e => e.Slug).IsRequired();
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // ClaimRequest Configuration
            modelBuilder.Entity<ClaimRequest>(entity =>
            {
                entity.HasKey(e => e.ClaimRequestId);

                entity.HasIndex(e => e.VenueId);
                entity.HasIndex(e => e.StripeSessionId);
                entity.HasIndex(e => e.PaymentStatus);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.Venue)
                    .WithMany()
                    .HasForeignKey(e => e.VenueId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.PremiumListing)
                    .WithMany()
                    .HasForeignKey(e => e.PremiumListingId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.Property(e => e.ContactName).IsRequired();
                entity.Property(e => e.ContactEmail).IsRequired();
                entity.Property(e => e.PlanType).HasDefaultValue("Premium");
                entity.Property(e => e.PaymentStatus).HasDefaultValue("pending");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // Invoice Configuration
            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasKey(e => e.InvoiceId);

                entity.HasIndex(e => e.InvoiceNumber).IsUnique();
                entity.HasIndex(e => e.VenueId);
                entity.HasIndex(e => e.ClaimRequestId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.Venue)
                    .WithMany()
                    .HasForeignKey(e => e.VenueId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ClaimRequest)
                    .WithMany()
                    .HasForeignKey(e => e.ClaimRequestId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.InvoiceNumber).IsRequired();
                entity.Property(e => e.Status).HasDefaultValue("pending");
                entity.Property(e => e.Currency).HasDefaultValue("USD");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // AffiliateClick Configuration
            modelBuilder.Entity<AffiliateClick>(entity =>
            {
                entity.HasKey(e => e.ClickId);

                entity.HasIndex(e => e.RoomId);
                entity.HasIndex(e => e.VenueId);
                entity.HasIndex(e => e.ClickedAt);

                entity.HasOne(e => e.Room)
                    .WithMany()
                    .HasForeignKey(e => e.RoomId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Venue)
                    .WithMany()
                    .HasForeignKey(e => e.VenueId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.ClickedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // Booking Configuration
            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasKey(e => e.BookingId);

                entity.HasIndex(e => e.RoomId);
                entity.HasIndex(e => e.VenueId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.BookingDate);
                entity.HasIndex(e => e.Status);

                entity.HasOne(e => e.Room)
                    .WithMany()
                    .HasForeignKey(e => e.RoomId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Venue)
                    .WithMany()
                    .HasForeignKey(e => e.VenueId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Bookings)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.Status).HasDefaultValue("clicked");
                entity.Property(e => e.CommissionPaid).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}
