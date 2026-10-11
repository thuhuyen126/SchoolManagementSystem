using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Repositories;
using System;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Controllers
{
    public class AlertController : Controller
    {
        private readonly IAlertRepository _alertRepository;
        private readonly IUserHelper _userHelper;

        public AlertController(IAlertRepository alertRepository, IUserHelper userHelper)
        {
            _alertRepository = alertRepository;
            _userHelper = userHelper;
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var alerts = await _alertRepository.GetActiveAlertsAsync();
            return View(alerts);
        }

        // --- BƯỚC 4: HIỂN THỊ LOG RA TRANG UNRESOLVED ALERTS ---
        [HttpGet]
        public async Task<IActionResult> UnresolvedAlerts()
        {
            // Dùng Repository của bạn rất chuẩn.
            // Lưu ý nhỏ: Hãy đảm bảo hàm GetActiveAlertsAsync() bên trong AlertRepository
            // đang có điều kiện lọc .Where(a => a.IsResolved == false) nhé!
            var alerts = await _alertRepository.GetActiveAlertsAsync();

            return View(alerts);
        }

        // Hàm xử lý khi Admin bấm nút "Mark as Resolved" (Đánh dấu đã giải quyết)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsResolved(int id)
        {
            await _alertRepository.MarkAsResolvedAsync(id);

            // Xử lý xong thì tự động load lại trang Unresolved Alerts
            return RedirectToAction(nameof(UnresolvedAlerts));
        }
        // --------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AlertViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrWhiteSpace(userName)) return Forbid();

                var employee = await _userHelper.GetEmployeeByUserAsync(userName);
                if (employee == null)
                {
                    // Đề phòng trường hợp user đăng nhập không phải là Employee
                    ModelState.AddModelError(string.Empty, "Only employees can create alerts.");
                    return View(model);
                }

                var alert = new Alert
                {
                    Message = model.Message,
                    CreatedAt = DateTime.UtcNow,
                    IsResolved = false,
                    EmployeeId = employee.Id
                };

                await _alertRepository.CreateAlertAsync(alert);
                return RedirectToAction(nameof(ViewAlerts));
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ViewAlerts()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName)) return RedirectToAction("Index", "Home");

            var employee = await _userHelper.GetEmployeeByUserAsync(userName);
            if (employee == null) return RedirectToAction("Index", "Home");

            var alerts = await _alertRepository.GetAlertsByEmployeeIdAsync(employee.Id);
            return View(alerts);
        }
    }
}
