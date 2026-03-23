using CookingClassFinder.Models.Domain;
using System.Text.Json;

namespace CookingClassFinder.Tests.Models;

public class DomainModelTests
{
    #region CookingSchool

    [Fact]
    public void CookingSchool_DefaultValues_AreCorrect()
    {
        var school = new CookingSchool();

        Assert.True(school.IsActive);
        Assert.False(school.IsPremium);
        Assert.False(school.IsVerified);
        Assert.Equal("United States", school.Country);
        Assert.Equal(0, school.TotalReviews);
        Assert.Equal(0, school.TotalClasses);
    }

    [Fact]
    public void CookingSchool_GetCuisineSpecialtiesList_WithValidJson()
    {
        var school = new CookingSchool();
        school.CuisineSpecialties = "[\"Italian\",\"French\",\"Thai\"]";

        var result = school.GetCuisineSpecialtiesList();

        Assert.Equal(3, result.Count);
        Assert.Contains("Italian", result);
        Assert.Contains("French", result);
        Assert.Contains("Thai", result);
    }

    [Fact]
    public void CookingSchool_GetCuisineSpecialtiesList_NullJson_ReturnsEmpty()
    {
        var school = new CookingSchool { CuisineSpecialties = null };
        Assert.Empty(school.GetCuisineSpecialtiesList());
    }

    [Fact]
    public void CookingSchool_GetCuisineSpecialtiesList_InvalidJson_ReturnsEmpty()
    {
        var school = new CookingSchool { CuisineSpecialties = "not json" };
        Assert.Empty(school.GetCuisineSpecialtiesList());
    }

    [Fact]
    public void CookingSchool_SetCuisineSpecialties_SerializesCorrectly()
    {
        var school = new CookingSchool();
        school.SetCuisineSpecialties(new List<string> { "Italian", "French" });

        var result = school.GetCuisineSpecialtiesList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void CookingSchool_GetAttributes_ValidJson_ReturnsDict()
    {
        var school = new CookingSchool();
        var attrs = new Dictionary<string, List<string>>
        {
            ["parking"] = new List<string> { "street", "garage" }
        };
        school.SetAttributes(attrs);

        var result = school.GetAttributes();
        Assert.NotNull(result);
        Assert.Contains("parking", result.Keys);
    }

    [Fact]
    public void CookingSchool_GetAttributes_Null_ReturnsNull()
    {
        var school = new CookingSchool { AttributesJson = null };
        Assert.Null(school.GetAttributes());
    }

    [Fact]
    public void CookingSchool_GetOpeningHours_Null_ReturnsNull()
    {
        var school = new CookingSchool { OpeningHours = null };
        Assert.Null(school.GetOpeningHours());
    }

    [Fact]
    public void CookingSchool_GetOpeningHours_ValidJson_ReturnsHours()
    {
        var hours = new List<OpeningHourPeriod>
        {
            new() { DayOfWeek = 1, OpenMinutes = 540, CloseMinutes = 1080 }
        };
        var school = new CookingSchool
        {
            OpeningHours = JsonSerializer.Serialize(hours)
        };

        var result = school.GetOpeningHours();
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(1, result[0].DayOfWeek);
    }

    [Fact]
    public void CookingSchool_IsOpenNow_NullHours_ReturnsFalse()
    {
        var school = new CookingSchool { OpeningHours = null };
        Assert.False(school.IsOpenNow());
    }

    [Fact]
    public void CookingSchool_IsOpenNow_InvalidJson_ReturnsFalse()
    {
        var school = new CookingSchool { OpeningHours = "invalid" };
        Assert.False(school.IsOpenNow());
    }

    [Fact]
    public void CookingSchool_GetAverageClassRating_NoClasses_ReturnsZero()
    {
        var school = new CookingSchool();
        Assert.Equal(0, school.GetAverageClassRating());
    }

    [Fact]
    public void CookingSchool_GetAverageClassRating_WithRatedClasses_CalculatesAverage()
    {
        var school = new CookingSchool
        {
            Classes = new List<CookingClass>
            {
                new() { AverageRating = 4.0m },
                new() { AverageRating = 5.0m },
                new() { AverageRating = null } // Should be excluded
            }
        };

        Assert.Equal(4.5, school.GetAverageClassRating());
    }

    #endregion

    #region CookingClass

    [Fact]
    public void CookingClass_DefaultValues_AreCorrect()
    {
        var cc = new CookingClass();

        Assert.True(cc.IsActive);
        Assert.Equal(CuisineTypes.Other, cc.CuisineType);
        Assert.Equal(DifficultyLevels.AllLevels, cc.DifficultyLevel);
        Assert.Equal(1, cc.MinStudents);
        Assert.Equal(12, cc.MaxStudents);
        Assert.Equal(120, cc.DurationMinutes);
        Assert.True(cc.IngredientsProvided);
        Assert.True(cc.MealIncluded);
        Assert.True(cc.HandsOnCooking);
    }

    [Fact]
    public void CookingClass_GetDifficultyBadgeClass_ReturnsCorrectClass()
    {
        Assert.Equal("bg-success", new CookingClass { DifficultyLevel = DifficultyLevels.Beginner }.GetDifficultyBadgeClass());
        Assert.Equal("bg-warning", new CookingClass { DifficultyLevel = DifficultyLevels.Intermediate }.GetDifficultyBadgeClass());
        Assert.Equal("bg-danger", new CookingClass { DifficultyLevel = DifficultyLevels.Advanced }.GetDifficultyBadgeClass());
        Assert.Equal("bg-info", new CookingClass { DifficultyLevel = DifficultyLevels.AllLevels }.GetDifficultyBadgeClass());
        Assert.Equal("bg-secondary", new CookingClass { DifficultyLevel = "Unknown" }.GetDifficultyBadgeClass());
    }

    [Fact]
    public void CookingClass_GetStudentRange_SameMinMax()
    {
        var cc = new CookingClass { MinStudents = 5, MaxStudents = 5 };
        Assert.Equal("5 students", cc.GetStudentRange());
    }

    [Fact]
    public void CookingClass_GetStudentRange_DifferentMinMax()
    {
        var cc = new CookingClass { MinStudents = 2, MaxStudents = 12 };
        Assert.Equal("2-12 students", cc.GetStudentRange());
    }

    [Fact]
    public void CookingClass_GetDurationDisplay_LessThanHour()
    {
        var cc = new CookingClass { DurationMinutes = 45 };
        Assert.Equal("45 min", cc.GetDurationDisplay());
    }

    [Fact]
    public void CookingClass_GetDurationDisplay_ExactHours()
    {
        Assert.Equal("1 hour", new CookingClass { DurationMinutes = 60 }.GetDurationDisplay());
        Assert.Equal("2 hours", new CookingClass { DurationMinutes = 120 }.GetDurationDisplay());
    }

    [Fact]
    public void CookingClass_GetDurationDisplay_HoursAndMinutes()
    {
        var cc = new CookingClass { DurationMinutes = 150 };
        Assert.Equal("2h 30m", cc.GetDurationDisplay());
    }

    [Fact]
    public void CookingClass_GetPriceDisplay_WithPricePerPerson()
    {
        var cc = new CookingClass { PricePerPerson = 75m };
        Assert.Equal("$75/person", cc.GetPriceDisplay());
    }

    [Fact]
    public void CookingClass_GetPriceDisplay_WithGroupPrice()
    {
        var cc = new CookingClass { PriceForPrivateGroup = 500m };
        Assert.Equal("$500/group", cc.GetPriceDisplay());
    }

    [Fact]
    public void CookingClass_GetPriceDisplay_NoPrice()
    {
        var cc = new CookingClass();
        Assert.Equal("Contact for pricing", cc.GetPriceDisplay());
    }

    [Fact]
    public void CookingClass_GetFeatureTags_ReturnsCorrectTags()
    {
        var cc = new CookingClass
        {
            HandsOnCooking = true,
            MealIncluded = true,
            IsVegetarian = true,
            IsOnline = true
        };

        var tags = cc.GetFeatureTags();

        Assert.Contains("Hands-on", tags);
        Assert.Contains("Meal Included", tags);
        Assert.Contains("Vegetarian", tags);
        Assert.Contains("Online", tags);
    }

    [Fact]
    public void CookingClass_GetDietaryTags_ReturnsCorrectTags()
    {
        var cc = new CookingClass
        {
            IsVegan = true,
            IsGlutenFree = true,
            IsHalal = true
        };

        var tags = cc.GetDietaryTags();

        Assert.Contains("Vegan", tags);
        Assert.Contains("Gluten-Free", tags);
        Assert.Contains("Halal", tags);
    }

    #endregion

    #region User

    [Fact]
    public void User_GetFullName_FirstAndLastName()
    {
        var user = new User { FirstName = "John", LastName = "Doe" };
        Assert.Equal("John Doe", user.GetFullName());
    }

    [Fact]
    public void User_GetFullName_DisplayName()
    {
        var user = new User { DisplayName = "JohnD" };
        Assert.Equal("JohnD", user.GetFullName());
    }

    [Fact]
    public void User_GetFullName_UserName()
    {
        var user = new User { UserName = "johnd" };
        Assert.Equal("johnd", user.GetFullName());
    }

    [Fact]
    public void User_GetFullName_NoName_ReturnsAnonymous()
    {
        var user = new User();
        Assert.Equal("Anonymous", user.GetFullName());
    }

    [Fact]
    public void User_GetCookingLevel_ReturnsCorrectLevels()
    {
        Assert.Equal("New Member", new User { TotalClassesTaken = 0 }.GetCookingLevel());
        Assert.Equal("Beginner", new User { TotalClassesTaken = 1 }.GetCookingLevel());
        Assert.Equal("Cooking Enthusiast", new User { TotalClassesTaken = 5 }.GetCookingLevel());
        Assert.Equal("Home Chef", new User { TotalClassesTaken = 10 }.GetCookingLevel());
        Assert.Equal("Experienced Cook", new User { TotalClassesTaken = 25 }.GetCookingLevel());
        Assert.Equal("Master Chef", new User { TotalClassesTaken = 50 }.GetCookingLevel());
    }

    [Fact]
    public void User_DefaultValues_AreCorrect()
    {
        var user = new User();

        Assert.Equal(0, user.TotalReviews);
        Assert.Equal(0, user.TotalClassesTaken);
        Assert.Equal(0, user.ReputationScore);
        Assert.False(user.IsSchoolOwner);
    }

    #endregion

    #region Review

    [Fact]
    public void Review_GetRatingStars_ReturnsCorrectStars()
    {
        var review = new Review { Rating = 3 };
        Assert.Equal("★★★☆☆", review.GetRatingStars());
    }

    [Fact]
    public void Review_GetRatingStars_FullStars()
    {
        var review = new Review { Rating = 5 };
        Assert.Equal("★★★★★", review.GetRatingStars());
    }

    [Fact]
    public void Review_GetRatingStars_OneStars()
    {
        var review = new Review { Rating = 1 };
        Assert.Equal("★☆☆☆☆", review.GetRatingStars());
    }

    [Fact]
    public void Review_GetTimeAgo_JustNow()
    {
        var review = new Review { CreatedAt = DateTime.UtcNow };
        Assert.Equal("just now", review.GetTimeAgo());
    }

    [Fact]
    public void Review_GetTimeAgo_HoursAgo()
    {
        var review = new Review { CreatedAt = DateTime.UtcNow.AddHours(-3) };
        Assert.Contains("hour", review.GetTimeAgo());
    }

    [Fact]
    public void Review_GetTimeAgo_DaysAgo()
    {
        var review = new Review { CreatedAt = DateTime.UtcNow.AddDays(-5) };
        Assert.Contains("day", review.GetTimeAgo());
    }

    [Fact]
    public void Review_GetTimeAgo_MonthsAgo()
    {
        var review = new Review { CreatedAt = DateTime.UtcNow.AddDays(-60) };
        Assert.Contains("month", review.GetTimeAgo());
    }

    [Fact]
    public void Review_GetTimeAgo_YearsAgo()
    {
        var review = new Review { CreatedAt = DateTime.UtcNow.AddDays(-400) };
        Assert.Contains("year", review.GetTimeAgo());
    }

    [Fact]
    public void Review_DefaultValues()
    {
        var review = new Review();

        Assert.False(review.IsApproved);
        Assert.False(review.IsVerifiedVisit);
        Assert.Equal(0, review.HelpfulCount);
    }

    #endregion

    #region OpeningHourPeriod

    [Fact]
    public void OpeningHourPeriod_GetOpenTime_Correct()
    {
        var period = new OpeningHourPeriod { OpenMinutes = 540 }; // 9:00 AM
        Assert.Equal("9:00 AM", period.GetOpenTime());
    }

    [Fact]
    public void OpeningHourPeriod_GetCloseTime_Correct()
    {
        var period = new OpeningHourPeriod { CloseMinutes = 1080 }; // 6:00 PM
        Assert.Equal("6:00 PM", period.GetCloseTime());
    }

    [Fact]
    public void OpeningHourPeriod_GetDayName_ReturnsCorrectNames()
    {
        Assert.Equal("Sunday", new OpeningHourPeriod { DayOfWeek = 0 }.GetDayName());
        Assert.Equal("Monday", new OpeningHourPeriod { DayOfWeek = 1 }.GetDayName());
        Assert.Equal("Saturday", new OpeningHourPeriod { DayOfWeek = 6 }.GetDayName());
        Assert.Equal("Unknown", new OpeningHourPeriod { DayOfWeek = 7 }.GetDayName());
    }

    #endregion

    #region City

    [Fact]
    public void City_DefaultValues_AreCorrect()
    {
        var city = new City();

        Assert.True(city.IsActive);
        Assert.Equal(0, city.CrawlCount);
        Assert.Equal(15, city.MaxResults);
        Assert.Equal("United States", city.Country);
        Assert.Equal("US", city.Region);
    }

    [Fact]
    public void City_Id_AliasMapsToCorrectProperty()
    {
        var city = new City { CityId = 42 };
        Assert.Equal(42, city.Id);
    }

    #endregion

    #region CrawlHistory

    [Fact]
    public void CrawlHistory_DefaultValues()
    {
        var history = new CrawlHistory();

        Assert.Equal("InProgress", history.Status);
        Assert.Equal(0, history.SchoolsFound);
        Assert.Equal(0, history.SchoolsAdded);
        Assert.Equal(0, history.SchoolsUpdated);
    }

    #endregion

    #region BlogPost

    [Fact]
    public void BlogPost_DefaultValues()
    {
        var post = new BlogPost();

        Assert.False(post.IsPublished);
        Assert.False(post.IsAutoGenerated);
        Assert.Equal(0, post.ViewCount);
    }

    [Fact]
    public void BlogPost_Aliases_WorkCorrectly()
    {
        var post = new BlogPost
        {
            FeaturedImage = "image.jpg",
            Summary = "Test summary"
        };

        Assert.Equal("image.jpg", post.FeaturedImageUrl);
        Assert.Equal("Test summary", post.Excerpt);
    }

    #endregion

    #region CuisineTypes

    [Fact]
    public void CuisineTypes_GetAll_ReturnsAllTypes()
    {
        var all = CuisineTypes.GetAll();
        Assert.True(all.Count > 20);
        Assert.Contains("Italian", all);
        Assert.Contains("French", all);
        Assert.Contains("Other", all);
    }

    #endregion

    #region DifficultyLevels

    [Fact]
    public void DifficultyLevels_GetAll_ReturnsFourLevels()
    {
        var all = DifficultyLevels.GetAll();
        Assert.Equal(4, all.Count);
        Assert.Contains("Beginner", all);
        Assert.Contains("Intermediate", all);
        Assert.Contains("Advanced", all);
        Assert.Contains("All Levels", all);
    }

    #endregion

    #region CrawlSettings

    [Fact]
    public void CrawlSettings_DefaultValues()
    {
        var settings = new CookingClassFinder.Models.CrawlSettings();

        Assert.False(settings.EnableAutoCrawl);
        Assert.Equal(3, settings.CitiesPerBatch);
        Assert.Equal(2, settings.RetryDelayHours);
        Assert.Equal(30, settings.DelayBetweenCitiesSeconds);
        Assert.Equal(15, settings.MaxResultsPerCity);
        Assert.Equal(30, settings.CheckIntervalMinutes);
    }

    #endregion

    #region SchoolSearchPagedResult

    [Fact]
    public void SchoolSearchPagedResult_TotalPages_CalculatesCorrectly()
    {
        var result = new CookingClassFinder.Models.DTOs.SchoolSearchPagedResult
        {
            TotalCount = 25,
            PageSize = 10
        };

        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void SchoolSearchPagedResult_HasNextPage()
    {
        var result = new CookingClassFinder.Models.DTOs.SchoolSearchPagedResult
        {
            TotalCount = 25,
            Page = 1,
            PageSize = 10
        };

        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void SchoolSearchPagedResult_LastPage_NoNextPage()
    {
        var result = new CookingClassFinder.Models.DTOs.SchoolSearchPagedResult
        {
            TotalCount = 25,
            Page = 3,
            PageSize = 10
        };

        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void SchoolSearchPagedResult_FirstPage_NoPreviousPage()
    {
        var result = new CookingClassFinder.Models.DTOs.SchoolSearchPagedResult
        {
            Page = 1,
            PageSize = 10
        };

        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void SchoolSearchPagedResult_Page2_HasPreviousPage()
    {
        var result = new CookingClassFinder.Models.DTOs.SchoolSearchPagedResult
        {
            Page = 2,
            PageSize = 10
        };

        Assert.True(result.HasPreviousPage);
    }

    #endregion
}
