using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SchoolManagementSystem.Controllers
{
    public class GradesController : Controller
    {
        private readonly IGradeRepository _gradeRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly ISchoolClassRepository _schoolClassRepository;
        private readonly IConverterHelper _converterHelper;
        private readonly ISubjectRepository _subjectRepository;
        private readonly ILogger<GradesController> _logger;
        // ĐÃ THÊM: Khai báo Log Helper
        private readonly IActivityLogHelper _activityLogHelper;

        public GradesController(
            IGradeRepository gradeRepository,
            IStudentRepository studentRepository,
            ISchoolClassRepository schoolClassRepository,
            IConverterHelper converterHelper,
            ISubjectRepository subjectRepository,
            ILogger<GradesController> logger,
            // ĐÃ THÊM: Tiêm Log Helper vào constructor
            IActivityLogHelper activityLogHelper)
        {
            _gradeRepository = gradeRepository;
            _studentRepository = studentRepository;
            _schoolClassRepository = schoolClassRepository;
            _converterHelper = converterHelper;
            _subjectRepository = subjectRepository;
            _logger = logger;
            // Gán giá trị
            _activityLogHelper = activityLogHelper;
        }

        // GET: Grades
        [Authorize(Roles = "Employee,Admin")]
        public async Task<IActionResult> Index(int? classId)
        {
            try
            {
                var schoolClasses = await _schoolClassRepository.GetAllAsync();
                ViewBag.Classes = schoolClasses.Select(sc => new SelectListItem
                {
                    Value = sc.Id.ToString(),
                    Text = sc.ClassName
                }).ToList();

                if (classId.HasValue)
                {
                    var students = await _studentRepository.GetStudentsBySchoolClassIdAsync(classId.Value);
                    var studentAverages = students.Select(student => new StudentGradeAverageViewModel
                    {
                        Student = student // Includes the Student entity, which contains the grades
                    }).ToList();

                    return View(studentAverages);
                }

                return View(new List<StudentGradeAverageViewModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading grades list.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the grades list. Please try again later.");
                return View(new List<StudentGradeAverageViewModel>());
            }
        }

        // GET: Grades/Details/5
        [Authorize(Roles = "Employee,Admin")]
        public async Task<IActionResult> Details(int studentId)
        {
            try
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                if (student == null)
                {
                    return new NotFoundViewResult("GradeNotFound");
                }

                var subjects = await _gradeRepository.GetSubjectsByStudentIdAsync(studentId);
                var grades = await _gradeRepository.GetGradesByStudentIdAsync(studentId);

                var model = subjects.Select(subject => new StudentSubjectGradeViewModel
                {
                    Subject = subject,
                    Grade = grades.FirstOrDefault(g => g.SubjectId == subject.Id),
                    StudentId = studentId,
                    StudentName = $"{student.FirstName} {student.LastName}"
                }).ToList();

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student details.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading student details. Please try again later.");
                return View(new List<StudentSubjectGradeViewModel>());
            }
        }

        // GET: Grades/AddGrade/5
        [Authorize(Roles = "Employee,Admin")]
        public async Task<IActionResult> AddGrade(int studentId, int subjectId)
        {
            try
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                if (student == null)
                {
                    return new NotFoundViewResult("GradeNotFound");
                }

                var subject = await _subjectRepository.GetByIdAsync(subjectId);
                if (subject == null)
                {
                    return new NotFoundViewResult("GradeNotFound");
                }

                var model = new GradeViewModel
                {
                    StudentId = studentId,
                    SubjectId = subjectId,
                    StudentName = $"{student.FirstName} {student.LastName}",
                    SubjectName = subject.Name
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data to add a grade.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading data to add a grade. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Grades/AddGrade
        [Authorize(Roles = "Employee,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddGrade(GradeViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var grade = await _converterHelper.ToGradeAsync(model, true);
                    if (grade == null) return new NotFoundViewResult("GradeNotFound");
                    await _gradeRepository.CreateAsync(grade);

                    // --- ĐÃ THÊM GHI LOG: TẠO ĐIỂM SỐ ---
                    var student = await _studentRepository.GetByIdAsync(model.StudentId);
                    var subject = await _subjectRepository.GetByIdAsync(model.SubjectId);
                    string studentName = student != null ? $"{student.FirstName} {student.LastName}" : $"ID {model.StudentId}";
                    string subjectName = subject != null ? subject.Name : $"ID {model.SubjectId}";

                    await _activityLogHelper.LogActivityAsync(
                        User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                        "Create",
                        $"Added grade for student: {studentName} in subject: {subjectName}"
                    );
                    // ------------------------------------

                    return RedirectToAction(nameof(Details), new { studentId = model.StudentId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding grade.");
                    ModelState.AddModelError(string.Empty, "An error occurred while adding the grade. Please try again later.");
                }
            }

            return View(model);
        }

        // GET: Grades/EditGrade/5
        [Authorize(Roles = "Employee,Admin")]
        public async Task<IActionResult> EditGrade(int id)
        {
            try
            {
                var grade = await _gradeRepository.GetGradeWithDetailsByIdAsync(id);
                if (grade == null)
                {
                    return new NotFoundViewResult("GradeNotFound");
                }

                var model = _converterHelper.ToGradeViewModel(grade);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading grade for editing.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the grade for editing. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Grades/EditGrade
        [Authorize(Roles = "Employee,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditGrade(GradeViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var grade = await _converterHelper.ToGradeAsync(model, false);
                    if (grade == null) return new NotFoundViewResult("GradeNotFound");
                    await _gradeRepository.UpdateAsync(grade);

                    // --- ĐÃ THÊM GHI LOG: CẬP NHẬT ĐIỂM SỐ ---
                    var student = await _studentRepository.GetByIdAsync(model.StudentId);
                    var subject = await _subjectRepository.GetByIdAsync(model.SubjectId);
                    string studentName = student != null ? $"{student.FirstName} {student.LastName}" : $"ID {model.StudentId}";
                    string subjectName = subject != null ? subject.Name : $"ID {model.SubjectId}";

                    await _activityLogHelper.LogActivityAsync(
                        User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                        "Update",
                        $"Updated grade for student: {studentName} in subject: {subjectName}"
                    );
                    // -----------------------------------------

                    return RedirectToAction(nameof(Details), new { studentId = model.StudentId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating grade.");
                    ModelState.AddModelError(string.Empty, "An error occurred while updating the grade. Please try again later.");
                }
            }
            return View(model);
        }

        // GET: Grades/DeleteGrade/5
        [Authorize(Roles = "Employee,Admin")]
        public async Task<IActionResult> DeleteGrade(int id)
        {
            try
            {
                var grade = await _gradeRepository.GetGradeWithDetailsByIdAsync(id);
                if (grade == null)
                {
                    return new NotFoundViewResult("GradeNotFound");
                }
                return View(_converterHelper.ToGradeViewModel(grade));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading grade for deletion.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the grade for deletion. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Grades/DeleteGrade
        [Authorize(Roles = "Employee,Admin")]
        [HttpPost, ActionName("DeleteGrade")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGradeConfirmed(int id)
        {
            try
            {
                var grade = await _gradeRepository.GetGradeWithDetailsByIdAsync(id);
                if (grade != null)
                {
                    // Lấy thông tin để ghi log trước khi xóa
                    var student = await _studentRepository.GetByIdAsync(grade.StudentId);
                    var subject = await _subjectRepository.GetByIdAsync(grade.SubjectId);
                    string studentName = student != null ? $"{student.FirstName} {student.LastName}" : $"ID {grade.StudentId}";
                    string subjectName = subject != null ? subject.Name : $"ID {grade.SubjectId}";

                    await _gradeRepository.DeleteAsync(grade);

                    // --- ĐÃ THÊM GHI LOG: XÓA ĐIỂM SỐ ---
                    await _activityLogHelper.LogActivityAsync(
                        User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                        "Delete",
                        $"Deleted grade for student: {studentName} in subject: {subjectName}"
                    );
                    // ------------------------------------
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting grade.");
                ModelState.AddModelError(string.Empty, "An error occurred while deleting the grade. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // Method for Student
        // GET: Grades/MyGrades
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyGrades()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userId)) return Forbid();
                var studentId = await _studentRepository.GetStudentIdByUserIdAsync(userId);

                if (studentId == null)
                {
                    return new NotFoundViewResult("GradeNotFound");
                }

                var subjects = await _gradeRepository.GetSubjectsByStudentIdAsync(studentId.Value);
                var grades = await _gradeRepository.GetGradesByStudentIdAsync(studentId.Value);
                var student = await _studentRepository.GetByIdAsync(studentId.Value);
                if (student == null) return new NotFoundViewResult("GradeNotFound");

                // Calculate the student's overall grade average
                double overallAverageGrade = grades.Any() ? grades.Average(g => g.Value) : 0;
                string overallGradeStatus = overallAverageGrade >= 9.5 ? "Passed" : "Failed";

                // Prepare the subjects and grades model
                var model = subjects.Select(subject => new StudentSubjectGradeViewModel
                {
                    Subject = subject,
                    Grade = grades.FirstOrDefault(g => g.SubjectId == subject.Id),
                    StudentId = studentId.Value,
                    StudentName = $"{student.FirstName} {student.LastName}"
                }).ToList();

                // Pass the overall average and status to the ViewBag
                ViewBag.OverallAverageGrade = overallAverageGrade.ToString("F2");
                ViewBag.OverallGradeStatus = overallGradeStatus;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student's grades.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading your grades. Please try again later.");
                return View(new List<StudentSubjectGradeViewModel>());
            }
        }

        public IActionResult GradeNotFound()
        {
            return View();
        }

    }
}
