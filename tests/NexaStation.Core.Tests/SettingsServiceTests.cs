using NexaStation.Core;
using Xunit;

namespace NexaStation.Core.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task JsonCollectionsRoundTrip()
    {
        MemorySettingsRepository repository=new();SettingsService settings=new(repository);string[] expected=["library-a","library-b"];
        await settings.SetAsync("libraries",expected);
        Assert.Equal(expected,await settings.GetAsync("libraries",Array.Empty<string>()));
    }

    private sealed class MemorySettingsRepository:ISettingsRepository
    {
        private readonly Dictionary<string,string> _values=[];
        public Task<string?> GetAsync(string key,CancellationToken cancellationToken=default)=>Task.FromResult(_values.GetValueOrDefault(key));
        public Task SetAsync(string key,string value,CancellationToken cancellationToken=default){_values[key]=value;return Task.CompletedTask;}
    }
}
