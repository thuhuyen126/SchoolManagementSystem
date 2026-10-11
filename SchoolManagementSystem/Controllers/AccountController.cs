using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Data.Entities;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace SchoolManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private const string LoginChallengeSessionKey = "LoginOtpChallengeId";
        private const string LoginOtpPurpose = "login";
        private readonly IUserHelper _userHelper;
        private readonly IMailHelper _mailHelper;
        private readonly IConfiguration _configuration;
        // ĐÃ THÊM: Khai báo Log Helper
        private readonly IActivityLogHelper _activityLogHelper;
        private readonly OtpChallengeStore _otpChallengeStore;
        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(
            IUserHelper userHelper,
            IMailHelper mailHelper,
            IConfiguration configuration,
            OtpChallengeStore otpChallengeStore,
            IHttpClientFactory httpClientFactory,
            // ĐÃ THÊM: Tiêm Log Helper vào constructor
            IActivityLogHelper activityLogHelper)
        {
            _userHelper = userHelper;
            _mailHelper = mailHelper;
            _configuration = configuration;
            _otpChallengeStore = otpChallengeStore;
            _httpClientFactory = httpClientFactory;
            // Gán giá trị
            _activityLogHelper = activityLogHelper;
        }

        // --- HÀM BỔ TRỢ KIỂM TRA RECAPTCHA ---
        private async Task<bool> IsReCaptchaValid(string token)
        {
            var secretKey = _configuration["GoogleReCAPTCHA:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            try
            {
                var client = _httpClientFactory.CreateClient("GoogleReCaptcha");
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = secretKey,
                    ["response"] = token
                });
                using var response = await client.PostAsync(
                    "https://www.google.com/recaptcha/api/siteverify",
                    content);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ReCaptchaVerificationResponse>(jsonResponse);
                var allowedHostnames = _configuration.GetSection("GoogleReCAPTCHA:AllowedHostnames")
                    .Get<string[]>() ?? Array.Empty<string>();
                return result?.Success == true &&
                    !string.IsNullOrWhiteSpace(result.Hostname) &&
                    allowedHostnames.Contains(result.Hostname, StringComparer.OrdinalIgnoreCase);
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private sealed class ReCaptchaVerificationResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("hostname")]
            public string? Hostname { get; set; }
        }

        // Displays the login page
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            var testMode = bool.TryParse(Environment.GetEnvironmentVariable("SCHOOL_TEST_MODE"), out var testModeEnabled) && testModeEnabled;
            var testRecaptchaBypass = bool.TryParse(Environment.GetEnvironmentVariable("TEST_RECAPTCHA_BYPASS"), out var bypassEnabled) && bypassEnabled;
            var recaptchaBypass = testMode && testRecaptchaBypass;

            if (!recaptchaBypass)
            {
                var reCaptchaResponse = Request.Form["g-recaptcha-response"].ToString();
                if (string.IsNullOrEmpty(reCaptchaResponse) || !await IsReCaptchaValid(reCaptchaResponse))
                {
                    ModelState.AddModelError(string.Empty, "Please verify that you are not a robot.");
                    return View(model);
                }
            }

            if (ModelState.IsValid)
            {
                var user = await _userHelper.GetUserByEmailAsync(model.Username);
                if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                {
                    var isValidPassword = await _userHelper.ValidatePasswordAsync(user, model.Password);
                    if (isValidPassword.Succeeded)
                    {
                        string otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
                        var challenge = _otpChallengeStore.Create(
                            LoginOtpPurpose,
                            HttpContext.Session.Id,
                            user.Email,
                            user.Id,
                            model.RememberMe,
                            otp);

                        string emailBody = $@"
                <div style='font-family: Arial; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                    <h2 style='color: #0d6efd;'>DevArt Academy - Login Verification</h2>
                    <p>Chào bạn,</p>
                    <p>Mã xác thực OTP để đăng nhập của bạn là: <strong style='font-size: 24px; color: red;'>{otp}</strong></p>
                    <p>Mã này dùng để hoàn tất quá trình đăng nhập. Vui lòng không chia sẻ cho ai.</p>
                </div>";

                        var mailResponse = _mailHelper.SendEmail(user.Email, "Your Login OTP Code", emailBody);
                        if (mailResponse == null || !mailResponse.IsSuccess)
                        {
                            _otpChallengeStore.Cancel(challenge.ChallengeId);
                            ModelState.AddModelError(string.Empty, "Unable to send the verification code. Please try again later.");
                            return View(model);
                        }

                        HttpContext.Session.SetString(LoginChallengeSessionKey, challenge.ChallengeId);

                        return RedirectToAction("VerifyOtp");
                    }
                }
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult VerifyOtp()
        {
            var challengeId = HttpContext.Session.GetString(LoginChallengeSessionKey);
            var challenge = challengeId == null
                ? null
                : _otpChallengeStore.GetPending(challengeId, LoginOtpPurpose, HttpContext.Session.Id);
            if (challenge == null)
            {
                HttpContext.Session.Remove(LoginChallengeSessionKey);
                return RedirectToAction("Login");
            }

            var model = new VerifyOtpViewModel { Email = challenge.SubjectId };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
        {
            var challengeId = HttpContext.Session.GetString(LoginChallengeSessionKey);
            if (string.IsNullOrEmpty(challengeId))
            {
                return RedirectToAction("Login");
            }

            var pending = _otpChallengeStore.GetPending(
                challengeId,
                LoginOtpPurpose,
                HttpContext.Session.Id);
            if (pending == null)
            {
                HttpContext.Session.Remove(LoginChallengeSessionKey);
                ModelState.AddModelError(string.Empty, "The verification code has expired. Please sign in again.");
                return View(model);
            }

            var validation = _otpChallengeStore.ValidateAndConsume(
                challengeId,
                LoginOtpPurpose,
                HttpContext.Session.Id,
                pending.SubjectId,
                pending.AccountId,
                model.Code);

            if (validation.Status == OtpValidationStatus.Invalid)
            {
                ModelState.AddModelError(string.Empty, "Invalid OTP code. Please check your email again.");
                model.Email = pending.SubjectId;
                return View(model);
            }

            if (validation.Status != OtpValidationStatus.Valid || validation.Challenge == null)
            {
                HttpContext.Session.Remove(LoginChallengeSessionKey);
                ModelState.AddModelError(string.Empty, "The verification code is no longer valid. Please sign in again.");
                return View(model);
            }

            HttpContext.Session.Remove(LoginChallengeSessionKey);
            var user = await _userHelper.GetUserByIdAsync(validation.Challenge.AccountId);
            if (user == null || string.IsNullOrWhiteSpace(user.Email) || !await _userHelper.CanSignInAsync(user))
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View("Login");
            }

            await _userHelper.SignInDirectlyAsync(user, validation.Challenge.RememberMe);
            await _activityLogHelper.LogActivityAsync(
                user.Email,
                "Login",
                "User logged into the system successfully.");

            var userRole = await _userHelper.GetRoleAsync(user);
            switch (userRole)
            {
                case "Admin": return RedirectToAction("AdminDashboard", "Dashboard");
                case "Employee": return RedirectToAction("EmployeeDashboard", "Dashboard");
                case "Teacher": return RedirectToAction("TeacherDashboard", "Dashboard");
                case "Student": return RedirectToAction("StudentDashboard", "Dashboard");
                default: return RedirectToAction("Index", "Home");
            }

        }

        // Logs out the user
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Lấy email trước khi đăng xuất để ghi log
            string? userEmail = User.Identity?.Name;

            await _userHelper.LogoutAsync();

            // --- ĐÃ THÊM GHI LOG: ĐĂNG XUẤT ---
            if (!string.IsNullOrEmpty(userEmail))
            {
                await _activityLogHelper.LogActivityAsync(
                    userEmail,
                    "Logout",
                    "User logged out of the system."
                );
            }
            // ----------------------------------

            return RedirectToAction("Index", "Home");
        }

        // Displays the registration page
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterNewUserViewModel());
        }

        // Processes the registration
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterNewUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingUser = await _userHelper.GetUserByEmailAsync(model.Username);
            if (existingUser != null)
            {
                ViewBag.Message = "If the address can be registered, an activation email will be sent.";
                return View("Login");
            }

            var user = new User
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Username,
                UserName = model.Username,
                Address = model.Address,
                PhoneNumber = model.PhoneNumber,
                DateCreated = DateTime.UtcNow
            };

            var createResult = await _userHelper.CreateUserWithRoleAsync(user, "Pending");
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            var confirmationToken = await _userHelper.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action(
                "ConfirmEmail",
                "Account",
                new { userId = user.Id, token = confirmationToken },
                protocol: HttpContext.Request.Scheme);

            if (string.IsNullOrWhiteSpace(confirmationLink))
            {
                var cleanupResult = await _userHelper.DeleteUserAsync(user);
                ModelState.AddModelError(string.Empty, cleanupResult.Succeeded
                    ? "Unable to create the activation link. Please try again later."
                    : "The account could not be activated automatically. Please contact an administrator.");
                return View(model);
            }

            var emailBody = $@"
                <div style='font-family: Arial;'>
                    <h2>Chào mừng {System.Net.WebUtility.HtmlEncode(model.FirstName)}!</h2>
                    <p>Tài khoản của bạn đã được tạo.</p>
                    <p>Vui lòng xác nhận địa chỉ email để tiếp tục và tự đặt mật khẩu.</p>
                    <a href='{System.Net.WebUtility.HtmlEncode(confirmationLink)}'>Xác nhận email</a>
                </div>";
            var mailResponse = _mailHelper.SendEmail(user.Email, "Xác nhận tài khoản", emailBody);
            if (mailResponse?.IsSuccess != true)
            {
                var cleanupResult = await _userHelper.DeleteUserAsync(user);
                ModelState.AddModelError(string.Empty, cleanupResult.Succeeded
                    ? "The activation email could not be sent. Please try registering again later."
                    : "The account could not be activated automatically. Please contact an administrator.");
                return View(model);
            }

            var creator = User.Identity?.Name ?? "System";
            await _activityLogHelper.LogActivityAsync(
                creator,
                "Register",
                "Created a pending user account.");

            ViewBag.Message = "If the address can be registered, an activation email will be sent.";
            return View("Login");
        }

        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> ChangeUser()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email)) return Forbid();

            var user = await _userHelper.GetUserByEmailAsync(email);
            if (user == null) return RedirectToAction("NotAuthorized");

            var model = new ChangeUserViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Address = user.Address,
                PhoneNumber = user.PhoneNumber
            };
            return View(model);
        }

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUser(ChangeUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var email = User.Identity?.Name;
                if (string.IsNullOrWhiteSpace(email)) return Forbid();

                var user = await _userHelper.GetUserByEmailAsync(email);
                if (user != null)
                {
                    user.FirstName = model.FirstName;
                    user.LastName = model.LastName;
                    user.Address = model.Address;
                    user.PhoneNumber = model.PhoneNumber;

                    var result = await _userHelper.UpdateUserAsync(user);
                    if (result.Succeeded)
                    {
                        await _userHelper.UpdateUserDataByRoleAsync(user);

                        // --- ĐÃ THÊM GHI LOG: CẬP NHẬT THÔNG TIN CÁ NHÂN ---
                        await _activityLogHelper.LogActivityAsync(
                            User.Identity?.Name ?? user.Id,
                            "Update",
                            "User updated their profile information."
                        );
                        // ---------------------------------------------------

                        return RedirectToAction("Index", "Home");
                    }
                    ModelState.AddModelError(string.Empty, "Failed to update user details.");
                }
                else
                {
                    return RedirectToAction("NotAuthorized");
                }
            }
            return View(model);
        }

        public IActionResult NotAuthorized() => View();

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token)) return NotFound();

            var user = await _userHelper.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            Response.Headers.CacheControl = "no-store";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            return View(new ConfirmEmailViewModel { UserId = userId, Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmEmail(ConfirmEmailViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userHelper.GetUserByIdAsync(model.UserId);
            if (user == null) return NotFound();

            var result = await _userHelper.ConfirmEmailAsync(user, model.Token);
            if (!result.Succeeded) return View("Error");

            var passwordSetupToken = await _userHelper.GeneratePasswordResetTokenAsync(user);
            return RedirectToAction("ChangeFirstPassword", new { email = user.Email, token = passwordSetupToken });
        }

        [HttpGet]
        public async Task<IActionResult> ChangeFirstPassword(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token)) return NotFound();
            var user = await _userHelper.GetUserByEmailAsync(email);
            if (user == null || !user.EmailConfirmed) return NotFound();

            Response.Headers.CacheControl = "no-store";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            var model = new ChangeFirstPasswordViewModel
            {
                Email = email,
                Token = token
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeFirstPassword(ChangeFirstPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userHelper.GetUserByEmailAsync(model.Email);
                if (user != null && user.EmailConfirmed)
                {
                    var result = await _userHelper.ResetPasswordAsync(user, model.Token, model.NewPassword);
                    if (result.Succeeded)
                    {
                        // --- ĐÃ THÊM GHI LOG: KÍCH HOẠT VÀ ĐỔI MẬT KHẨU LẦN ĐẦU ---
                        await _activityLogHelper.LogActivityAsync(
                            user.Email ?? user.Id,
                            "Update",
                            "User activated account and set the first password."
                        );
                        // ----------------------------------------------------------
                        return RedirectToAction("Login");
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "The account could not be verified. Request a new activation link.");
                }
            }
            return View(model);
        }

        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult ChangePassword() => View();

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var email = User.Identity?.Name;
                if (string.IsNullOrWhiteSpace(email)) return Forbid();

                var user = await _userHelper.GetUserByEmailAsync(email);
                if (user != null)
                {
                    var result = await _userHelper.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
                    if (result.Succeeded)
                    {
                        // --- ĐÃ THÊM GHI LOG: ĐỔI MẬT KHẨU ---
                        await _activityLogHelper.LogActivityAsync(
                            User.Identity?.Name ?? user.Id,
                            "Update",
                            "User changed their password."
                        );
                        // -------------------------------------
                        return RedirectToAction("ChangeUser");
                    }
                    ModelState.AddModelError(
                        string.Empty,
                        result.Errors.FirstOrDefault()?.Description ?? "The password could not be changed.");
                }
            }
            return View(model);
        }

        public IActionResult RecoverPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecoverPassword(RecoverPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userHelper.GetUserByEmailAsync(model.Email);
            if (user is { EmailConfirmed: true })
            {
                var myToken = await _userHelper.GeneratePasswordResetTokenAsync(user);
                var link = Url.Action("ResetPassword", "Account", new { token = myToken }, protocol: HttpContext.Request.Scheme);
                if (!string.IsNullOrWhiteSpace(link))
                {
                    _mailHelper.SendEmail(
                        user.Email ?? user.Id,
                        "Password Reset",
                        $"<h1>Password Reset</h1><a href='{System.Net.WebUtility.HtmlEncode(link)}'>Reset Password</a>");
                }
            }

            ViewBag.Message = "If the account is eligible, password reset instructions will be sent.";
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return NotFound();

            Response.Headers.CacheControl = "no-store";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            return View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userHelper.GetUserByEmailAsync(model.UserName);
            if (user != null)
            {
                var result = await _userHelper.ResetPasswordAsync(user, model.Token, model.Password);
                if (result.Succeeded)
                {
                    // --- ĐÃ THÊM GHI LOG: KHÔI PHỤC MẬT KHẨU ---
                    await _activityLogHelper.LogActivityAsync(
                        user.Email ?? user.Id,
                        "Update",
                        "User reset their password via email token."
                    );
                    // -------------------------------------------

                    ViewBag.Message = "Password reset successfully.";
                    return View();
                }
                ViewBag.Message = "Error resetting the password.";
                return View(model);
            }
            ViewBag.Message = "User not found.";
            return View(model);
        }

        public IActionResult AccessDenied() => View();

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(string userId, string roleName)
        {
            if (string.IsNullOrWhiteSpace(userId)) return NotFound();
            var user = await _userHelper.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actorId)) return Forbid();

            var result = await _userHelper.ChangeUserRoleAsync(
                user,
                roleName,
                actorId);
            if (result.Succeeded)
            {
                await _activityLogHelper.LogActivityAsync(
                    User.Identity?.Name ?? actorId,
                    "Update",
                    $"Changed role for user {user.Email} to {roleName}."
                );
                TempData["SuccessMessage"] = "User role updated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(error => error.Description));
            }
            return RedirectToAction("Index1", "UserManagement");
        }
    }
}
