using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GBC_Ticketing.Web.Models;
using GBC_Ticketing.Web.Services;
using Serilog;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace GBC_Ticketing.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            // Log model state for debugging
            if (!ModelState.IsValid)
            {
                Log.Warning("Registration validation failed for {Email}", model.Email);
                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    foreach (var error in errors)
                    {
                        Log.Warning("ModelState Error - Key: {Key}, Error: {Error}", key, error.ErrorMessage);
                    }
                }
                return View(model);
            }

            try
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    PhoneNumber = model.PhoneNumber
                };

                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    Log.Warning("Registration attempted with existing email: {Email}", model.Email);
                    ModelState.AddModelError(string.Empty, "An account with this email already exists. Please use a different email or try logging in.");
                    return View(model);
                }

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // Assign default role as Attendee
                    var roleResult = await _userManager.AddToRoleAsync(user, "Attendee");
                    if (!roleResult.Succeeded)
                    {
                        Log.Warning("Failed to assign Attendee role to {Email}. Errors: {Errors}", 
                            user.Email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }

                    Log.Information("New user registered successfully: {Email}", user.Email);

                    // Generate email confirmation token
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var encodedToken = WebUtility.UrlEncode(token);
                    var confirmationLink = Url.Action("ConfirmEmail", "Account", 
                        new { userId = user.Id, token = encodedToken }, Request.Scheme);

                    // Send confirmation email asynchronously (don't block registration)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var emailBody = $@"
                                <h2>Welcome to GBC Ticketing!</h2>
                                <p>Thank you for registering, {user.FullName}!</p>
                                <p>Please confirm your email by clicking the link below:</p>
                                <p><a href='{confirmationLink}'>Confirm Email</a></p>
                                <p>If you didn't create this account, please ignore this email.</p>
                            ";

                            await _emailService.SendEmailAsync(user.Email, "Confirm your email", emailBody);
                            Log.Information("Confirmation email sent to {Email}", user.Email);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to send confirmation email to {Email}", user.Email);
                        }
                    });

                    TempData["Message"] = "Registration successful! Please check your email to confirm your account. You can also log in now.";
                    return RedirectToAction("Login");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                    Log.Warning("Registration error for {Email}: {Error}", model.Email, error.Description);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception during registration for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "An error occurred during registration. Please try again.");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                Log.Warning("Login validation failed for {Email}", model.Email);
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                Log.Warning("Login attempt for non-existent user: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            if (!user.EmailConfirmed)
            {
                Log.Warning("Login blocked for unconfirmed email: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Please confirm your email before logging in. Check your inbox for the confirmation link.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                Log.Information("User {Email} logged in successfully", model.Email);
                return RedirectToLocal(returnUrl);
            }

            if (result.IsLockedOut)
            {
                Log.Warning("User {Email} account locked out", model.Email);
                TempData["Error"] = "This account has been locked out. Please try again later.";
                return View(model);
            }

            Log.Warning("Failed login attempt for {Email}. Result: IsLockedOut={IsLockedOut}, IsNotAllowed={IsNotAllowed}, RequiresTwoFactor={RequiresTwoFactor}", 
                model.Email, result.IsLockedOut, result.IsNotAllowed, result.RequiresTwoFactor);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                Log.Warning("Email confirmation attempted with invalid user ID: {UserId}", userId);
                return NotFound();
            }

            // URL decode the token
            var decodedToken = WebUtility.UrlDecode(token);

            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            if (result.Succeeded)
            {
                Log.Information("Email confirmed successfully for user {Email}", user.Email);
                TempData["Message"] = "Email confirmed successfully! You can now log in.";
                return RedirectToAction("Login");
            }

            Log.Warning("Email confirmation failed for user {Email}. Errors: {Errors}", 
                user.Email, string.Join(", ", result.Errors.Select(e => e.Description)));

            TempData["Error"] = "Email confirmation failed. Please try again or contact support.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user doesn't exist
                return RedirectToAction("ForgotPasswordConfirmation");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var resetLink = Url.Action("ResetPassword", "Account", 
                new { userId = user.Id, token = encodedToken }, Request.Scheme);

            try
            {
                var emailBody = $@"
                    <h2>Password Reset Request</h2>
                    <p>Hi {user.FullName},</p>
                    <p>You requested to reset your password. Click the link below to reset it:</p>
                    <p><a href='{resetLink}'>Reset Password</a></p>
                    <p>If you didn't request this, please ignore this email.</p>
                    <p>This link will expire in 24 hours.</p>
                ";

                await _emailService.SendEmailAsync(user.Email, "Reset your password", emailBody);
                Log.Information("Password reset email sent to {Email}", user.Email);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send password reset email to {Email}", user.Email);
            }

            return RedirectToAction("ForgotPasswordConfirmation");
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string? userId, string? token)
        {
            if (userId == null || token == null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new ResetPasswordViewModel { UserId = userId, Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                Log.Warning("Password reset attempted with invalid user ID: {UserId}", model.UserId);
                return RedirectToAction("ResetPasswordConfirmation");
            }

            // URL decode the token in case it was encoded
            var decodedToken = WebUtility.UrlDecode(model.Token);
            
            var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.Password);
            if (result.Succeeded)
            {
                Log.Information("Password reset successful for user {Email}", user.Email);
                return RedirectToAction("ResetPasswordConfirmation");
            }

            Log.Warning("Password reset failed for user {Email}. Errors: {Errors}", 
                user.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View("AccessDenied");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Dashboard");
        }
    }

    // View Models
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

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
    }

    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
