using System.Text.Json;

namespace PapayApp.UI.Services;

public sealed record RecentWork(string Title, string Directory, DateTime CreatedAt)
{
    public string Description => $"{CreatedAt:dd/MM/yyyy HH:mm} · {Directory}";
}

public static class RecentWorkStore
{
    private static string StorePath => Path.Combine(FileSystem.AppDataDirectory, "recent-work.json");
    public static event EventHandler? Changed;
    public static IReadOnlyList<RecentWork> Load()
    {
        try { return File.Exists(StorePath) ? JsonSerializer.Deserialize<List<RecentWork>>(File.ReadAllText(StorePath)) ?? [] : []; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return []; }
    }
    public static void Add(string title, string directory)
    {
        var items = Load().Prepend(new RecentWork(title, directory, DateTime.Now)).Take(12).ToList();
        try { File.WriteAllText(StorePath, JsonSerializer.Serialize(items)); Changed?.Invoke(null, EventArgs.Empty); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* History must not turn a successful generation into a failure. */ }
    }
}
