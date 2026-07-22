using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Infrastructure.Persistence;
using NexaStation.Recovery;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class AppPathIntegrationTests
{
    [Fact]
    public async Task DatabaseAndSettingsUseTheCentralPortablePath()
    {
        string root = Path.Combine(Path.GetTempPath(), "NexaStation-Path-Integration", Guid.NewGuid().ToString("N"));
        string installation = Path.Combine(root, "App");
        string localAppData = Path.Combine(root, "LocalAppData");
        Directory.CreateDirectory(installation);
        await File.WriteAllTextAsync(Path.Combine(installation, "portable.flag"), string.Empty);

        try
        {
            NexaStationPaths paths = new(new NexaStationPathOptions(installation, localAppData));
            paths.EnsureCreated();
            SqliteConnectionFactory factory = new(paths);
            await new DatabaseInitializer(factory, new NullLogger()).InitializeAsync();
            SettingsService settings = new(new SqliteSettingsRepository(factory));

            await settings.SetAsync("Packaging.PortableTest", "stored beside the app");

            Assert.Equal(paths.Database, factory.DatabasePath);
            Assert.Equal("stored beside the app", await settings.GetAsync("Packaging.PortableTest", string.Empty));
            Assert.True(File.Exists(Path.Combine(installation, "Data", "nexastation.db")));
            Assert.False(Directory.Exists(Path.Combine(localAppData, "NexaStation")));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class NullLogger : IAppLogger
    {
        public void Information(string eventName, string message, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }

        public void LogError(string eventName, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }
    }
}
