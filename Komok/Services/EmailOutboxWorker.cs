using System.Net;
using System.Net.Mail;
using Komok.Data;
using Microsoft.EntityFrameworkCore;

namespace Komok.Services;

public sealed class EmailOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        do
        {
            try
            {
                await SendPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Email outbox processing failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendPendingAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration["Smtp:Host"])
            || string.IsNullOrWhiteSpace(configuration["Smtp:From"]))
        {
            if (environment.IsDevelopment())
                await LogDevelopmentEmailsAsync(cancellationToken);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KomokDbContext>();
        var now = DateTimeOffset.UtcNow;
        var messages = await db.EmailOutbox
            .Where(message => message.SentAt == null && (message.NextAttemptAt == null || message.NextAttemptAt <= now))
            .OrderBy(message => message.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await SendAsync(message.Recipient, message.Subject, message.Body, cancellationToken);
                message.SentAt = DateTimeOffset.UtcNow;
                message.LastError = null;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                message.Attempts++;
                var retrySeconds = Math.Min(3600, 30 * Math.Pow(2, Math.Min(message.Attempts - 1, 7)));
                message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(retrySeconds);
                message.LastError = error.Message[..Math.Min(error.Message.Length, 2000)];
                logger.LogWarning(error, "Could not send queued email {EmailOutboxId}.", message.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task LogDevelopmentEmailsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KomokDbContext>();
        var messages = await db.EmailOutbox.Where(message => message.SentAt == null)
            .OrderBy(message => message.CreatedAt).Take(20).ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            logger.LogInformation("Development email to {Recipient}: {Subject}\n{Body}",
                message.Recipient, message.Subject, message.Body);
            message.SentAt = DateTimeOffset.UtcNow;
        }
        if (messages.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var host = configuration["Smtp:Host"];
        var from = configuration["Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("SMTP host and sender address must be configured.");

        var port = int.TryParse(configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = !bool.TryParse(configuration["Smtp:EnableSsl"], out var useSsl) || useSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var username = configuration["Smtp:Username"];
        var password = configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        using var mail = new MailMessage(from, recipient, subject, body);
        await client.SendMailAsync(mail, cancellationToken);
    }
}
