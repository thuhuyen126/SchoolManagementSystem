using SchoolManagementSystem.Data;
using SchoolManagementSystem.Data.Entities;
using System;
using System.Threading.Tasks;

namespace SchoolManagementSystem.Helpers
{
    public interface IActivityLogHelper
    {
        Task LogActivityAsync(string username, string action, string description);
    }

    public class ActivityLogHelper : IActivityLogHelper
    {
        private readonly SchoolDbContext _context;

        public ActivityLogHelper(SchoolDbContext context)
        {
            _context = context;
        }

        public async Task LogActivityAsync(string username, string action, string description)
        {
            var log = new ActivityLog
            {
                Username = username ?? "System", // Đề phòng trường hợp không có user đăng nhập
                Action = action,
                Description = description,
                Timestamp = DateTime.Now
            };

            _context.ActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}