using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayLogic.Services;
public class BrailleDisplay : IBrailleDisplay
{
    private readonly IConnectionStrategy _connectionStrategy;
    private readonly ILogger<BrailleDisplay> _logger;
    public BrailleDisplay(IConnectionStrategy connectionStrategy, ILogger<BrailleDisplay>? logger = null)
    {
        _connectionStrategy = connectionStrategy;
        _logger = logger ?? NullLogger<BrailleDisplay>.Instance;
    }
    public bool IsConnected => _connectionStrategy.IsConnected;

    public event EventHandler? TextReceived;

    public async Task ConnectAsync()
    {
        try
        {
            bool connected = await _connectionStrategy.ConnectAsync();
            if (connected)
                _logger.LogInformation("BrailleDisplay connected successfully.");
            else
                _logger.LogWarning("BrailleDisplay failed to connect.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during BrailleDisplay.ConnectAsync.");
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            bool disconnected = await _connectionStrategy.DisconnectAsync();
            if (disconnected)
                _logger.LogInformation("BrailleDisplay disconnected successfully.");
            else
                _logger.LogWarning("BrailleDisplay failed to disconnect.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occured during BrailleDisplay.DisconectAync");
        }
    }

    public async Task<string> ReceiveTextAsync()
    {
        if (!_connectionStrategy.IsConnected)
        {
            _logger.LogWarning("Attempted to receive text while BrailleDisplay is not connected.");
            return string.Empty;
        }
        try
        {
            string text = await _connectionStrategy.ReceiveTextAsync();
            _logger.LogInformation($"BrailleDisplay received text: {text}");
            TextReceived?.Invoke(this, EventArgs.Empty);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during BrailleDisplay.ReceiveTextAsync.");
            throw;
        }
    }

    public async Task SendTextAsync(string text)
    {
        if (!_connectionStrategy.IsConnected)
        {
            _logger.LogWarning("Attempted to send text while BrailleDisplay is not connected.");
            return;
        }
        try
        {
            await _connectionStrategy.SendTextAsync(text);
            _logger.LogInformation($"BrailleDisplay sent text: {text}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occured during BrailleDisplay.SendTextAsync.");
        }
    }
}