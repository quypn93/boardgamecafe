using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using BoardGameCafeFinder.Models.Domain;
using BoardGameCafeFinder.Models.ViewModels;
using BoardGameCafeFinder.Services;

namespace BoardGameCafeFinder.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<AccountController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public AccountController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<AccountController> logger,
        IWebHostEnvironment environment,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _environment = environment;
        _emailService = emailService;
        _configuration = configuration;
    }

    private bool RequireEmailConfirmation => _configuration.GetValue<bool>("Email:RequireConfirmation", false);

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in: {Email}", model.Email);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                // Check if admin and redirect to admin dashboard
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return RedirectToAction("Dashboard", "Admin");
                }

                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
            }
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new User
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                IsCafeOwner = model.IsCafeOwner,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User created a new account: {Email}", model.Email);

                // Assign role based on user type
                if (model.IsCafeOwner)
                {
                    await _userManager.AddToRoleAsync(user, "CafeOwner");
                    _logger.LogInformation("Assigned CafeOwner role to: {Email}", model.Email);
                }
                else
                {
                    await _userManager.AddToRoleAsync(user, "User");
                }

                // Send email confirmation if enabled
                if (RequireEmailConfirmation)
                {
                    try
                    {
                        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        var confirmationLink = Url.Action("ConfirmEmail", "Account",
                            new { userId = user.Id, token = token }, Request.Scheme);

                        if (!string.IsNullOrEmpty(confirmationLink))
                        {
                            await _emailService.SendEmailConfirmationAsync(
                                user.Email!,
                                user.FirstName ?? user.Email!,
                                confirmationLink);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send confirmation email to {Email}", model.Email);
                    }
                }

                // Auto sign-in after registration
                await _signInManager.SignInAsync(user, isPersistent: false);

                // Redirect cafe owners to their profile to complete setup
                if (model.IsCafeOwner)
                {
                    var msg = "Welcome! As a cafe owner, you can claim your cafe from the Pricing page.";
                    if (RequireEmailConfirmation) msg += " We've sent a confirmation email to verify your address.";
                    TempData["Success"] = msg;
                    return RedirectToAction("Profile");
                }

                TempData["Success"] = RequireEmailConfirmation
                    ? "Welcome! We've sent a confirmation email to verify your address."
                    : "Welcome!";
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        ViewBag.EmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
        ViewBag.RequireEmailConfirmation = RequireEmailConfirmation;
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string firstName, string lastName, string? displayName, string? city, string? country, string? bio)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.DisplayName = displayName ?? $"{firstName} {lastName}";
        user.City = city;
        user.Country = country;
        user.Bio = bio;

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            TempData["Success"] = "Profile updated successfully!";
        }
        else
        {
            TempData["Error"] = "Failed to update profile.";
        }

        return RedirectToAction("Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAvatar(IFormFile avatar)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        if (avatar == null || avatar.Length == 0)
        {
            TempData["Error"] = "Please select an image file.";
            return RedirectToAction("Profile");
        }

        // Validate file type
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(avatar.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            TempData["Error"] = "Invalid file type. Allowed: JPG, PNG, GIF, WebP.";
            return RedirectToAction("Profile");
        }

        // Validate file size (max 5MB)
        if (avatar.Length > 5 * 1024 * 1024)
        {
            TempData["Error"] = "File size must be less than 5MB.";
            return RedirectToAction("Profile");
        }

        try
        {
            // Create avatars directory if not exists
            var avatarsPath = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
            if (!Directory.Exists(avatarsPath))
            {
                Directory.CreateDirectory(avatarsPath);
            }

            // Delete old avatar if exists
            if (!string.IsNullOrEmpty(user.AvatarUrl) && user.AvatarUrl.StartsWith("/uploads/avatars/"))
            {
                var oldAvatarPath = Path.Combine(_environment.WebRootPath, user.AvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldAvatarPath))
                {
                    System.IO.File.Delete(oldAvatarPath);
                }
            }

            // Generate unique filename
            var fileName = $"{user.Id}_{DateTime.UtcNow.Ticks}{extension}";
            var filePath = Path.Combine(avatarsPath, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await avatar.CopyToAsync(stream);
            }

            // Update user avatar URL
            user.AvatarUrl = $"/uploads/avatars/{fileName}";
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["Success"] = "Avatar updated successfully!";
                _logger.LogInformation("User {Email} updated avatar", user.Email);
            }
            else
            {
                TempData["Error"] = "Failed to update avatar.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading avatar for user {Email}", user.Email);
            TempData["Error"] = "An error occurred while uploading avatar.";
        }

        return RedirectToAction("Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAvatar()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        try
        {
            // Delete avatar file if exists
            if (!string.IsNullOrEmpty(user.AvatarUrl) && user.AvatarUrl.StartsWith("/uploads/avatars/"))
            {
                var avatarPath = Path.Combine(_environment.WebRootPath, user.AvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(avatarPath))
                {
                    System.IO.File.Delete(avatarPath);
                }
            }

            user.AvatarUrl = null;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["Success"] = "Avatar removed successfully!";
            }
            else
            {
                TempData["Error"] = "Failed to remove avatar.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing avatar for user {Email}", user.Email);
            TempData["Error"] = "An error occurred while removing avatar.";
        }

        return RedirectToAction("Profile");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(int userId, string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "Invalid confirmation link.";
            return RedirectToAction("Login");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("Login");
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            _logger.LogInformation("Email confirmed for user: {Email}", user.Email);
            TempData["Success"] = "Email confirmed successfully! Thank you for verifying your email.";
        }
        else
        {
            TempData["Error"] = "Failed to confirm email. The link may have expired.";
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Profile");
        }

        return RedirectToAction("Login");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        if (await _userManager.IsEmailConfirmedAsync(user))
        {
            TempData["Success"] = "Your email is already confirmed.";
            return RedirectToAction("Profile");
        }

        try
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action("ConfirmEmail", "Account",
                new { userId = user.Id, token = token }, Request.Scheme);

            if (!string.IsNullOrEmpty(confirmationLink))
            {
                await _emailService.SendEmailConfirmationAsync(
                    user.Email!,
                    user.FirstName ?? user.Email!,
                    confirmationLink);

                TempData["Success"] = "Confirmation email sent! Please check your inbox.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend confirmation email to {Email}", user.Email);
            TempData["Error"] = "Failed to send confirmation email. Please try again later.";
        }

        return RedirectToAction("Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmNewPassword)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login");
        }

        if (newPassword != confirmNewPassword)
        {
            TempData["Error"] = "New password and confirmation do not match.";
            return RedirectToAction("Profile");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Password changed successfully!";
            _logger.LogInformation("User changed password: {Email}", user.Email);
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction("Profile");
    }
}
