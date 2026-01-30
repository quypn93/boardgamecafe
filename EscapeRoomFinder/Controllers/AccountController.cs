using EscapeRoomFinder.Models.Domain;
using EscapeRoomFinder.Models.ViewModels;
using EscapeRoomFinder.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EscapeRoomFinder.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IEmailService _emailService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IEmailService emailService,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _logger = logger;
        }

        [Route("login")]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [Route("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(
                    model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    if (user != null)
                    {
                        user.LastLoginAt = DateTime.UtcNow;
                        await _userManager.UpdateAsync(user);
                    }

                    _logger.LogInformation("User {Email} logged in.", model.Email);

                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }

                    return RedirectToAction("Index", "Home");
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User {Email} account locked out.", model.Email);
                    ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                }
            }

            return View(model);
        }

        [Route("register")]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new RegisterViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [Route("register")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new User
                {
                    UserName = model.Email,
                    Email = model.Email,
                    DisplayName = model.DisplayName ?? model.Email.Split('@')[0],
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "User");

                    _logger.LogInformation("User {Email} created a new account.", model.Email);

                    // Send welcome email
                    try
                    {
                        await _emailService.SendWelcomeEmailAsync(user.Email!, user.DisplayName ?? "User");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send welcome email to {Email}", user.Email);
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);

                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }

                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpPost]
        [Route("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [Route("profile")]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? "",
                DisplayName = user.DisplayName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Bio = user.Bio,
                City = user.City,
                Country = user.Country,
                FavoriteThemes = user.FavoriteThemes,
                AvatarUrl = user.AvatarUrl,
                TotalReviews = user.TotalReviews,
                TotalRoomsPlayed = user.TotalRoomsPlayed,
                TotalEscapes = user.TotalEscapes,
                EscapeRate = user.GetEscapeRate(),
                ReputationScore = user.ReputationScore
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [Route("profile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                user.DisplayName = model.DisplayName;
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Bio = model.Bio;
                user.City = model.City;
                user.Country = model.Country;
                user.FavoriteThemes = model.FavoriteThemes;

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    TempData["Success"] = "Profile updated successfully.";
                    return RedirectToAction(nameof(Profile));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // Re-populate read-only fields
            model.Email = user.Email ?? "";
            model.AvatarUrl = user.AvatarUrl;
            model.TotalReviews = user.TotalReviews;
            model.TotalRoomsPlayed = user.TotalRoomsPlayed;
            model.TotalEscapes = user.TotalEscapes;
            model.EscapeRate = user.GetEscapeRate();
            model.ReputationScore = user.ReputationScore;

            return View(model);
        }

        [Route("access-denied")]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
