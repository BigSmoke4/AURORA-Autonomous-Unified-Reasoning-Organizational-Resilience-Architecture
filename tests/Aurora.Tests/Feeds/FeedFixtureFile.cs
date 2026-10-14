namespace Aurora.Tests.Feeds;

/// <summary>Reads a recorded feed fixture from the repository, so the tests use the same file the fixture adapter serves.</summary>
internal static class FeedFixtureFile
{
    private static readonly string[] FixtureFolder = ["src", "Web", "Aurora.Web", "Data", "Feeds", "Fixtures"];

    public static string Read(string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. FixtureFolder, fileName]);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }

        throw new FileNotFoundException($"The feed fixture '{fileName}' was not found above {AppContext.BaseDirectory}.");
    }
}
