using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;
using System.Linq;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Controllers
{
    [Authorize(Roles = "Employee,Admin")]
    public class SubjectsController : Controller
    {
        private readonly ISubjectRepository _subjectRepository;
        private readonly IConverterHelper _converterHelper;
        private readonly IActivityLogHelper _activityLogHelper;

        public SubjectsController(
            ISubjectRepository subjectRepository,
            IConverterHelper converterHelper,
            IActivityLogHelper activityLogHelper)
        {
            _subjectRepository = subjectRepository;
            _converterHelper = converterHelper;
            _activityLogHelper = activityLogHelper;
        }

        // GET: Subject/Index
        public async Task<IActionResult> Index()
        {
            var subjects = await _subjectRepository.GetAll().ToListAsync();
            var subjectViewModels = subjects.Select(s => _converterHelper.ToSubjectViewModel(s)).ToList();
            return View(subjectViewModels);
        }

        // GET: Subject/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Subject/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubjectViewModel model)
        {
            if (ModelState.IsValid)
            {
                var subject = await _converterHelper.ToSubjectAsync(model, true);
                if (subject == null) return new NotFoundViewResult("SubjectNotFound");
                await _subjectRepository.CreateAsync(subject);

                // ĐÃ SỬA: Dùng subject.Name thay vì model.Name
                await _activityLogHelper.LogActivityAsync(
                    User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                    "Create",
                    $"Added new subject: {subject.Name}"
                );

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: Subject/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var subject = await _subjectRepository.GetByIdAsync(id);
            if (subject == null)
            {
                return new NotFoundViewResult("SubjectNotFound");
            }

            var model = _converterHelper.ToSubjectViewModel(subject);
            return View(model);
        }

        // POST: Subject/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SubjectViewModel model)
        {
            if (ModelState.IsValid)
            {
                var subject = await _converterHelper.ToSubjectAsync(model, false);
                if (subject == null) return new NotFoundViewResult("SubjectNotFound");
                await _subjectRepository.UpdateAsync(subject);

                // ĐÃ SỬA: Dùng subject.Name thay vì model.Name
                await _activityLogHelper.LogActivityAsync(
                    User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                    "Update",
                    $"Updated subject information for: {subject.Name}"
                );

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: Subject/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var subject = await _subjectRepository.GetByIdAsync(id);
            if (subject == null)
            {
                return new NotFoundViewResult("SubjectNotFound");
            }

            var model = _converterHelper.ToSubjectViewModel(subject);
            return View(model);
        }

        // POST: Subject/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var subject = await _subjectRepository.GetByIdAsync(id);
            if (subject == null)
            {
                return new NotFoundViewResult("SubjectNotFound");
            }

            string subjectName = subject.Name;

            try
            {
                await _subjectRepository.DeleteAsync(subject);

                await _activityLogHelper.LogActivityAsync(
                    User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                    "Delete",
                    $"Deleted subject: {subjectName}"
                );

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException != null && ex.InnerException.Message.Contains("DELETE"))
                {
                    ViewBag.ErrorTitle = $"{subject.Name} is being used!";
                    ViewBag.ErrorMessage = "This subject cannot be deleted because it has associated data.";
                }
                return View("Error");
            }
        }

        public IActionResult SubjectNotFound()
        {
            return View();
        }
    }
}
