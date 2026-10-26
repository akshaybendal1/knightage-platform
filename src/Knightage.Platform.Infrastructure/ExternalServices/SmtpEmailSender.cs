using Knightage.Platform.Core.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Knightage.Platform.Infrastructure.ExternalServices;

// Mirrors knightage-crm's SmtpEmailSender exactly (minus the attachment variant,
// unused here) -- same Smtp:* config shape, so the same mail server/credentials
// can be reused across services.
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var smtp = _configuration.GetSection("Smtp");
        var host = smtp["Host"] ?? throw new InvalidOperationException("Smtp:Host is not configured.");
        var port = int.TryParse(smtp["Port"], out var configuredPort) ? configuredPort : 587;
        var username = smtp["Username"];
        var password = smtp["Password"];
        var fromAddress = smtp["FromAddress"] ?? throw new InvalidOperationException("Smtp:FromAddress is not configured.");
        var fromName = smtp["FromName"] ?? "Knightage Platform";
        var useStartTls = !bool.TryParse(smtp["UseStartTls"], out var configuredStartTls) || configuredStartTls;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, useStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken);
        if (!string.IsNullOrEmpty(username))
        {
            await client.AuthenticateAsync(username, password, cancellationToken);
        }
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
