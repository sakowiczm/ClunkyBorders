namespace ClunkyBorders;

internal class Logger
{
    private void Log(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var entry = $"[{timestamp}] [{level}] {message}";

        Console.ForegroundColor = level switch
        {
            "DEBUG" => ConsoleColor.DarkGray,
            "INFO" => ConsoleColor.White,
            "WARN" => ConsoleColor.Yellow,
            "ERROR" => ConsoleColor.Red,
            _ => Console.ForegroundColor
        };

        Console.WriteLine(entry);
        Console.ResetColor();
    }

    public void Debug(string message) => Log("DEBUG", message);
    public void Info(string message) => Log("INFO", message);
    public void Warning(string message) => Log("WARN", message);
    public void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception != null
            ? $"{message} | Exception: {exception.GetType().Name} - {exception.Message}\n{exception.StackTrace}"
            : message;

        Log("ERROR", message);
    }

}
