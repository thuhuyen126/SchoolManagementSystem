using SchoolManagementSystem.Data;
using SchoolManagementSystem.Data.Entities;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Helpers
{
    public class AlertHelper : IAlertHelper
    {
        // Đã sửa thành SchoolDbContext ở dòng này
        private readonly SchoolDbContext _context;

        // Đã sửa thành SchoolDbContext ở dòng này
        public AlertHelper(SchoolDbContext context)
        {
            _context = context;
        }

        public async Task CreateAlertAsync(string message, string alertType, string userId)
        {
            var alert = new SystemAlert
            {
                Message = message,
                AlertType = alertType,
                UserId = userId
            };

            _context.SystemAlerts.Add(alert);
            await _context.SaveChangesAsync();
        }
    }
}
