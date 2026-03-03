using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Dispatching;

namespace Tactadile.Core.Ipc;

/// <summary>
/// Named pipe server that accepts commands from the Command Palette extension
/// and other external consumers. Runs a background listener loop.
/// </summary>
public sealed class CommandPipeServer : IDisposable
{
    private const string PipeName = "Tactadile_CommandPipe";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly Action<string, Dictionary<string, double>?> _executeAction;
    private readonly Func<List<ActionInfo>> _getActions;
    private readonly DispatcherQueue _dispatcherQueue;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public CommandPipeServer(
        Action<string, Dictionary<string, double>?> executeAction,
        Func<List<ActionInfo>> getActions,
        DispatcherQueue dispatcherQueue)
    {
        _executeAction = executeAction;
        _getActions = getActions;
        _dispatcherQueue = dispatcherQueue;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoop(_cts.Token));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        try { _listenTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);

                try
                {
                    await HandleClient(server, ct);
                }
                catch
                {
                    // Client disconnected or sent bad data — continue listening
                }
                finally
                {
                    if (server.IsConnected)
                        server.Disconnect();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Pipe creation failed — wait before retrying
                try { await Task.Delay(1000, ct); } catch { break; }
            }
        }
    }

    private async Task HandleClient(NamedPipeServerStream server, CancellationToken ct)
    {
        using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(server, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        var line = await reader.ReadLineAsync(ct);
        if (string.IsNullOrEmpty(line)) return;

        var request = JsonSerializer.Deserialize<PipeRequest>(line, JsonOptions);
        if (request == null) return;

        string response = request.Type?.ToLowerInvariant() switch
        {
            "ping" => JsonSerializer.Serialize(new PipeResponse { Type = "pong" }, JsonOptions),
            "query" => HandleQuery(request),
            "execute" => HandleExecute(request),
            _ => JsonSerializer.Serialize(new PipeResponse { Type = "error", Message = "Unknown request type" }, JsonOptions)
        };

        await writer.WriteLineAsync(response);
    }

    private string HandleQuery(PipeRequest request)
    {
        if (request.What == "actions")
        {
            var actions = _getActions();
            var response = new PipeActionsResponse { Type = "actions", Data = actions };
            return JsonSerializer.Serialize(response, JsonOptions);
        }

        return JsonSerializer.Serialize(
            new PipeResponse { Type = "error", Message = $"Unknown query: {request.What}" }, JsonOptions);
    }

    private string HandleExecute(PipeRequest request)
    {
        if (string.IsNullOrEmpty(request.Action))
        {
            return JsonSerializer.Serialize(
                new PipeResponse { Type = "error", Message = "Missing action" }, JsonOptions);
        }

        // Dispatch to UI thread
        _dispatcherQueue.TryEnqueue(() =>
        {
            _executeAction(request.Action, request.Parameters);
        });

        return JsonSerializer.Serialize(new PipeResponse { Type = "ok" }, JsonOptions);
    }
}

// ── IPC message types ──

public sealed class PipeRequest
{
    public string Type { get; set; } = "";
    public string? What { get; set; }
    public string? Action { get; set; }
    public Dictionary<string, double>? Parameters { get; set; }
}

public sealed class PipeResponse
{
    public string Type { get; set; } = "";
    public string? Message { get; set; }
}

public sealed class PipeActionsResponse
{
    public string Type { get; set; } = "";
    public List<ActionInfo> Data { get; set; } = new();
}

public sealed class ActionInfo
{
    public string Name { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string Hotkey { get; set; } = "";
    public string Category { get; set; } = "";
}
