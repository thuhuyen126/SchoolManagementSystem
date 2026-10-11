using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

namespace SchoolManagementSystem.Controllers
{
    [Authorize(Roles = "Employee,Admin")]
    public class SchoolClassesController : Controller
    {
        private readonly ISchoolClassRepository _schoolClassRepository;
        private readonly IConverterHelper _converterHelper;
        private readonly ILogger<SchoolClassesController> _logger;
        // ĐÃ THÊM: Khai báo Log Helper
        private readonly IActivityLogHelper _activityLogHelper;

        public SchoolClassesController(
            ISchoolClassRepository schoolClassRepository,
            IConverterHelper converterHelper,
            ILogger<SchoolClassesController> logger,
            // ĐÃ THÊM: Tiêm Log Helper vào constructor
            IActivityLogHelper activityLogHelper)
        {
            _schoolClassRepository = schoolClassRepository;
            _converterHelper = converterHelper;
            _logger = logger;
            // Gán giá trị cho Log Helper
            _activityLogHelper = activityLogHelper;
        }

        // GET: SchoolClass/Index
        public async Task<IActionResult> Index()
        {
            var schoolClasses = await _schoolClassRepository.GetAll().ToListAsync();
            var schoolClassViewModels = schoolClasses.Select(sc => _converterHelper.ToSchoolClassViewModel(sc)).ToList();
            return View(schoolClassViewModels);
        }

        // GET: SchoolClass/Create
        public IActionResult Create()
        {
            var model = new SchoolClassViewModel();
            return View(model);
        }

        // POST: SchoolClass/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SchoolClassViewModel model)
        {
            if (ModelState.IsValid)
            {
                var schoolClass = await _converterHelper.ToSchoolClassAsync(model, true);
                if (schoolClass == null) return new NotFoundViewResult("SchoolClassNotFound");
                await _schoolClassRepository.CreateAsync(schoolClass);
                _logger.LogInformation("Turma criada com sucesso: {ClassName}", schoolClass.ClassName);

                // --- ĐÃ THÊM GHI LOG: TẠO MỚI LỚP HỌC ---
                await _activityLogHelper.LogActivityAsync(
                    User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                    "Create",
                    $"Added new school class: {schoolClass.ClassName}"
                );
                // ----------------------------------------

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: SchoolClass/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var schoolClass = await _schoolClassRepository.GetByIdAsync(id);
            if (schoolClass == null)
            {
                return new NotFoundViewResult("SchoolClassNotFound");
            }

            var model = _converterHelper.ToSchoolClassViewModel(schoolClass);
            return View(model);
        }

        // POST: SchoolClass/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SchoolClassViewModel model)
        {
            if (ModelState.IsValid)
            {
                var schoolClass = await _converterHelper.ToSchoolClassAsync(model, false);
                if (schoolClass == null) return new NotFoundViewResult("SchoolClassNotFound");
                await _schoolClassRepository.UpdateAsync(schoolClass);
                _logger.LogInformation("Turma editada com sucesso: {ClassName}", schoolClass.ClassName);

                // --- ĐÃ THÊM GHI LOG: CẬP NHẬT LỚP HỌC ---
                await _activityLogHelper.LogActivityAsync(
                    User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                    "Update",
                    $"Updated school class information for: {schoolClass.ClassName}"
                );
                // -----------------------------------------

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: SchoolClass/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var schoolClass = await _schoolClassRepository.GetByIdAsync(id);
            if (schoolClass == null)
            {
                return new NotFoundViewResult("SchoolClassNotFound");
            }

            var model = _converterHelper.ToSchoolClassViewModel(schoolClass);
            return View(model);
        }

        // POST: SchoolClass/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var schoolClass = await _schoolClassRepository.GetByIdAsync(id);
            if (schoolClass == null)
            {
                return new NotFoundViewResult("SchoolClassNotFound");
            }

            // ĐÃ THÊM: Lưu tên lớp học trước khi xóa để ghi log
            string className = schoolClass.ClassName;

            try
            {
                await _schoolClassRepository.DeleteAsync(schoolClass);
                _logger.LogInformation("Turma excluída com sucesso: {ClassId}", id);

                // --- ĐÃ THÊM GHI LOG: XÓA LỚP HỌC ---
                await _activityLogHelper.LogActivityAsync(
                    User.Identity?.Name ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Unknown",
                    "Delete",
                    $"Deleted school class: {className}"
                );
                // ------------------------------------

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Erro ao excluir turma: {ClassId}", id);
                ModelState.AddModelError("", "Ocorreu um erro ao excluir a turma. Tente novamente.");
                return View(schoolClass);
            }
        }

        public IActionResult SchoolClassNotFound()
        {
            return View();
        }
    }
}
