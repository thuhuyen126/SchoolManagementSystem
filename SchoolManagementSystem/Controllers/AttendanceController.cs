using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Controllers
{

    public class AttendanceController : Controller
    {
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly ISubjectRepository _subjectRepository;
        private readonly IConverterHelper _converterHelper;
        private readonly ISchoolClassRepository _schoolClassRepository;
        private readonly ILogger<AttendanceController> _logger;
        // ĐÃ THÊM: Khai báo Log Helper
        private readonly IActivityLogHelper _activityLogHelper;

        public AttendanceController(
            IAttendanceRepository attendanceRepository,
            IStudentRepository studentRepository,
            ISubjectRepository subjectRepository,
            IConverterHelper converterHelper,
            ISchoolClassRepository schoolClassRepository,
            ILogger<AttendanceController> logger,
            // ĐÃ THÊM: Tiêm Log Helper vào constructor
            IActivityLogHelper activityLogHelper)
        {
            _attendanceRepository = attendanceRepository;
            _studentRepository = studentRepository;
            _subjectRepository = subjectRepository;
            _converterHelper = converterHelper;
            _schoolClassRepository = schoolClassRepository;
            _logger = logger;
            // Gán giá trị
            _activityLogHelper = activityLogHelper;
        }

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
                    var studentAttendances = new List<StudentAttendanceViewModel>();

                    foreach (var student in students)
                    {
                        // Get only the subjects in which the student is enrolled
                        var subjectsForStudent = await _subjectRepository.GetSubjectsByStudentIdAsync(student.Id);

                        // Calculate the total number of classes by adding only the classes from the student's subjects
                        int totalClasses = subjectsForStudent.Sum(s => s.TotalClasses);

                        var attendances = await _attendanceRepository.GetAttendancesByStudentIdAsync(student.Id);

                        studentAttendances.Add(new StudentAttendanceViewModel
                        {
                            Student = student,
                            Attendances = attendances,
                            TotalClasses = totalClasses // Defines TotalClasses only with subjects associated with the student
                        });
                    }

                    return View(studentAttendances);
                }

                return View(new List<StudentAttendanceViewModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attendance list.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the attendance list. Please try again later.");
                return View(new List<StudentAttendanceViewModel>());
            }
        }


        // GET: Attendance/Details/5
        [Authorize(Roles = "Employee,Admin")]
        public async Task<IActionResult> Details(int studentId, int classId)
        {
            try
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                if (student == null)
                {
                    return new NotFoundViewResult("StudentNotFound");
                }

                var subjects = await _attendanceRepository.GetSubjectsByStudentIdAsync(studentId);
                var attendances = await _attendanceRepository.GetAttendancesByStudentIdAsync(studentId);

                var model = subjects.Select(subject => new StudentSubjectAttendanceViewModel
                {
                    Subject = subject,
                    Attendance = attendances.FirstOrDefault(a => a.SubjectId == subject.Id),
                    StudentId = studentId,
                    StudentName = $"{student.FirstName} {student.LastName}",
                    AllAttendances = attendances.Where(a => a.SubjectId == subject.Id).ToList(),
                    CanAddAttendance = (attendances.Count(a => a.SubjectId == subject.Id) < subject.TotalClasses)
                }).ToList();

                ViewBag.ClassId = classId;
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attendance details.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading attendance details. Please try again later.");
                return View(new List<StudentSubjectAttendanceViewModel>());
            }
        }


        // GET: Attendance/AddAttendance
        [Authorize(Roles = "Employee,Admin")]
        public async Task<IActionResult> AddAttendance(int studentId, int subjectId)
        {
            try
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                var subject = await _subjectRepository.GetByIdAsync(subjectId);

                if (student == null || subject == null)
                {
                    return new NotFoundViewResult("StudentOrSubjectNotFound");
                }

                var model = new AttendanceViewModel
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
                _logger.LogError(ex, "Error loading data to add attendance.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading data to add attendance. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Attendance/AddAttendance
        [Authorize(Roles = "Employee,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAttendance(AttendanceViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var attendance = await _converterHelper.ToAttendanceAsync(model, true);
                    if (attendance == null) return new NotFoundViewResult("AttendanceNotFound");
                    await _attendanceRepository.AddAttendanceAsync(attendance);

                    // --- ĐÃ THÊM GHI LOG: ĐIỂM DANH ---
                    var student = await _studentRepository.GetByIdAsync(model.StudentId);
                    var subject = await _subjectRepository.GetByIdAsync(model.SubjectId);
                    string studentName = student != null ? $"{student.FirstName} {student.LastName}" : $"ID {model.StudentId}";
                    string subjectName = subject != null ? subject.Name : $"ID {model.SubjectId}";

                    await _activityLogHelper.LogActivityAsync(
                        User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                        "Create",
                        $"Added attendance for student: {studentName} in subject: {subjectName}"
                    );
                    // ----------------------------------

                    return RedirectToAction(nameof(Details), new { studentId = model.StudentId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding attendance.");
                    ModelState.AddModelError(string.Empty, "An error occurred while adding the attendance. Please try again later.");
                }
            }

            return View(model);
        }

        // GET: Attendance/EditAttendance/5
        [Authorize(Roles = "Employee,Admin")]
        public async Task<IActionResult> EditAttendance(int id)
        {
            try
            {
                var attendance = await _attendanceRepository.GetAttendanceWithDetailsByIdAsync(id);
                if (attendance == null)
                {
                    return new NotFoundViewResult("AttendanceNotFound");
                }

                var model = _converterHelper.ToAttendanceViewModel(attendance);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attendance for editing.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the attendance for editing. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Attendance/EditAttendance
        [Authorize(Roles = "Employee,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAttendance(AttendanceViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var attendance = await _converterHelper.ToAttendanceAsync(model, false);
                    if (attendance == null) return new NotFoundViewResult("AttendanceNotFound");
                    await _attendanceRepository.UpdateAsync(attendance);

                    // --- ĐÃ THÊM GHI LOG: SỬA ĐIỂM DANH ---
                    var student = await _studentRepository.GetByIdAsync(model.StudentId);
                    var subject = await _subjectRepository.GetByIdAsync(model.SubjectId);
                    string studentName = student != null ? $"{student.FirstName} {student.LastName}" : $"ID {model.StudentId}";
                    string subjectName = subject != null ? subject.Name : $"ID {model.SubjectId}";

                    await _activityLogHelper.LogActivityAsync(
                        User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                        "Update",
                        $"Updated attendance for student: {studentName} in subject: {subjectName}"
                    );
                    // --------------------------------------

                    return RedirectToAction(nameof(Details), new { studentId = model.StudentId });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating attendance.");
                    ModelState.AddModelError(string.Empty, "An error occurred while updating the attendance. Please try again later.");
                }
            }

            return View(model);
        }

        // GET: Attendance/DeleteAttendance/5
        [Authorize(Roles = "Employee,Admin")]
        public async Task<IActionResult> DeleteAttendance(int id)
        {
            try
            {
                var attendance = await _attendanceRepository.GetAttendanceWithDetailsByIdAsync(id);
                if (attendance == null)
                {
                    return new NotFoundViewResult("AttendanceNotFound");
                }

                return View(_converterHelper.ToAttendanceViewModel(attendance));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading attendance for deletion.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading the attendance for deletion. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Attendance/DeleteAttendance
        [Authorize(Roles = "Employee,Admin")]
        [HttpPost, ActionName("DeleteAttendance")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttendanceConfirmed(int id)
        {
            try
            {
                var attendance = await _attendanceRepository.GetAttendanceWithDetailsByIdAsync(id);
                if (attendance != null)
                {
                    // Lấy thông tin trước khi xóa
                    var student = await _studentRepository.GetByIdAsync(attendance.StudentId);
                    var subject = await _subjectRepository.GetByIdAsync(attendance.SubjectId);
                    string studentName = student != null ? $"{student.FirstName} {student.LastName}" : $"ID {attendance.StudentId}";
                    string subjectName = subject != null ? subject.Name : $"ID {attendance.SubjectId}";

                    await _attendanceRepository.DeleteAsync(attendance);

                    // --- ĐÃ THÊM GHI LOG: XÓA ĐIỂM DANH ---
                    await _activityLogHelper.LogActivityAsync(
                        User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                        "Delete",
                        $"Deleted attendance for student: {studentName} in subject: {subjectName}"
                    );
                    // --------------------------------------
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attendance.");
                ModelState.AddModelError(string.Empty, "An error occurred while deleting the attendance. Please try again later.");
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Attendance/MyAttendances
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyAttendances()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userId)) return Forbid();
                var studentId = await _studentRepository.GetStudentIdByUserIdAsync(userId);

                if (studentId == null)
                {
                    return new NotFoundViewResult("StudentNotFound");
                }

                // Get the student's subjects and their absences
                var subjects = await _subjectRepository.GetSubjectsByStudentIdAsync(studentId.Value);
                var attendances = await _attendanceRepository.GetAttendancesByStudentIdAsync(studentId.Value);
                var student = await _studentRepository.GetByIdAsync(studentId.Value);
                if (student == null) return new NotFoundViewResult("AttendanceNotFound");

                // Calculate total classes and total absences
                int totalClasses = subjects.Sum(s => s.TotalClasses);
                int totalAbsences = attendances.Count;

                // Determine overall attendance status based on absences
                string overallStatus = totalAbsences >= totalClasses * 0.3 ? "Failed" : "Passed";

                // Prepare the model for each subject
                var model = subjects.Select(subject => new StudentSubjectAttendanceViewModel
                {
                    Subject = subject,
                    AllAttendances = attendances.Where(a => a.SubjectId == subject.Id).ToList(),
                    StudentId = studentId.Value,
                    StudentName = $"{student.FirstName} {student.LastName}"
                }).ToList();

                // Pass the total number of absences and the general status to the ViewBag
                ViewBag.TotalClasses = totalClasses;
                ViewBag.TotalAbsences = totalAbsences;
                ViewBag.OverallAttendanceStatus = overallStatus;

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading student's attendances.");
                ModelState.AddModelError(string.Empty, "An error occurred while loading your attendances. Please try again later.");
                return View(new List<StudentSubjectAttendanceViewModel>());
            }
        }


        public IActionResult StudentNotFound()
        {
            return View();
        }

        public IActionResult AttendanceNotFound()
        {
            return View();
        }

        public IActionResult StudentOrSubjectNotFound()
        {
            return View();
        }
    }
}
