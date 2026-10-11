using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace SchoolManagementSystem.Controllers
{
    // Chỉ cho phép Admin truy cập trang này
    [Authorize(Roles = "Admin")]
    public class ActivityLogController : Controller
    {
        private readonly SchoolDbContext _context;

        public ActivityLogController(SchoolDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy toàn bộ log từ Database, sắp xếp thời gian từ mới nhất -> cũ nhất
            var logs = await _context.ActivityLogs
                                     .OrderByDescending(l => l.Timestamp)
                                     .ToListAsync();

            return View(logs);
        }
    }
}