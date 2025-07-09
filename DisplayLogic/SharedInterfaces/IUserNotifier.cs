namespace DisplayLogic.SharedInterfaces
{
    // For Web Socket to notify user differently depending on the platform
    public interface IUserNotifier
    {
        Task NotifyAsync(string title, string message);
    }
}
