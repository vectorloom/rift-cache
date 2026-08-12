using Microsoft.Extensions.Configuration;
using RiftCache.Providers.Secrets;

namespace RiftCache.Tests.Providers;

public class EnvironmentSecretProviderTests : IDisposable
{
    private readonly List<string> _envVarsToClear = [];

    [Fact]
    public async Task GetSecretAsync_EnvVarSet_ReturnsValue()
    {
        var key = SetEnvVar("dummy-value");
        var provider = new EnvironmentSecretProvider(BuildEnvironmentConfiguration());

        Assert.Equal("dummy-value", await provider.GetSecretAsync(key));
    }

    [Fact]
    public async Task GetSecretAsync_Missing_ReturnsNull()
    {
        var provider = new EnvironmentSecretProvider(BuildEnvironmentConfiguration());

        Assert.Null(await provider.GetSecretAsync($"RIFTCACHE_TEST_MISSING_{Guid.NewGuid():N}"));
    }

    [Fact]
    public async Task GetSecretAsync_FallsBackToMountedFile_WhenEnvVarAbsent()
    {
        var key = $"RIFTCACHE_TEST_{Guid.NewGuid():N}";
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, "from-file\n");

        try
        {
            Environment.SetEnvironmentVariable($"{key}_FILE", filePath);
            _envVarsToClear.Add($"{key}_FILE");
            var provider = new EnvironmentSecretProvider(BuildEnvironmentConfiguration());

            Assert.Equal("from-file", await provider.GetSecretAsync(key));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetSecretAsync_EnvVarTakesPrecedenceOverMountedFile()
    {
        var key = SetEnvVar("from-env");
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, "from-file");

        try
        {
            Environment.SetEnvironmentVariable($"{key}_FILE", filePath);
            _envVarsToClear.Add($"{key}_FILE");
            var provider = new EnvironmentSecretProvider(BuildEnvironmentConfiguration());

            Assert.Equal("from-env", await provider.GetSecretAsync(key));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task GetSecretAsync_ReadsFromNonEnvironmentConfigurationSource_LikeUserSecrets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RIFTCACHE_API_KEY"] = "from-user-secrets" })
            .Build();
        var provider = new EnvironmentSecretProvider(configuration);

        Assert.Equal("from-user-secrets", await provider.GetSecretAsync("RIFTCACHE_API_KEY"));
    }

    [Fact]
    public async Task GetSecretAsync_DoubleUnderscoreKey_ResolvesNestedConfigurationSection()
    {
        // Mirrors how the environment-variables configuration provider maps "__" to ":" for
        // section nesting, so a multi-tenant secret name (RIFTCACHE_API_KEY__{TENANT}) resolves
        // the same way whether it arrives as a real env var or a nested config entry.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RIFTCACHE_API_KEY:TEAM-A"] = "team-a-key" })
            .Build();
        var provider = new EnvironmentSecretProvider(configuration);

        Assert.Equal("team-a-key", await provider.GetSecretAsync("RIFTCACHE_API_KEY__TEAM-A"));
    }

    private string SetEnvVar(string value)
    {
        var key = $"RIFTCACHE_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(key, value);
        _envVarsToClear.Add(key);
        return key;
    }

    private static IConfiguration BuildEnvironmentConfiguration() =>
        new ConfigurationBuilder().AddEnvironmentVariables().Build();

    public void Dispose()
    {
        foreach (var key in _envVarsToClear)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}
