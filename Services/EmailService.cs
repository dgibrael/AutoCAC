using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace AutoCAC.Services;

public class EmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public async Task SendEmailAsync(string subject, string body, params string[] to)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.Username),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        foreach (var address in to)
        {
            message.To.Add(address);
        }

        using var client = new SmtpClient(_settings.SmtpServer, _settings.Port)
        {
            Credentials = new NetworkCredential(_settings.Username, _settings.Password),
            EnableSsl = _settings.UseSsl
        };

        await client.SendMailAsync(message);
    }
}

public class EmailSettings
{
    public string SmtpServer { get; set; } = "";
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}