using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Data.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace SchoolManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserManagementController : Controller
    {
        private const string DeleteChallengeSessionKey = "DeleteUserOtpChallengeId";
        private const string DeleteOtpPurpose = "delete-user";
        private readonly IUserHelper _userHelper;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMailHelper _mailHelper;
        private readonly IActivityLogHelper _activityLogHelper;
        private readonly SchoolDbContext _context; // Dùng để dọn dẹp dữ liệu liên quan
        private readonly OtpChallengeStore _otpChallengeStore;

        public UserManagementController(
            IUserHelper userHelper,
            RoleManager<IdentityRole> roleManager,
            IMailHelper mailHelper,
            IActivityLogHelper activityLogHelper,
            SchoolDbContext context,
            OtpChallengeStore otpChallengeStore)
        {
            _userHelper = userHelper;
            _roleManager = roleManager;
            _mailHelper = mailHelper;
            _activityLogHelper = activityLogHelper;
            _context = context;
            _otpChallengeStore = otpChallengeStore;
        }

        // 1. Trang danh sách User
        public async Task<IActionResult> Index1()
        {
            var users = await _userHelper.GetAllUsersWithRolesAsync();
            return View(users);
        }

        // 2. Trang Edit Role
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userHelper.GetUserByIdAsync(id);
            if (user == null) return NotFound();

            ViewBag.Roles = _roleManager.Roles
                .Where(role => UserHelper.AssignableRoles.Contains(role.Name, StringComparer.Ordinal))
                .Select(r => new SelectListItem { Text = r.Name, Value = r.Name })
                .ToList();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, string roleName)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var user = await _userHelper.GetUserByIdAsync(id);
            if (user == null) return NotFound();
            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actorId)) return Forbid();

            var result = await _userHelper.ChangeUserRoleAsync(
                user,
                roleName,
                actorId);
            if (result.Succeeded)
            {
                await _activityLogHelper.LogActivityAsync(User.Identity?.Name ?? actorId, "Update", $"Changed role for user {user.Email} to {roleName}");
                TempData["SuccessMessage"] = "User role updated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(error => error.Description));
            }
            return RedirectToAction(nameof(Index1));
        }

        // 3. Ngừng hoạt động (Khóa tài khoản) & Gửi Mail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userHelper.GetUserByIdAsync(id);
            if (user != null)
            {
                bool isLocked = user.LockoutEnd > DateTimeOffset.Now;
                var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(actorId)) return Forbid();
                if (string.Equals(user.Id, actorId, StringComparison.Ordinal))
                {
                    TempData["ErrorMessage"] = "Administrators cannot change their own account status.";
                    return RedirectToAction(nameof(Index1));
                }

                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    TempData["ErrorMessage"] = "The account has no email address for status notifications.";
                    return RedirectToAction(nameof(Index1));
                }

                if (!isLocked && await _userHelper.IsUserInRoleAsync(user, "Admin"))
                {
                    var admins = await _userHelper.GetAllUsersInRoleAsync("Admin");
                    var activeAdmins = admins.Count(admin => admin.LockoutEnd == null || admin.LockoutEnd <= DateTimeOffset.Now);
                    if (activeAdmins <= 1)
                    {
                        TempData["ErrorMessage"] = "The last active administrator cannot be suspended.";
                        return RedirectToAction(nameof(Index1));
                    }
                }

                string emailSubject = "";
                string emailBody = "";
                string statusAction = "";

                if (isLocked)
                {
                    // Đang khóa -> Mở khóa
                    user.LockoutEnd = DateTimeOffset.Now;
                    statusAction = "Reactivated";
                    emailSubject = "Your DevArt Academy Account has been Reactivated";
                    emailBody = $"<h3>Hello {user.FirstName},</h3><p>Good news! Your account has been reactivated. You can now log in and access the system normally.</p>";
                }
                else
                {
                    // Đang mở -> Khóa tới năm 2099
                    user.LockoutEnd = DateTimeOffset.MaxValue;
                    statusAction = "Suspended";
                    emailSubject = "Account Suspension Notice - DevArt Academy";
                    emailBody = $"<h3>Hello {user.FirstName},</h3><p>We are writing to inform you that your account has been temporarily <strong>suspended</strong> by the Administrator. If you believe this is a mistake, please contact support.</p>";
                }

                var updateResult = await _userHelper.UpdateUserAsync(user);
                if (!updateResult.Succeeded)
                {
                    TempData["ErrorMessage"] = string.Join(" ", updateResult.Errors.Select(error => error.Description));
                    return RedirectToAction(nameof(Index1));
                }

                // Gửi thông báo cho User bị khóa/mở khóa
                var mailResult = _mailHelper.SendEmail(user.Email, emailSubject, emailBody);
                if (mailResult == null || !mailResult.IsSuccess)
                {
                    TempData["ErrorMessage"] = "The account status changed, but notification delivery failed.";
                    return RedirectToAction(nameof(Index1));
                }

                // Ghi Log hành động của Admin
                await _activityLogHelper.LogActivityAsync(User.Identity?.Name ?? actorId, "Update", $"{statusAction} account: {user.Email}");
            }
            return RedirectToAction(nameof(Index1));
        }

        // GET only displays a confirmation screen; the POST starts the OTP challenge.
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userHelper.GetUserByIdAsync(id);
            if (user == null) return NotFound();

            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await CanDeleteUserAsync(user, actorId))
            {
                TempData["ErrorMessage"] = "You cannot delete your own account or the last active administrator.";
                return RedirectToAction(nameof(Index1));
            }

            ViewBag.UserEmailToDelete = user.Email;
            ViewBag.TargetUserId = user.Id;
            ViewBag.RequestOtp = false;
            return View("ConfirmDelete");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id, bool requestOtp)
        {
            var user = await _userHelper.GetUserByIdAsync(id);
            if (user == null) return NotFound();

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(adminId)) return Forbid();
            var administrator = await _userHelper.GetUserByIdAsync(adminId);
            var administratorEmail = administrator?.Email;
            if (string.IsNullOrWhiteSpace(administratorEmail)) return Forbid();
            if (!await CanDeleteUserAsync(user, adminId))
            {
                TempData["ErrorMessage"] = "You cannot delete your own account or the last active administrator.";
                return RedirectToAction(nameof(Index1));
            }

            var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var owner = $"{HttpContext.Session.Id}:{adminId}";
            var challenge = _otpChallengeStore.Create(
                DeleteOtpPurpose,
                owner,
                user.Id,
                adminId,
                rememberMe: false,
                otp);
            HttpContext.Session.SetString(DeleteChallengeSessionKey, challenge.ChallengeId);

            var emailBody = $@"
                <div style='font-family: Arial; padding: 20px; border: 1px solid #ff4d4d; border-radius: 5px;'>
                    <h2 style='color: #ff4d4d;'>Deletion confirmation</h2>
                    <p>You requested to delete the selected user account.</p>
                    <p>Your confirmation code is: <strong style='font-size: 24px;'>{otp}</strong></p>
                    <p>This action is irreversible and deletes associated records.</p>
                </div>";

            var mailResponse = _mailHelper.SendEmail(administratorEmail, "Confirm User Deletion", emailBody);
            if (mailResponse == null || !mailResponse.IsSuccess)
            {
                _otpChallengeStore.Cancel(challenge.ChallengeId);
                HttpContext.Session.Remove(DeleteChallengeSessionKey);
                TempData["ErrorMessage"] = "Unable to send the confirmation code. Please try again later.";
                return RedirectToAction(nameof(Index1));
            }

            ViewBag.UserEmailToDelete = user.Email;
            ViewBag.TargetUserId = user.Id;
            ViewBag.RequestOtp = true;
            return View("ConfirmDelete");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelete(string otpCode)
        {
            var challengeId = HttpContext.Session.GetString(DeleteChallengeSessionKey);
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(challengeId) || string.IsNullOrWhiteSpace(adminId))
            {
                return RedirectToAction(nameof(Index1));
            }

            var owner = $"{HttpContext.Session.Id}:{adminId}";
            var pending = _otpChallengeStore.GetPending(challengeId, DeleteOtpPurpose, owner);
            if (pending == null)
            {
                HttpContext.Session.Remove(DeleteChallengeSessionKey);
                TempData["ErrorMessage"] = "The confirmation code expired. Please start again.";
                return RedirectToAction(nameof(Index1));
            }

            var validation = _otpChallengeStore.ValidateAndConsume(
                challengeId,
                DeleteOtpPurpose,
                owner,
                pending.SubjectId,
                adminId,
                otpCode);
            if (validation.Status == OtpValidationStatus.Invalid)
            {
                var target = await _userHelper.GetUserByIdAsync(pending.SubjectId);
                ViewBag.UserEmailToDelete = target?.Email;
                ViewBag.TargetUserId = pending.SubjectId;
                ViewBag.RequestOtp = true;
                ModelState.AddModelError(string.Empty, "Invalid confirmation code. Please try again.");
                return View("ConfirmDelete");
            }

            HttpContext.Session.Remove(DeleteChallengeSessionKey);
            if (validation.Status != OtpValidationStatus.Valid)
            {
                TempData["ErrorMessage"] = "The confirmation code expired or exceeded the allowed attempts.";
                return RedirectToAction(nameof(Index1));
            }

            var user = await _userHelper.GetUserByIdAsync(pending.SubjectId);
            if (user == null) return NotFound();

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            if (!await CanDeleteUserAsync(user, adminId))
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "You cannot delete your own account or the last active administrator.";
                return RedirectToAction(nameof(Index1));
            }

            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (student != null) _context.Students.Remove(student);
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == user.Id);
            if (teacher != null) _context.Teachers.Remove(teacher);
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == user.Id);
            if (employee != null) _context.Employees.Remove(employee);

            await _context.SaveChangesAsync();
            var deleteResult = await _userHelper.DeleteUserAsync(user);
            if (!deleteResult.Succeeded)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = string.Join(" ", deleteResult.Errors.Select(error => error.Description));
                return RedirectToAction(nameof(Index1));
            }

            await transaction.CommitAsync();
            await _activityLogHelper.LogActivityAsync(
                User.Identity?.Name ?? adminId,
                "Delete",
                "Deleted a user account and associated records.");
            TempData["SuccessMessage"] = "User deleted successfully.";
            return RedirectToAction(nameof(Index1));
        }

        private async Task<bool> CanDeleteUserAsync(User target, string? actorId)
        {
            var targetIsAdmin = await _userHelper.IsUserInRoleAsync(target, "Admin");
            var admins = targetIsAdmin
                ? await _userHelper.GetAllUsersInRoleAsync("Admin")
                : new List<User>();
            var activeAdminCount = admins.Count(admin =>
                admin.LockoutEnd == null || admin.LockoutEnd <= DateTimeOffset.UtcNow);

            return UserAdministrationPolicy.CanDeleteUser(
                actorId,
                target.Id,
                targetIsAdmin,
                activeAdminCount);
        }
    }
}
