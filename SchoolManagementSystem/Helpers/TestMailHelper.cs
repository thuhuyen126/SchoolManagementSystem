using MailKit.Net.Smtp;
using MimeKit;

namespace SchoolManagementSystem.Helpers
{
    public class TestMailHelper : IMailHelper
    {
        public Response SendEmail(string to, string subject, string body)
        {
            var testMode = bool.TryParse(Environment.GetEnvironmentVariable("SCHOOL_TEST_MODE"), out var testModeEnabled) && testModeEnabled;
            if (!testMode)
            {
                throw new InvalidOperationException("TestMailHelper can only be used when SCHOOL_TEST_MODE=true.");
            }

            var emailMode = Environment.GetEnvironmentVariable("TEST_EMAIL_MODE")?.Trim();
            if (string.IsNullOrEmpty(emailMode) || string.Equals(emailMode, "local", StringComparison.OrdinalIgnoreCase))
            {
                return SendLocal(to, subject, body);
            }

            throw new InvalidOperationException("Test email delivery is restricted to the local mail sink.");
        }

        private Response SendLocal(string to, string subject, string body)
        {
            const string smtp = "localhost";
            const int port = 1025;
            return Send(smtp, port, to, subject, body);
        }

        private static Response Send(string smtp, int port, string to, string subject, string body)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("School Management Test", "test@localhost"));
                message.To.Add(new MailboxAddress(to, to));
                message.Subject = subject;
                message.Body = new BodyBuilder { HtmlBody = body }.ToMessageBody();

                using var client = new SmtpClient();
                client.Connect(smtp, port, MailKit.Security.SecureSocketOptions.None);
                client.Send(message);
                client.Disconnect(true);

                return new Response { IsSuccess = true };
            }
            catch
            {
                return Failure();
            }
        }

        private static Response Failure() => new Response
        {
            IsSuccess = false,
                Message = "Local test email delivery failed."
        };
    }
}
