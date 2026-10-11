using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Repositories;

namespace SchoolManagementSystem.Controllers
{
    [Authorize] // Este atributo garante que o utilizador deve estar autenticado
    public class DashboardController : Controller
    {
        private readonly IAlertRepository _alertRepository;

        public DashboardController(IAlertRepository alertRepository)
        {
            _alertRepository = alertRepository;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        [Authorize(Roles = "Admin")] 
        public async Task<IActionResult> AdminDashboard()
        {
            return View();
        }

        [Authorize(Roles = "Employee")] 
        public IActionResult EmployeeDashboard()
        {
            return View();
        }

        [Authorize(Roles = "Teacher")] 
        public IActionResult TeacherDashboard()
        {
            return View();
        }

        [Authorize(Roles = "Student")] 
        public IActionResult StudentDashboard()
        {
            return View();
        }
    }
}
