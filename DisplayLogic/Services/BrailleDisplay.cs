using Serilog;

namespace DisplayLogic.Services;
public class BrailleDisplay : IBrailleDisplay
{
    private readonly IConnectionStrategy _connectionStrategy;
    public BrailleDisplay(IConnectionStrategy connectionStrategy)
    {
        _connectionStrategy = connectionStrategy;
    }
    public bool IsConnected => _connectionStrategy.IsConnected;

    public event EventHandler? TextReceived;

    public async Task ConnectAsync()
    {
        try
        {
            bool connected = await _connectionStrategy.ConnectAsync();
            if (connected)
                Log.Information("BrailleDisplay connected successfully.");
            else
                Log.Warning("BrailleDisplay failed to connect.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during BrailleDisplay.ConnectAsync.");
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            bool disconnected = await _connectionStrategy.DisconnectAsync();
            if (disconnected)
                Log.Information("BrailleDisplay disconnected successfully.");
            else
                Log.Warning("BrailleDisplay failed to disconnect.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occured during BrailleDisplay.DisconectAync");
        }
    }

    public async Task<string> ReceiveTextAsync()
    {
        if (!_connectionStrategy.IsConnected)
        {
            Log.Warning("Attempted to receive text while BrailleDisplay is not connected.");
            return string.Empty;
        }
        try
        {
            string text = await _connectionStrategy.ReceiveTextAsync();
            Log.Information($"BrailleDisplay received text: {text}");
            TextReceived?.Invoke(this, EventArgs.Empty);
            return text;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred during BrailleDisplay.ReceiveTextAsync.");
            throw;
        }
    }

    public async Task SendTextAsync(string text)
    {
        if (!_connectionStrategy.IsConnected)
        {
            Log.Warning("Attempted to send text while BrailleDisplay is not connected.");
            return;
        }
        try
        {
            await _connectionStrategy.SendTextAsync(text);
            Log.Information($"BrailleDisplay sent text: {text}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occured during BrailleDisplay.SendTextAsync.");
        }
    }
}