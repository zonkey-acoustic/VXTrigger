using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace VxTrigger.Services;

public class OpenConnectListenerService : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    private int _clientCount;

    public event EventHandler? ShotDetected;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<bool>? ClientConnectionChanged;
    public event EventHandler<string>? Error;

    public bool IsRunning { get; private set; }
    public bool IsClientConnected => _clientCount > 0;
    public int Port { get; set; } = 921;

    public void Start()
    {
        if (IsRunning || _disposed)
            return;

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            IsRunning = true;
            StatusChanged?.Invoke(this, $"Listening on port {Port} (OpenConnect)");

            _ = AcceptClientsAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, $"Failed to start listener on port {Port}: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        IsRunning = false;
        StatusChanged?.Invoke(this, "Stopped");
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                StatusChanged?.Invoke(this, $"ProTee Labs connected from {client.Client.RemoteEndPoint}");
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Error?.Invoke(this, $"Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        Interlocked.Increment(ref _clientCount);
        ClientConnectionChanged?.Invoke(this, true);

        using var _ = client;
        var stream = client.GetStream();
        var buffer = new byte[8192];
        var messageBuffer = new StringBuilder();

        try
        {
            while (!ct.IsCancellationRequested && client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, ct);
                if (bytesRead == 0)
                    break;

                messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                // OpenConnect messages are newline-terminated
                var content = messageBuffer.ToString();
                int newlineIndex;
                while ((newlineIndex = content.IndexOf('\n')) >= 0)
                {
                    var message = content[..newlineIndex].Trim();
                    content = content[(newlineIndex + 1)..];

                    if (!string.IsNullOrEmpty(message))
                        ProcessMessage(message, stream);
                }
                messageBuffer.Clear();
                messageBuffer.Append(content);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                Error?.Invoke(this, $"Client error: {ex.Message}");
        }

        Interlocked.Decrement(ref _clientCount);
        ClientConnectionChanged?.Invoke(this, false);
        StatusChanged?.Invoke(this, $"ProTee Labs disconnected. Listening on port {Port}");
    }

    private void ProcessMessage(string json, NetworkStream stream)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Send ACK for every valid message
            SendAck(stream);

            // Check if this message contains ball data (= a shot)
            if (root.TryGetProperty("ShotDataOptions", out var options) &&
                options.TryGetProperty("ContainsBallData", out var containsBall) &&
                containsBall.GetBoolean())
            {
                System.Diagnostics.Debug.WriteLine("OpenConnectListener: Shot detected (ContainsBallData=true)");
                ShotDetected?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenConnectListener: JSON parse error: {ex.Message}");
            SendError(stream);
        }
    }

    private static void SendAck(NetworkStream stream)
    {
        try
        {
            var response = JsonSerializer.Serialize(new { Code = 200, Message = "Shot received successfully" });
            var data = Encoding.UTF8.GetBytes(response + "\n");
            stream.Write(data, 0, data.Length);
        }
        catch { }
    }

    private static void SendError(NetworkStream stream)
    {
        try
        {
            var response = JsonSerializer.Serialize(new { Code = 501, Message = "Bad format" });
            var data = Encoding.UTF8.GetBytes(response + "\n");
            stream.Write(data, 0, data.Length);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _cts?.Dispose();
        _disposed = true;
    }
}
