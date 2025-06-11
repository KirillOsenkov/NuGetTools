using System;
using System.Threading.Tasks;
using NuGet.Common;

public class ConsoleLogger : LoggerBase
{
    public static ConsoleLogger Instance { get; } = new ConsoleLogger();

    public override void Log(ILogMessage message)
    {
        Console.WriteLine(message.ToString());
    }

    public override async Task LogAsync(ILogMessage message)
    {
        Log(message);
    }
}