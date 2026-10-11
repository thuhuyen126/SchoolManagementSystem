using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;
using SchoolManagementSystem.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace SchoolManagementSystem.Controllers
{
    [Authorize(Roles = "Employee,Admin")]
    public class StudentsController : Controller
    {
        private readonly IStudentRepository _studentRepository;
        private readonly IBlobHelper _blobHelper;
        private readonly IConverterHelper _converterHelper;
        private readonly ISchoolClassRepository _schoolClassRepository;
        private readonly IUserHelper _userHelper;
        private readonly ILogger<StudentsController> _logger;
        private readonly IActivityLogHelper _activityLogHelper; // ĐÃ THÊM: Khai báo Log Helper

        public StudentsController(
            IStudentRepository studentRepository,
            IBlobHelper blobHelper,
            IConverterHelper converterHelper,
            ISchoolClassRepository schoolClassRepository,
            IUserHelper userHelper,
            ILogger<StudentsController> logger,
            IActivityLogHelper activityLogHelper) // ĐÃ THÊM: Tiêm Log Helper vào đây
        {
            _studentRepository = studentRepository;
            _blobHelper = blobHelper;
            _converterHelper = converterHelper;
            _schoolClassRepository = schoolClassRepository;
            _userHelper = userHelper;
            _logger = logger;
            _activityLogHelper = activityLogHelper; // Gán giá trị
        }

        // GET: Students
        public async Task<IActionResult> Index()
        {
            var students = await _studentRepository.GetAllWithIncludesAsync();
            return View(students);
        }

        // GET: Students/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return new NotFoundViewResult("StudentNotFound");

            var student = await _studentRepository.GetStudentWithCoursesAsync(id.Value);
            if (student == null)
                return new NotFoundViewResult("StudentNotFound");

            return View(_converterHelper.ToStudentViewModel(student));
        }

        // GET: Students/Create
        public async Task<IActionResult> Create()
        {
            var pendingUsers = await _userHelper.GetAllUsersInRoleAsync("Pending");

            var model = new StudentViewModel
            {
                PendingUsers = pendingUsers
            };

            ViewBag.PendingUsers = new SelectList(pendingUsers, "Id", "Email");
            await LoadDropdownData();

            return View(model);
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudentViewModel model)
        {
            if (ModelState.IsValid)
            {
                var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(actorId)) return Forbid();

                if (model.SchoolClassId.HasValue)
                {
                    var schoolClass = await _schoolClassRepository.GetByIdAsync(model.SchoolClassId.Value);
                    if (schoolClass == null || schoolClass.CourseId == null)
                    {
                        TempData["ErrorMessage"] = "The selected school class does not have an associated course.";
                        ModelState.AddModelError("SchoolClassId", "The selected school class does not have an associated course.");
                        await LoadDropdownData();
                        var pendingUsers = await _userHelper.GetAllUsersInRoleAsync("Pending");
                        ViewBag.PendingUsers = new SelectList(pendingUsers, "Id", "Email");
                        return View(model);
                    }
                }

                try
                {
                    Guid imageId = Guid.Empty;
                    if (model.ImageFile != null && model.ImageFile.Length > 0)
                    {
                        imageId = await _blobHelper.UploadBlobAsync(model.ImageFile, "students");
                    }

                    var student = await _converterHelper.ToStudentAsync(model, imageId, true);
                    await _studentRepository.CreateAsync(student);

                    var user = await _userHelper.GetUserByIdAsync(model.UserId);
                    if (user == null)
                    {
                        await _studentRepository.DeleteAsync(student);
                        return new NotFoundViewResult("UserNotFound");
                    }
                    var roleResult = await _userHelper.ChangeUserRoleAsync(
                        user,
                        "Student",
                        actorId);
                    if (!roleResult.Succeeded)
                    {
                        await _studentRepository.DeleteAsync(student);
                        throw new InvalidOperationException("Could not assign the Student role.");
                    }

                    // --- GHI LOG: TẠO MỚI HỌC SINH ---
                    await _activityLogHelper.LogActivityAsync(
                        User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                        "Create",
                        $"Added new student: {model.FirstName} {model.LastName}"
                    );
                    // ---------------------------------

                    TempData["SuccessMessage"] = "Student created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating student");
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                }
            }

            var updatedPendingUsers = await _userHelper.GetAllUsersInRoleAsync("Pending");
            ViewBag.PendingUsers = new SelectList(updatedPendingUsers, "Id", "Email");
            await LoadDropdownData();
            return View(model);
        }

        // GET: Students/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return new NotFoundViewResult("StudentNotFound");

            var student = await _studentRepository.GetByIdAsync(id.Value);
            if (student == null) return new NotFoundViewResult("StudentNotFound");

            var model = _converterHelper.ToStudentViewModel(student);
            await LoadDropdownData();

            return View(model);
        }

        // POST: Students/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StudentViewModel model)
        {
            if (id != model.Id) return new NotFoundViewResult("StudentNotFound");

            if (ModelState.IsValid)
            {
                if (model.SchoolClassId.HasValue)
                {
                    var schoolClass = await _schoolClassRepository.GetByIdAsync(model.SchoolClassId.Value);
                    if (schoolClass == null || schoolClass.CourseId == null)
                    {
                        TempData["ErrorMessage"] = "The selected school class does not have an associated course.";
                        ModelState.AddModelError("SchoolClassId", "The selected school class does not have an associated course.");
                        await LoadDropdownData();
                        var pendingUsers = await _userHelper.GetAllUsersInRoleAsync("Pending");
                        ViewBag.PendingUsers = new SelectList(pendingUsers, "Id", "Email");
                        return View(model);
                    }
                }

                try
                {
                    Guid imageId = model.ImageId;

                    if (model.ImageFile != null && model.ImageFile.Length > 0)
                    {
                        imageId = await _blobHelper.UploadBlobAsync(model.ImageFile, "students");
                    }

                    var student = await _converterHelper.ToStudentAsync(model, imageId, false);
                    await _studentRepository.UpdateAsync(student);

                    var user = await _userHelper.GetUserByIdAsync(model.UserId);
                    if (user != null)
                    {
                        user.FirstName = model.FirstName;
                        user.LastName = model.LastName;
                        await _userHelper.UpdateUserAsync(user);
                    }

                    // --- GHI LOG: CẬP NHẬT HỌC SINH ---
                    await _activityLogHelper.LogActivityAsync(
                        User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                        "Update",
                        $"Updated student information for: {model.FirstName} {model.LastName}"
                    );
                    // ----------------------------------

                    TempData["SuccessMessage"] = "Student updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await StudentExists(model.Id)) return new NotFoundViewResult("StudentNotFound");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating student");
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                }
            }

            var updatedPendingUsers = await _userHelper.GetAllUsersInRoleAsync("Pending");
            ViewBag.PendingUsers = new SelectList(updatedPendingUsers, "Id", "Email");
            await LoadDropdownData();
            return View(model);
        }

        // GET: Students/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return new NotFoundViewResult("StudentNotFound");

            var student = await _studentRepository.GetStudentWithCoursesAsync(id.Value);

            if (student == null) return new NotFoundViewResult("StudentNotFound");

            return View(_converterHelper.ToStudentViewModel(student));
        }

        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _studentRepository.GetByIdAsync(id);

            if (student == null) return new NotFoundViewResult("StudentNotFound");

            // Lưu lại tên học sinh trước khi xóa để ghi Log
            string studentName = $"{student.FirstName} {student.LastName}";

            try
            {
                await _studentRepository.DeleteAsync(student);

                // --- GHI LOG: XÓA HỌC SINH ---
                await _activityLogHelper.LogActivityAsync(
                    User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                    "Delete",
                    $"Deleted student: {studentName}"
                );
                // -----------------------------

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException != null && ex.InnerException.Message.Contains("DELETE"))
                {
                    ViewBag.ErrorTitle = $"{student.Id} está a ser usado!";
                    ViewBag.ErrorMessage = $"Este estudante não pode ser apagado. Tente apagar primeiro as associações.";
                }

                return View("Error");
            }
        }

        private async Task<bool> StudentExists(int id)
        {
            return await _studentRepository.ExistAsync(id);
        }

        private async Task LoadDropdownData()
        {
            var schoolClasses = await _schoolClassRepository.GetAllAsync();
            var classList = schoolClasses.ToList();
            ViewBag.SchoolClassId = new SelectList(classList, "Id", "ClassName");
        }

        public IActionResult StudentNotFound()
        {
            return View();
        }
    }
}
