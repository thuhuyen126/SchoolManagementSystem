using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;

namespace SchoolManagementSystem.Helpers
{
    public class UserHelper : IUserHelper
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly ITeacherRepository _teacherRepository;
        private readonly IAlertRepository _alertRepository;
        private readonly SchoolDbContext _context;

        public static IReadOnlyCollection<string> AssignableRoles => UserAdministrationPolicy.AssignableRoles;

        public UserHelper(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            RoleManager<IdentityRole> roleManager,
            IEmployeeRepository employeeRepository,
            IStudentRepository studentRepository,
            ITeacherRepository teacherRepository,
            IAlertRepository alertRepository,
            SchoolDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _employeeRepository = employeeRepository;
            _studentRepository = studentRepository;
            _teacherRepository = teacherRepository;
            _alertRepository = alertRepository;
            _context = context;
        }

        public async Task<IdentityResult> AddUserAsync(User user, string password)
        {
            return await _userManager.CreateAsync(user, password);
        }

        public async Task<IdentityResult> CreateUserWithRoleAsync(User user, string password, string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName) || !await _roleManager.RoleExistsAsync(roleName))
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "RoleNotFound",
                    Description = "The requested role does not exist."
                });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return createResult;
            }

            var roleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return roleResult;
            }

            await transaction.CommitAsync();
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> CreateUserWithRoleAsync(User user, string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName) || !await _roleManager.RoleExistsAsync(roleName))
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "RoleNotFound",
                    Description = "The requested role does not exist."
                });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return createResult;
            }

            var roleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return roleResult;
            }

            await transaction.CommitAsync();
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> AddUserToRoleAsync(User user, string roleName)
        {
            return await _userManager.AddToRoleAsync(user, roleName);
        }

        public async Task<IdentityResult> ChangePasswordAsync(User user, string oldPassword, string newPassword)
        {
            return await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);
        }

        public async Task CheckRoleAsync(string roleName)
        {
            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                await _roleManager.CreateAsync(new IdentityRole
                {
                    Name = roleName
                });
            }
        }

        public async Task<IdentityResult> ConfirmEmailAsync(User user, string token)
        {
            return await _userManager.ConfirmEmailAsync(user, token);
        }

        public async Task<string> GenerateEmailConfirmationTokenAsync(User user)
        {
            return await _userManager.GenerateEmailConfirmationTokenAsync(user);
        }

        public async Task<string> GeneratePasswordResetTokenAsync(User user)
        {
            return await _userManager.GeneratePasswordResetTokenAsync(user);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _userManager.FindByEmailAsync(email);
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
        }

        public async Task<bool> IsUserInRoleAsync(User user, string roleName)
        {
            return await _userManager.IsInRoleAsync(user, roleName);
        }

        public async Task<SignInResult> LoginAsync(LoginViewModel model)
        {
            return await _signInManager.PasswordSignInAsync(
                model.Username,
                model.Password,
                model.RememberMe,
                false);
        }

        public async Task LogoutAsync()
        {
            await _signInManager.SignOutAsync();
        }

        public async Task<IdentityResult> ResetPasswordAsync(User user, string token, string password)
        {
            return await _userManager.ResetPasswordAsync(user, token, password);
        }

        public async Task<IdentityResult> UpdateUserAsync(User user)
        {
            return await _userManager.UpdateAsync(user);
        }

        public async Task<SignInResult> ValidatePasswordAsync(User user, string password)
        {
            return await _signInManager.CheckPasswordSignInAsync(
                user,
                password,
                true);
        }

        public async Task<bool> CanSignInAsync(User user)
        {
            return !await _userManager.IsLockedOutAsync(user) &&
                await _signInManager.CanSignInAsync(user);
        }

        public async Task RemoveUserFromRoleAsync(User user, string roleName)
        {
            await _userManager.RemoveFromRoleAsync(user, roleName);
        }

        public async Task<List<User>> GetAllUsersInRoleAsync(string roleName)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
            return usersInRole.ToList();
        }

        public async Task NotifyAdministrativeEmployeesPendingUserAsync(User user)
        {
            var administrativeEmployees = await _employeeRepository.GetAdministrativeEmployeesAsync();

            foreach (var adminEmployee in administrativeEmployees)
            {
                var alert = new Alert
                {
                    Message = $"New User 'Pending': {user.FullName}",
                    CreatedAt = DateTime.UtcNow,
                    IsResolved = false,
                    EmployeeId = adminEmployee.Id
                };

                await _alertRepository.CreateAsync(alert);
            }
        }

        public async Task<string?> GetRoleAsync(User user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return roles.FirstOrDefault(); // Returns the first role associated with the user
        }

        public async Task UpdateUserDataByRoleAsync(User user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Employee"))
            {
                var employee = await _employeeRepository.GetEmployeeByUserIdAsync(user.Id);
                if (employee != null)
                {
                    employee.FirstName = user.FirstName;
                    employee.LastName = user.LastName;
                    await _employeeRepository.UpdateAsync(employee);
                }
            }
            else if (roles.Contains("Student"))
            {
                var student = await _studentRepository.GetStudentByUserIdAsync(user.Id);
                if (student != null)
                {
                    student.FirstName = user.FirstName;
                    student.LastName = user.LastName;
                    await _studentRepository.UpdateAsync(student);
                }
            }
            else if (roles.Contains("Teacher"))
            {
                var teacher = await _teacherRepository.GetTeacherByUserIdAsync(user.Id);
                if (teacher != null)
                {
                    teacher.FirstName = user.FirstName;
                    teacher.LastName = user.LastName;
                    await _teacherRepository.UpdateAsync(teacher);
                }
            }
        }

        public async Task<Employee?> GetEmployeeByUserAsync(string userEmail)
        {
            var user = await GetUserByEmailAsync(userEmail);  // Search for the user by email
            if (user == null) return null;

            return await _employeeRepository.GetEmployeeByUserIdAsync(user.Id); // Search for the employee by UserId
        }

        public async Task<IList<string>> GetRolesAsync(User user)
        {
            return await _userManager.GetRolesAsync(user);
        }

        public async Task<IdentityResult> RemoveUserFromRolesAsync(User user, IEnumerable<string> roles)
        {
            return await _userManager.RemoveFromRolesAsync(user, roles);
        }

        // 1. Hàm lấy danh sách User kèm theo Role của họ
        public async Task<List<Models.UserWithRoleViewModel>> GetAllUsersWithRolesAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            var userWithRoles = new List<Models.UserWithRoleViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userWithRoles.Add(new Models.UserWithRoleViewModel
                {
                    UserId = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Role = roles.FirstOrDefault() ?? "No Role",
                    // Kiểm tra xem tài khoản có đang bị khóa không
                    IsLocked = user.LockoutEnd > DateTimeOffset.Now
                });
            }
            return userWithRoles;
        }

        // 2. Hàm xóa User
        public async Task<IdentityResult> DeleteUserAsync(Data.Entities.User user)
        {
            return await _userManager.DeleteAsync(user);
        }

        public async Task<IdentityResult> ChangeUserRoleAsync(User user, string roleName, string actorUserId)
        {
            if (user == null || string.IsNullOrWhiteSpace(actorUserId) || string.IsNullOrWhiteSpace(roleName) ||
                !UserAdministrationPolicy.IsAssignableRole(roleName))
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "InvalidRoleAssignment",
                    Description = "The requested user role is not supported."
                });
            }

            if (string.Equals(user.Id, actorUserId, StringComparison.Ordinal))
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "SelfRoleChangeNotAllowed",
                    Description = "Administrators cannot change their own role."
                });
            }

            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "RoleNotFound",
                    Description = "The requested role does not exist."
                });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Count == 1 && string.Equals(currentRoles[0], roleName, StringComparison.Ordinal))
            {
                return IdentityResult.Success;
            }

            if (currentRoles.Contains("Admin", StringComparer.Ordinal) && roleName != "Admin")
            {
                var administrators = await _userManager.GetUsersInRoleAsync("Admin");
                var activeAdministratorCount = administrators.Count(admin =>
                    admin.LockoutEnd == null || admin.LockoutEnd <= DateTimeOffset.Now);
                if (activeAdministratorCount <= 1)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = "LastAdministrator",
                        Description = "The last administrator cannot be demoted."
                    });
                }
            }

            var addResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!addResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return addResult;
            }

            var rolesToRemove = currentRoles
                .Where(role => !string.Equals(role, roleName, StringComparison.Ordinal))
                .ToArray();

            if (rolesToRemove.Length > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return removeResult;
                }
            }

            await transaction.CommitAsync();
            return IdentityResult.Success;
        }

        public async Task SignInDirectlyAsync(Data.Entities.User user, bool rememberMe)
        {
            await _signInManager.SignInAsync(user, rememberMe);
        }
    }
}
