using CookingClassFinder.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CookingClassFinder.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets
        public DbSet<CookingSchool> Schools { get; set; }
        public DbSet<CookingClass> Classes { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<ClassReview> ClassReviews { get; set; }
        public DbSet<Photo> Photos { get; set; }
        public DbSet<ClassPhoto> ClassPhotos { get; set; }
        public DbSet<PremiumListing> PremiumListings { get; set; }
        public DbSet<BlogPost> BlogPosts { get; set; }
        public DbSet<ClaimRequest> ClaimRequests { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<AffiliateClick> AffiliateClicks { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<City> Cities { get; set; }
        public DbSet<CrawlHistory> CrawlHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // CookingSchool Configuration
            modelBuilder.Entity<CookingSchool>(entity =>
            {
                entity.HasKey(e => e.SchoolId);

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
                    .WithMany(u => u.CreatedSchools)
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.PremiumListing)
                    .WithOne(p => p.School)
                    .HasForeignKey<PremiumListing>(p => p.SchoolId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // CookingClass Configuration
            modelBuilder.Entity<CookingClass>(entity =>
            {
                entity.HasKey(e => e.ClassId);

                entity.HasIndex(e => e.SchoolId);
                entity.HasIndex(e => e.Slug).IsUnique();
                entity.HasIndex(e => e.CuisineType);
                entity.HasIndex(e => e.DifficultyLevel);
                entity.HasIndex(e => e.IsActive);

                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.CuisineType).HasDefaultValue("Other");
                entity.Property(e => e.DifficultyLevel).HasDefaultValue("All Levels");
                entity.Property(e => e.MinStudents).HasDefaultValue(1);
                entity.Property(e => e.MaxStudents).HasDefaultValue(12);
                entity.Property(e => e.DurationMinutes).HasDefaultValue(120);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(e => e.School)
                    .WithMany(s => s.Classes)
                    .HasForeignKey(e => e.SchoolId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Check constraints
                entity.ToTable(t => t.HasCheckConstraint("CK_CookingClass_Students", "[MinStudents] <= [MaxStudents]"));
            });

            // Review Configuration (School-level reviews)
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasKey(e => e.ReviewId);

                entity.HasIndex(e => e.SchoolId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.School)
                    .WithMany(s => s.Reviews)
                    .HasForeignKey(e => e.SchoolId)
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

            // ClassReview Configuration (Class-level reviews)
            modelBuilder.Entity<ClassReview>(entity =>
            {
                entity.HasKey(e => e.ClassReviewId);

                entity.HasIndex(e => e.ClassId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.Class)
                    .WithMany(c => c.Reviews)
                    .HasForeignKey(e => e.ClassId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.ClassReviews)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Rating).IsRequired();
                entity.Property(e => e.HelpfulCount).HasDefaultValue(0);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.ToTable(t => t.HasCheckConstraint("CK_ClassReview_Rating", "[Rating] >= 1 AND [Rating] <= 5"));
            });

            // Photo Configuration (School photos)
            modelBuilder.Entity<Photo>(entity =>
            {
                entity.HasKey(e => e.PhotoId);

                entity.HasIndex(e => e.SchoolId);
                entity.HasIndex(e => e.IsApproved);

                entity.HasOne(e => e.School)
                    .WithMany(s => s.Photos)
                    .HasForeignKey(e => e.SchoolId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.UploadedBy)
                    .WithMany(u => u.Photos)
                    .HasForeignKey(e => e.UploadedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
                entity.Property(e => e.IsApproved).HasDefaultValue(false);
                entity.Property(e => e.UploadedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // ClassPhoto Configuration
            modelBuilder.Entity<ClassPhoto>(entity =>
            {
                entity.HasKey(e => e.ClassPhotoId);

                entity.HasIndex(e => e.ClassId);
                entity.HasIndex(e => e.IsApproved);

                entity.HasOne(e => e.Class)
                    .WithMany(c => c.Photos)
                    .HasForeignKey(e => e.ClassId)
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

                entity.HasIndex(e => e.SchoolId).IsUnique();
                entity.HasIndex(e => e.IsActive);

                entity.Property(e => e.PlanType).HasDefaultValue("Basic");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // User Configuration (extends Identity)
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.TotalReviews).HasDefaultValue(0);
                entity.Property(e => e.TotalClassesTaken).HasDefaultValue(0);
                entity.Property(e => e.TotalRecipesLearned).HasDefaultValue(0);
                entity.Property(e => e.ReputationScore).HasDefaultValue(0);
                entity.Property(e => e.IsSchoolOwner).HasDefaultValue(false);
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
                entity.HasIndex(e => e.RelatedCuisine);

                entity.Property(e => e.Title).IsRequired();
                entity.Property(e => e.Slug).IsRequired();
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // ClaimRequest Configuration
            modelBuilder.Entity<ClaimRequest>(entity =>
            {
                entity.HasKey(e => e.ClaimRequestId);

                entity.HasIndex(e => e.SchoolId);
                entity.HasIndex(e => e.StripeSessionId);
                entity.HasIndex(e => e.PaymentStatus);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.School)
                    .WithMany()
                    .HasForeignKey(e => e.SchoolId)
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
                entity.HasIndex(e => e.SchoolId);
                entity.HasIndex(e => e.ClaimRequestId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.School)
                    .WithMany()
                    .HasForeignKey(e => e.SchoolId)
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

                entity.HasIndex(e => e.ClassId);
                entity.HasIndex(e => e.SchoolId);
                entity.HasIndex(e => e.ClickedAt);

                entity.HasOne(e => e.Class)
                    .WithMany()
                    .HasForeignKey(e => e.ClassId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.School)
                    .WithMany()
                    .HasForeignKey(e => e.SchoolId)
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

                entity.HasIndex(e => e.ClassId);
                entity.HasIndex(e => e.SchoolId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.BookingDate);
                entity.HasIndex(e => e.Status);

                entity.HasOne(e => e.Class)
                    .WithMany()
                    .HasForeignKey(e => e.ClassId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.School)
                    .WithMany()
                    .HasForeignKey(e => e.SchoolId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Bookings)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.Status).HasDefaultValue("clicked");
                entity.Property(e => e.CommissionPaid).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // City Configuration
            modelBuilder.Entity<City>(entity =>
            {
                entity.HasKey(e => e.CityId);

                entity.HasIndex(e => new { e.Name, e.Country }).IsUnique();
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.LastCrawlStatus);
                entity.HasIndex(e => e.NextCrawlAt);

                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Country).HasDefaultValue("United States");
                entity.Property(e => e.Region).HasDefaultValue("US");
                entity.Property(e => e.CrawlCount).HasDefaultValue(0);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.MaxResults).HasDefaultValue(15);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // CrawlHistory Configuration
            modelBuilder.Entity<CrawlHistory>(entity =>
            {
                entity.HasKey(e => e.CrawlHistoryId);

                entity.HasIndex(e => e.CityId);
                entity.HasIndex(e => e.StartedAt);
                entity.HasIndex(e => e.Status);

                entity.HasOne(e => e.City)
                    .WithMany(c => c.CrawlHistories)
                    .HasForeignKey(e => e.CityId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.Status).HasDefaultValue("InProgress");
                entity.Property(e => e.SchoolsFound).HasDefaultValue(0);
                entity.Property(e => e.SchoolsAdded).HasDefaultValue(0);
                entity.Property(e => e.SchoolsUpdated).HasDefaultValue(0);
                entity.Property(e => e.StartedAt).HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}
