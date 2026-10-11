using MailKit.Net.Smtp;
using MailKit.Security; // Thêm thư viện này để xử lý bảo mật
using MimeKit;

namespace SchoolManagementSystem.Helpers
{
    public class MailHelper : IMailHelper
    {
        private readonly IConfiguration _configuration;

        public MailHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Response SendEmail(string to, string subject, string body)
        {
            // Lấy thông tin từ appsettings.json
            var nameFrom = _configuration["Mail:NameFrom"];
            var from = _configuration["Mail:From"];
            var smtp = _configuration["Mail:Smtp"];
            var port = _configuration["Mail:Port"];
            var password = _configuration["Mail:Password"];

            if (string.IsNullOrWhiteSpace(nameFrom) ||
                string.IsNullOrWhiteSpace(from) ||
                string.IsNullOrWhiteSpace(smtp) ||
                string.IsNullOrWhiteSpace(password) ||
                !int.TryParse(port, out var smtpPort) || smtpPort is < 1 or > 65535)
            {
                return Failure();
            }

            var message = new MimeMessage();
            // Gán người gửi
            message.From.Add(new MailboxAddress(nameFrom, from));
            // Gán người nhận
            message.To.Add(new MailboxAddress(to, to));
            message.Subject = subject;

            var bodybuilder = new BodyBuilder
            {
                HtmlBody = body,
            };
            message.Body = bodybuilder.ToMessageBody();

            try
            {
                using (var client = new SmtpClient())
                {
                    // TỰ ĐỘNG CHẤP NHẬN CHỨNG CHỈ (Nếu dùng mail công ty/local mail server)
                    // client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    // Kết nối với Server Mail
                    // Sử dụng SecureSocketOptions.StartTls cho cổng 587 (phổ biến nhất)
                    client.Connect(smtp, smtpPort, SecureSocketOptions.StartTls);

                    // Đăng nhập bằng App Password (16 ký tự)
                    client.Authenticate(from, password);

                    // Thực hiện gửi
                    client.Send(message);

                    // Ngắt kết nối
                    client.Disconnect(true);
                }
            }
            catch
            {
                return Failure();
            }

            return new Response
            {
                IsSuccess = true
            };
        }

        private static Response Failure() => new Response
        {
            IsSuccess = false,
            Message = "Email delivery failed."
        };
    }
}
