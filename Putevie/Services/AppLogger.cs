using System.Diagnostics;

namespace Putevie.Services;

public static class AppLogger
{
    public static void LogInfo(string message) =>
        Debug.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} {message}");

    public static void LogError(string message, Exception exception) =>
        Debug.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} {message}: {exception}");
}
