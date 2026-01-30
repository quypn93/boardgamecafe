using System.ComponentModel.DataAnnotations;

namespace EscapeRoomFinder.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Display Name")]
        [MaxLength(100)]
        public string? DisplayName { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class ProfileViewModel
    {
        public int UserId { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Display Name")]
        [MaxLength(100)]
        public string? DisplayName { get; set; }

        [Display(Name = "First Name")]
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [Display(Name = "Last Name")]
        [MaxLength(100)]
        public string? LastName { get; set; }

        [Display(Name = "Bio")]
        [MaxLength(1000)]
        public string? Bio { get; set; }

        [Display(Name = "City")]
        [MaxLength(100)]
        public string? City { get; set; }

        [Display(Name = "Country")]
        [MaxLength(100)]
        public string? Country { get; set; }

        [Display(Name = "Favorite Themes")]
        public string? FavoriteThemes { get; set; }

        public string? AvatarUrl { get; set; }

        // Stats
        public int TotalReviews { get; set; }
        public int TotalRoomsPlayed { get; set; }
        public int TotalEscapes { get; set; }
        public double EscapeRate { get; set; }
        public int ReputationScore { get; set; }
    }
}
