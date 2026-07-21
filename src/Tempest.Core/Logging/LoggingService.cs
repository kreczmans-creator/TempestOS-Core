namespace Tempest.Core.Logging;

public class LoggingService
{
    private readonly string _logFile;

    public LoggingService(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);

        _logFile = Path.Combine(
            logDirectory,
            $"Tempest_{DateTime.Now:yyyyMMdd}.log");
    }

    public void Information(string message)
    {
        var line =
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO  {message}";

        Console.WriteLine(line);

        File.AppendAllText(
            _logFile,
            line + Environment.NewLine);
    }
}