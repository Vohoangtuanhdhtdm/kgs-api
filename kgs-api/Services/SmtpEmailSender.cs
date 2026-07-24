using kgs_api.Interfaces;
using kgs_api.Utility;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MimeKit;

namespace kgs_api.Services
{
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<SmtpSettings> settings, ILogger<SmtpEmailSender> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public Task SendEmailConfirmationAsync(string toEmail, string userName, string confirmationLink, CancellationToken ct = default)
        {
            var html = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                      <h2>Xác thực địa chỉ email</h2>
                      <p>Xin chào {System.Net.WebUtility.HtmlEncode(userName)},</p>
                      <p>Vui lòng bấm vào nút bên dưới để xác thực địa chỉ email của bạn:</p>
                      <p><a href='{confirmationLink}' style='background:#1e3a8a;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;'>Xác thực email</a></p>
                      <p>Nếu nút không hoạt động, dán link sau vào trình duyệt:<br/>{confirmationLink}</p>
                    </div>";
            return SendCoreAsync(toEmail, "Xác thực địa chỉ email của bạn", html, ct);
        }

        public Task SendPasswordResetAsync(string toEmail, string userName, string resetLink, CancellationToken ct = default)
        {
            var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                  <h2>Đặt lại mật khẩu</h2>
                  <p>Xin chào {System.Net.WebUtility.HtmlEncode(userName)},</p>
                  <p>Bấm vào nút bên dưới để đặt lại mật khẩu (link có hiệu lực trong thời gian giới hạn):</p>
                  <p><a href='{resetLink}' style='background:#1e3a8a;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;'>Đặt lại mật khẩu</a></p>
                  <p>Nếu bạn không yêu cầu việc này, hãy bỏ qua email này.</p>
                </div>";
            return SendCoreAsync(toEmail, "Đặt lại mật khẩu", html, ct);
        }

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
            => SendCoreAsync(toEmail, subject, htmlBody, ct);

        // ---- Lõi dùng chung cho cả 3 method public ----
        private async Task SendCoreAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

            using var client = new SmtpClient();
            try
            {
                var socketOptions = _settings.Port == 465
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

                await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions, ct);
                await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
                await client.SendAsync(message, ct);
                await client.DisconnectAsync(true, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gửi email SMTP thất bại tới {Email} qua {Host}:{Port}",
                    toEmail, _settings.Host, _settings.Port);
              
            }
        }
    }
}
