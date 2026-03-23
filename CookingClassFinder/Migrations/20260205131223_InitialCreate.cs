using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookingClassFinder.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FavoriteCuisines = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DietaryPreferences = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalReviews = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalClassesTaken = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalRecipesLearned = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ReputationScore = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsSchoolOwner = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SchoolId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlogPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FeaturedImage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MetaTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Author = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsAutoGenerated = table.Column<bool>(type: "bit", nullable: false),
                    RelatedCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RelatedCountry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RelatedCuisine = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ViewCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    CityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "United States"),
                    Region = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "US"),
                    Latitude = table.Column<decimal>(type: "decimal(10,8)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GuideContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    SchoolCount = table.Column<int>(type: "int", nullable: false),
                    CrawlCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastCrawledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastCrawlStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NextCrawlAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MaxResults = table.Column<int>(type: "int", nullable: false, defaultValue: 15),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.CityId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Schools",
                columns: table => new
                {
                    SchoolId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "United States"),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,8)", nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(11,8)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BookingUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OpeningHours = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttributesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriceRange = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TotalClasses = table.Column<int>(type: "int", nullable: false),
                    MaxStudentsPerClass = table.Column<int>(type: "int", nullable: false),
                    HasProfessionalKitchen = table.Column<bool>(type: "bit", nullable: false),
                    OffersPrivateClasses = table.Column<bool>(type: "bit", nullable: false),
                    OffersCorporateEvents = table.Column<bool>(type: "bit", nullable: false),
                    OffersKidsClasses = table.Column<bool>(type: "bit", nullable: false),
                    OffersCouplesClasses = table.Column<bool>(type: "bit", nullable: false),
                    HasParking = table.Column<bool>(type: "bit", nullable: false),
                    IsWheelchairAccessible = table.Column<bool>(type: "bit", nullable: false),
                    ProvidesCertificates = table.Column<bool>(type: "bit", nullable: false),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    CuisineSpecialties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GooglePlaceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    YelpBusinessId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GoogleMapsUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LocalImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AverageRating = table.Column<decimal>(type: "decimal(3,2)", nullable: true),
                    TotalReviews = table.Column<int>(type: "int", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    IsPremium = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MetaDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schools", x => x.SchoolId);
                    table.ForeignKey(
                        name: "FK_Schools_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CrawlHistories",
                columns: table => new
                {
                    CrawlHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CityId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "InProgress"),
                    SchoolsFound = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SchoolsAdded = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SchoolsUpdated = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrawlHistories", x => x.CrawlHistoryId);
                    table.ForeignKey(
                        name: "FK_CrawlHistories_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    ClassId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CuisineType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Other"),
                    CuisineDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DifficultyLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "All Levels"),
                    MinStudents = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    MaxStudents = table.Column<int>(type: "int", nullable: false, defaultValue: 12),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 120),
                    PricePerPerson = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    PriceForPrivateGroup = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    IngredientsProvided = table.Column<bool>(type: "bit", nullable: false),
                    MealIncluded = table.Column<bool>(type: "bit", nullable: false),
                    TakeHomeRecipes = table.Column<bool>(type: "bit", nullable: false),
                    WineParingIncluded = table.Column<bool>(type: "bit", nullable: false),
                    ApronProvided = table.Column<bool>(type: "bit", nullable: false),
                    HandsOnCooking = table.Column<bool>(type: "bit", nullable: false),
                    IsDemonstrationOnly = table.Column<bool>(type: "bit", nullable: false),
                    IsVegetarian = table.Column<bool>(type: "bit", nullable: false),
                    IsVegan = table.Column<bool>(type: "bit", nullable: false),
                    IsGlutenFree = table.Column<bool>(type: "bit", nullable: false),
                    IsHalal = table.Column<bool>(type: "bit", nullable: false),
                    IsKosher = table.Column<bool>(type: "bit", nullable: false),
                    CanAccommodateDietary = table.Column<bool>(type: "bit", nullable: false),
                    IsKidsFriendly = table.Column<bool>(type: "bit", nullable: false),
                    IsCouplesClass = table.Column<bool>(type: "bit", nullable: false),
                    IsTeamBuilding = table.Column<bool>(type: "bit", nullable: false),
                    IsPrivateAvailable = table.Column<bool>(type: "bit", nullable: false),
                    IsOnline = table.Column<bool>(type: "bit", nullable: false),
                    MinAge = table.Column<int>(type: "int", nullable: true),
                    MinAgeWithAdult = table.Column<int>(type: "int", nullable: true),
                    WhatToBring = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WhatYouLearn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InstructorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InstructorBio = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InstructorImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecurringSchedule = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AvailableLanguages = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BookingUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LocalImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AverageRating = table.Column<decimal>(type: "decimal(3,2)", nullable: true),
                    TotalReviews = table.Column<int>(type: "int", nullable: false),
                    TotalBookings = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsNew = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Slug = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.ClassId);
                    table.CheckConstraint("CK_CookingClass_Students", "[MinStudents] <= [MaxStudents]");
                    table.ForeignKey(
                        name: "FK_Classes_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Photos",
                columns: table => new
                {
                    PhotoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Caption = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LocalPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Photos", x => x.PhotoId);
                    table.ForeignKey(
                        name: "FK_Photos_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Photos_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PremiumListings",
                columns: table => new
                {
                    ListingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    PlanType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Basic"),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MonthlyFee = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    FeaturedPlacement = table.Column<bool>(type: "bit", nullable: false),
                    PhotoGallery = table.Column<bool>(type: "bit", nullable: false),
                    ClassShowcase = table.Column<bool>(type: "bit", nullable: false),
                    BookingIntegration = table.Column<bool>(type: "bit", nullable: false),
                    AnalyticsDashboard = table.Column<bool>(type: "bit", nullable: false),
                    PrioritySupport = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiumListings", x => x.ListingId);
                    table.ForeignKey(
                        name: "FK_PremiumListings_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    ReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    VisitDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsVerifiedVisit = table.Column<bool>(type: "bit", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    HelpfulCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.ReviewId);
                    table.CheckConstraint("CK_Review_Rating", "[Rating] >= 1 AND [Rating] <= 5");
                    table.ForeignKey(
                        name: "FK_Reviews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reviews_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateClicks",
                columns: table => new
                {
                    ClickId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: true),
                    ClassId = table.Column<int>(type: "int", nullable: true),
                    LinkType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DestinationUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReferrerPage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClickedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateClicks", x => x.ClickId);
                    table.ForeignKey(
                        name: "FK_AffiliateClicks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AffiliateClicks_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "ClassId");
                    table.ForeignKey(
                        name: "FK_AffiliateClicks_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "SchoolId");
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    BookingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    BookingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeSlot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PartySize = table.Column<int>(type: "int", nullable: true),
                    ReferralSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AffiliateCode = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalBookingId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BookingUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "clicked"),
                    BookingValue = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CommissionAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CommissionPaid = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GuestName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    BookedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.BookingId);
                    table.ForeignKey(
                        name: "FK_Bookings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Bookings_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookings_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "SchoolId");
                });

            migrationBuilder.CreateTable(
                name: "ClassPhotos",
                columns: table => new
                {
                    ClassPhotoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Caption = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LocalPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PhotoType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassPhotos", x => x.ClassPhotoId);
                    table.ForeignKey(
                        name: "FK_ClassPhotos_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClassPhotos_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassReviews",
                columns: table => new
                {
                    ClassReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClassId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    InstructorRating = table.Column<int>(type: "int", nullable: true),
                    RecipeClarityRating = table.Column<int>(type: "int", nullable: true),
                    FacilitiesRating = table.Column<int>(type: "int", nullable: true),
                    ValueForMoneyRating = table.Column<int>(type: "int", nullable: true),
                    FoodQualityRating = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    DidCompleteRecipe = table.Column<bool>(type: "bit", nullable: true),
                    WouldRecommend = table.Column<bool>(type: "bit", nullable: true),
                    WouldTakeAgain = table.Column<bool>(type: "bit", nullable: true),
                    PerceivedDifficulty = table.Column<int>(type: "int", nullable: true),
                    TipsForFutureStudents = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FavoritePartOfClass = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClassDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsVerifiedAttendance = table.Column<bool>(type: "bit", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    HelpfulCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassReviews", x => x.ClassReviewId);
                    table.CheckConstraint("CK_ClassReview_Rating", "[Rating] >= 1 AND [Rating] <= 5");
                    table.ForeignKey(
                        name: "FK_ClassReviews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassReviews_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "ClassId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimRequests",
                columns: table => new
                {
                    ClaimRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlanType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Premium"),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    MonthlyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VerificationMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    PayPalOrderId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StripeSessionId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StripeCustomerId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PremiumListingId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimRequests", x => x.ClaimRequestId);
                    table.ForeignKey(
                        name: "FK_ClaimRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClaimRequests_PremiumListings_PremiumListingId",
                        column: x => x.PremiumListingId,
                        principalTable: "PremiumListings",
                        principalColumn: "ListingId");
                    table.ForeignKey(
                        name: "FK_ClaimRequests_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    InvoiceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClaimRequestId = table.Column<int>(type: "int", nullable: true),
                    SchoolId = table.Column<int>(type: "int", nullable: true),
                    PremiumListingId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BillingAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentIntentId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceId);
                    table.ForeignKey(
                        name: "FK_Invoices_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invoices_ClaimRequests_ClaimRequestId",
                        column: x => x.ClaimRequestId,
                        principalTable: "ClaimRequests",
                        principalColumn: "ClaimRequestId");
                    table.ForeignKey(
                        name: "FK_Invoices_PremiumListings_PremiumListingId",
                        column: x => x.PremiumListingId,
                        principalTable: "PremiumListings",
                        principalColumn: "ListingId");
                    table.ForeignKey(
                        name: "FK_Invoices_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "SchoolId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateClicks_ClassId",
                table: "AffiliateClicks",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateClicks_ClickedAt",
                table: "AffiliateClicks",
                column: "ClickedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateClicks_SchoolId",
                table: "AffiliateClicks",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateClicks_UserId",
                table: "AffiliateClicks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_Category",
                table: "BlogPosts",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_IsPublished",
                table: "BlogPosts",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_PublishedAt",
                table: "BlogPosts",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_RelatedCity",
                table: "BlogPosts",
                column: "RelatedCity");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_RelatedCuisine",
                table: "BlogPosts",
                column: "RelatedCuisine");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_Slug",
                table: "BlogPosts",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BookingDate",
                table: "Bookings",
                column: "BookingDate");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClassId",
                table: "Bookings",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SchoolId",
                table: "Bookings",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status",
                table: "Bookings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_UserId",
                table: "Bookings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_IsActive",
                table: "Cities",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_LastCrawlStatus",
                table: "Cities",
                column: "LastCrawlStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_Name_Country",
                table: "Cities",
                columns: new[] { "Name", "Country" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cities_NextCrawlAt",
                table: "Cities",
                column: "NextCrawlAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRequests_CreatedAt",
                table: "ClaimRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRequests_PaymentStatus",
                table: "ClaimRequests",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRequests_PremiumListingId",
                table: "ClaimRequests",
                column: "PremiumListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRequests_SchoolId",
                table: "ClaimRequests",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRequests_StripeSessionId",
                table: "ClaimRequests",
                column: "StripeSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRequests_UserId",
                table: "ClaimRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_CuisineType",
                table: "Classes",
                column: "CuisineType");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_DifficultyLevel",
                table: "Classes",
                column: "DifficultyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_IsActive",
                table: "Classes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_SchoolId",
                table: "Classes",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_Slug",
                table: "Classes",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassPhotos_ClassId",
                table: "ClassPhotos",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPhotos_IsApproved",
                table: "ClassPhotos",
                column: "IsApproved");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPhotos_UploadedByUserId",
                table: "ClassPhotos",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassReviews_ClassId",
                table: "ClassReviews",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassReviews_CreatedAt",
                table: "ClassReviews",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClassReviews_UserId",
                table: "ClassReviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlHistories_CityId",
                table: "CrawlHistories",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlHistories_StartedAt",
                table: "CrawlHistories",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlHistories_Status",
                table: "CrawlHistories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClaimRequestId",
                table: "Invoices",
                column: "ClaimRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreatedAt",
                table: "Invoices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PremiumListingId",
                table: "Invoices",
                column: "PremiumListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SchoolId",
                table: "Invoices",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_UserId",
                table: "Invoices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_IsApproved",
                table: "Photos",
                column: "IsApproved");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_SchoolId",
                table: "Photos",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_UploadedByUserId",
                table: "Photos",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiumListings_IsActive",
                table: "PremiumListings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PremiumListings_SchoolId",
                table: "PremiumListings",
                column: "SchoolId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CreatedAt",
                table: "Reviews",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_SchoolId",
                table: "Reviews",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_City",
                table: "Schools",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_CreatedByUserId",
                table: "Schools",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_GooglePlaceId",
                table: "Schools",
                column: "GooglePlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_IsActive",
                table: "Schools",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Schools_Latitude_Longitude",
                table: "Schools",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Schools_Slug",
                table: "Schools",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateClicks");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BlogPosts");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "ClassPhotos");

            migrationBuilder.DropTable(
                name: "ClassReviews");

            migrationBuilder.DropTable(
                name: "CrawlHistories");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Photos");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Classes");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropTable(
                name: "ClaimRequests");

            migrationBuilder.DropTable(
                name: "PremiumListings");

            migrationBuilder.DropTable(
                name: "Schools");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
