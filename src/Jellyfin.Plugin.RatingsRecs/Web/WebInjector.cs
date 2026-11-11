using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RatingsRecs.Hooks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RatingsRecs.Web;

/// <summary>
/// Registers <see cref="IndexTransformation"/> with the File Transformation plugin through reflection
/// (as its README describes), so jellyfin-web loads the plugin script without touching files on disk.
/// If File Transformation is missing or incompatible the hook stays off and the settings page explains
/// how to add the script by hand.
/// </summary>
public sealed class WebInjector : BackgroundService
{
    /// <summary>
    /// Fixed id of our transformation.
    /// </summary>
    public static readonly Guid TransformationId = new("0a8f7c3e-5b1d-4f2a-9c6e-2d4b8e1f7a90");

    private const string FileTransformationAssembly = "Jellyfin.Plugin.FileTransformation";
    private const string PluginInterfaceType = "Jellyfin.Plugin.FileTransformation.PluginInterface";
    private const int MaxAttempts = 12;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly HookStatusRegistry _status;
    private readonly ILogger<WebInjector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebInjector"/> class.
    /// </summary>
    /// <param name="status">The hook status registry.</param>
    /// <param name="logger">The logger.</param>
    public WebInjector(HookStatusRegistry status, ILogger<WebInjector> logger)
    {
        _status = status;
        _logger = logger;
    }

    /// <summary>
    /// Builds the registration payload (JSON) documented by File Transformation.
    /// </summary>
    /// <returns>The JSON payload.</returns>
    public static string BuildPayload()
    {
        return JsonSerializer.Serialize(new
        {
            id = TransformationId,
            // Exactly "index.html": File Transformation groups transformations by pattern text and prefers the exact
            // key, so other plugins' "index.html" pipeline would otherwise hide a differently written pattern.
            fileNamePattern = "index.html",
            callbackAssembly = typeof(IndexTransformation).Assembly.FullName,
            callbackClass = typeof(IndexTransformation).FullName,
            callbackMethod = nameof(IndexTransformation.Transform),
        });
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _status.Set(HookStatusRegistry.WebInjection, HookState.Pending, "Waiting for File Transformation.");

        // Other plugins may finish loading after us: retry for about a minute.
        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            var result = TryRegister();
            if (result is null)
            {
                _status.Set(HookStatusRegistry.WebInjection, HookState.Active, "Script injected through File Transformation.");
                return;
            }

            if (attempt == MaxAttempts)
            {
                _status.Set(HookStatusRegistry.WebInjection, result.Value.State, result.Value.Message);
                return;
            }

            try
            {
                await Task.Delay(RetryDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private (HookState State, string Message)? TryRegister()
    {
        try
        {
            var assembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(a => string.Equals(a.GetName().Name, FileTransformationAssembly, StringComparison.Ordinal));
            if (assembly is null)
            {
                return (HookState.Unavailable, "File Transformation is not installed: install it from the catalog, or add the script manually (see below).");
            }

            var method = assembly.GetType(PluginInterfaceType)?.GetMethod("RegisterTransformation", BindingFlags.Public | BindingFlags.Static);
            var parameterType = method?.GetParameters() is { Length: 1 } parameters ? parameters[0].ParameterType : null;
            var parse = parameterType?.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, [typeof(string)], null);
            if (method is null || parameterType is null || parse is null)
            {
                return (HookState.Failed, "This version of File Transformation is not compatible (RegisterTransformation not found). Add the script manually.");
            }

            var payload = parse.Invoke(null, [BuildPayload()]);
            method.Invoke(null, [payload]);
            _logger.LogInformation("Registered the web client transformation with File Transformation {Version}", assembly.GetName().Version);
            return null;
        }
        catch (Exception ex) when (ex is TargetInvocationException or MemberAccessException or ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Registering with File Transformation failed");
            return (HookState.Failed, "File Transformation rejected the registration: " + (ex.InnerException ?? ex).Message);
        }
    }
}
