using System.Diagnostics;
using Microsoft.Extensions.Options;
using RespondaRapido.Domain;

namespace RespondaRapido.Infrastructure;

public sealed class BrowserLauncher(
    IOptions<QuizSettings> settings,
    IHostApplicationLifetime lifetime,
    ILogger<BrowserLauncher> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!settings.Value.OpenBrowserOnStart) return Task.CompletedTask;
        lifetime.ApplicationStarted.Register(OpenBrowser);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void OpenBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo(settings.Value.BrowserUrl) { UseShellExecute = true });
            logger.LogInformation("Abrindo o navegador em {BrowserUrl}", settings.Value.BrowserUrl);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Não foi possível abrir o navegador automaticamente em {BrowserUrl}", settings.Value.BrowserUrl);
        }
    }
}