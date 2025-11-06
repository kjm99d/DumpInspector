using System;
using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace DumpInspector.Server.Services.Implementations
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly CrashDumpSettings _settings;

        public SmtpEmailSender(IOptions<CrashDumpSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            var smtp = _settings.Smtp;
            var host = smtp?.Host?.Trim();
            var from = smtp?.FromAddress?.Trim();
            if (smtp == null || !smtp.Enabled || string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            {
                throw new InvalidOperationException("SMTP is not configured.");
            }

            using var message = new MailMessage(from, to, subject, body)
            {
                IsBodyHtml = false
            };
            using var client = new SmtpClient(host, smtp.Port)
            {
                EnableSsl = smtp.UseSsl,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(smtp.Username))
            {
                client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
            }

            await client.SendMailAsync(message);
        }
    }
}
