using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace AutoCAC.Services;

public class EmailService
{
    private readonly EmailSettings _settings;
    private readonly IDbContextFactory<mainContext> _contextFactory;

    public EmailService(
        IOptions<EmailSettings> options,
        IDbContextFactory<mainContext> contextFactory)
    {
        _settings = options.Value;
        _contextFactory = contextFactory;
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

    public async Task<string[]> GetEmailsByGroups(params string[] groupNames)
    {
        if (groupNames == null || groupNames.Length == 0)
            return Array.Empty<string>();

        var normalizedGroups = groupNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedGroups.Length == 0)
            return Array.Empty<string>();

        await using var db = await _contextFactory.CreateDbContextAsync();

        var emails = await db.AuthUserGroups
            .Where(x => normalizedGroups.Contains(x.Group.Name))
            .Select(u => u.User.Email)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct()
            .ToArrayAsync();

        return emails;
    }

    public async Task SendEmailByGroupsAsync(string subject, string body, params string[] groupNames)
    {
        var emails = await GetEmailsByGroups(groupNames);

        if (emails.Length == 0)
            return;

        await SendEmailAsync(subject, body, emails);
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