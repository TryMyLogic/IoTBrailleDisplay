using DisplayLogic.SharedInterfaces;

namespace DevConsole
{
    internal class ConsoleNotifier : IUserNotifier
    {
        public Task NotifyAsync(string title, string message)
        {
            Console.WriteLine($"[{title}] {message}");
            return Task.CompletedTask;
        }
    }
}
