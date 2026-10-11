using System;
using System.ComponentModel.DataAnnotations;

namespace SchoolManagementSystem.Data.Entities
{
    public class ActivityLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } // "Ai" làm (Thường lấy địa chỉ Email/Username đang đăng nhập)

        [Required]
        public string Action { get; set; } // Làm "Hành động" gì (VD: "Create", "Update", "Delete", "Login")

        [Required]
        public string Description { get; set; } // Chi tiết (VD: "Đã thêm học sinh Đoàn Phương Thảo")

        public DateTime Timestamp { get; set; } = DateTime.Now; // "Thời điểm" nào
    }
}