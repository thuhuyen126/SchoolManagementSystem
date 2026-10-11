using System;
using System.ComponentModel.DataAnnotations;

namespace SchoolManagementSystem.Data.Entities
{
    public class SystemAlert
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Message { get; set; } // Nội dung cảnh báo (VD: "Phát hiện đăng nhập sai 5 lần")

        public string AlertType { get; set; } // Phân loại: "Warning", "Error", "Info"

        public DateTime CreatedAt { get; set; } = DateTime.Now; // Thời gian xảy ra

        public bool IsResolved { get; set; } = false; // Trạng thái: Đã giải quyết chưa?

        // (Tùy chọn) Lưu thêm ID của người dùng liên quan nếu cần
        public string UserId { get; set; }
    }
}