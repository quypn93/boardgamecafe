using BoardGameCafeFinder.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets
        public DbSet<Cafe> Cafes { get; set; }
        public DbSet<BoardGame> BoardGames { get; set; }
        public DbSet<CafeGame> CafeGames { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<EventBooking> EventBookings { get; set; }
        public DbSet<Photo> Photos { get; set; }
        public DbSet<PremiumListing> PremiumListings { get; set; }
        public DbSet<BlogPost> BlogPosts { get; set; }
        public DbSet<ClaimRequest> ClaimRequests { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<AffiliateClick> AffiliateClicks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cafe Configuration
            modelBuilder.Entity<Cafe>(entity =>
            {
                entity.HasKey(e => e.CafeId);

                entity.HasIndex(e => e.Slug).IsUnique();
                entity.HasIndex(e => new { e.Latitude, e.Longitude });
                entity.HasIndex(e => e.City);
                entity.HasIndex(e => e.GooglePlaceId);
                entity.HasIndex(e => e.BggUsername);
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
                    .WithMany(u => u.CreatedCafes)
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.PremiumListing)
                    .WithOne(p => p.Cafe)
                    .HasForeignKey<PremiumListing>(p => p.CafeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // BoardGame Configuration
            modelBuilder.Entity<BoardGame>(entity =>
            {
                entity.HasKey(e => e.GameId);

                entity.HasIndex(e => e.BGGId);
                entity.HasIndex(e => e.Name);

                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // CafeGame Configuration (Many-to-Many)
            modelBuilder.Entity<CafeGame>(entity =>
            {
                entity.HasKey(e => e.CafeGameId);

                entity.HasIndex(e => new { e.CafeId, e.GameId }).IsUnique();

                entity.HasOne(e => e.Cafe)
                    .WithMany(c => c.CafeGames)
                    .HasForeignKey(e => e.CafeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Game)
                    .WithMany(g => g.CafeGames)
                    .HasForeignKey(e => e.GameId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.VerifiedBy)
                    .WithMany()
                    .HasForeignKey(e => e.VerifiedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.IsAvailable).HasDefaultValue(true);
                entity.Property(e => e.Quantity).HasDefaultValue(1);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // Review Configuration
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasKey(e => e.ReviewId);

                entity.HasIndex(e => e.CafeId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.Cafe)
                    .WithMany(c => c.Reviews)
                    .HasForeignKey(e => e.CafeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Reviews)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Rating).IsRequired();
                entity.Property(e => e.HelpfulCount).HasDefaultValue(0);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

                // Check constraint for rating using EF Core 8 syntax
                entity.ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "[Rating] >= 1 AND [Rating] <= 5"));
            });

            // Event Configuration
            modelBuilder.Entity<Event>(entity =>
            {
                entity.HasKey(e => e.EventId);

                entity.HasIndex(e => e.CafeId);
                entity.HasIndex(e => e.StartDateTime);
                entity.HasIndex(e => e.IsActive);

                entity.HasOne(e => e.Cafe)
                    .WithMany(c => c.Events)
                    .HasForeignKey(e => e.CafeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Title).IsRequired();
                entity.Property(e => e.CurrentParticipants).HasDefaultValue(0);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // EventBooking Configuration
            modelBuilder.Entity<EventBooking>(entity =>
            {
                entity.HasKey(e => e.BookingId);

                entity.HasIndex(e => e.EventId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => new { e.EventId, e.UserId });

                entity.HasOne(e => e.Event)
                    .WithMany(ev => ev.Bookings)
                    .HasForeignKey(e => e.EventId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.EventBookings)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.NumberOfSeats).HasDefaultValue(1);
                entity.Property(e => e.Status).HasDefaultValue("Confirmed");
                entity.Property(e => e.PaymentStatus).HasDefaultValue("Pending");
                entity.Property(e => e.BookingDate).HasDefaultValueSql("GETUTCDATE()");
            });

            // Photo Configuration
            modelBuilder.Entity<Photo>(entity =>
            {
                entity.HasKey(e => e.PhotoId);

                entity.HasIndex(e => e.CafeId);
                entity.HasIndex(e => e.IsApproved);

                entity.HasOne(e => e.Cafe)
                    .WithMany(c => c.Photos)
                    .HasForeignKey(e => e.CafeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.UploadedBy)
                    .WithMany(u => u.Photos)
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

                entity.HasIndex(e => e.CafeId).IsUnique();
                entity.HasIndex(e => e.IsActive);

                entity.Property(e => e.PlanType).HasDefaultValue("Basic");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // User Configuration (extends Identity)
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.TotalReviews).HasDefaultValue(0);
                entity.Property(e => e.TotalBookings).HasDefaultValue(0);
                entity.Property(e => e.ReputationScore).HasDefaultValue(0);
                entity.Property(e => e.IsCafeOwner).HasDefaultValue(false);
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

                entity.HasIndex(e => e.CafeId);
                entity.HasIndex(e => e.StripeSessionId);
                entity.HasIndex(e => e.PaymentStatus);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.Cafe)
                    .WithMany()
                    .HasForeignKey(e => e.CafeId)
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
                entity.HasIndex(e => e.CafeId);
                entity.HasIndex(e => e.ClaimRequestId);
                entity.HasIndex(e => e.PaymentStatus);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.Cafe)
                    .WithMany()
                    .HasForeignKey(e => e.CafeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ClaimRequest)
                    .WithMany()
                    .HasForeignKey(e => e.ClaimRequestId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.PremiumListing)
                    .WithMany()
                    .HasForeignKey(e => e.PremiumListingId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.Property(e => e.InvoiceNumber).IsRequired();
                entity.Property(e => e.BillingName).IsRequired();
                entity.Property(e => e.BillingEmail).IsRequired();
                entity.Property(e => e.PaymentStatus).HasDefaultValue("pending");
                entity.Property(e => e.Currency).HasDefaultValue("USD");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // AffiliateClick Configuration
            modelBuilder.Entity<AffiliateClick>(entity =>
            {
                entity.HasKey(e => e.ClickId);

                entity.HasIndex(e => e.GameId);
                entity.HasIndex(e => e.CafeId);
                entity.HasIndex(e => e.ClickedAt);

                entity.HasOne(e => e.Game)
                    .WithMany()
                    .HasForeignKey(e => e.GameId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Cafe)
                    .WithMany()
                    .HasForeignKey(e => e.CafeId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.ClickedAt).HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}
