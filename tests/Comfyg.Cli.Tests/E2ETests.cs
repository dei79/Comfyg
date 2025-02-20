﻿using System.Text.Json;
using Comfyg.Store.Authentication.Abstractions;
using Comfyg.Store.Contracts;
using Comfyg.Store.Core.Abstractions;
using Comfyg.Store.Core.Abstractions.Permissions;
using Comfyg.Tests.Common;
using Comfyg.Tests.Common.Contracts;
using Moq;
using Xunit.Abstractions;

namespace Comfyg.Cli.Tests;

public class E2ETests : IClassFixture<E2ETestWebApplicationFactory<Program>>
{
    private readonly E2ETestWebApplicationFactory<Program> _factory;

    public E2ETests(E2ETestWebApplicationFactory<Program> factory, ITestOutputHelper testOutputHelper)
    {
        _factory = factory;
        _factory.TestOutputHelper = testOutputHelper;

        _factory.ResetMocks();
    }

    [Fact]
    public async Task Test_Import()
    {
        var json = new Dictionary<string, object>
        {
            { "key1", "value1" },
            { "key2", new Dictionary<string, object> { { "", "value2" }, { "key3", "value3" } } }
        };
        var importFile = Path.GetTempPath() + Guid.NewGuid();
        await File.WriteAllTextAsync(importFile, JsonSerializer.Serialize(json));
        const string expectedOutput = "Successfully imported 3 values";

        _factory.Mock<IPermissionService>(mock =>
        {
            mock.Setup(ps =>
                    ps.IsPermittedAsync<IConfigurationValue>(It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        });

        var client = await ConnectAsync();

        var result = await TestCli.ExecuteAsync($"import config \"{importFile}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith(expectedOutput, result.Output);

        _factory.Mock<IPermissionService>(mock =>
        {
            mock.Verify(
                ps => ps.IsPermittedAsync<IConfigurationValue>(It.Is<string>(s => s == client.ClientId),
                    It.Is<string>(s => s == "key1"), It.IsAny<CancellationToken>()), Times.Once);
            mock.Verify(
                ps => ps.IsPermittedAsync<IConfigurationValue>(It.Is<string>(s => s == client.ClientId),
                    It.Is<string>(s => s == "key2"), It.IsAny<CancellationToken>()), Times.Once);
            mock.Verify(
                ps => ps.IsPermittedAsync<IConfigurationValue>(It.Is<string>(s => s == client.ClientId),
                    It.Is<string>(s => s == "key2:key3"), It.IsAny<CancellationToken>()), Times.Once);
        });

        _factory.Mock<IValueService<IConfigurationValue>>(mock =>
        {
            mock.Verify(cs => cs.AddValueAsync(It.Is<string>(s => s == client.ClientId),
                    It.Is<string>(s => s == "key1"), It.Is<string>(s => s == "value1"), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            mock.Verify(cs => cs.AddValueAsync(It.Is<string>(s => s == client.ClientId),
                    It.Is<string>(s => s == "key2"), It.Is<string>(s => s == "value2"), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            mock.Verify(cs => cs.AddValueAsync(It.Is<string>(s => s == client.ClientId),
                    It.Is<string>(s => s == "key2:key3"), It.Is<string>(s => s == "value3"), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        });
    }

    [Fact]
    public async Task Test_Export()
    {
        var values = new List<IConfigurationValue>
        {
            new TestConfigurationValue { Key = "key1", Value = "value1" },
            new TestConfigurationValue { Key = "key2", Value = "value2" },
            new TestConfigurationValue { Key = "key2:key3", Value = "value3" }
        };
        var exportFile = Path.GetTempPath() + Guid.NewGuid();
        const string expectedOutput = "Successfully exported 3 values";

        _factory.Mock<IValueService<IConfigurationValue>>(mock =>
        {
            mock.Setup(cs => cs.GetValuesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(values.ToAsyncEnumerable());
        });

        var client = await ConnectAsync();

        var result = await TestCli.ExecuteAsync($"export config \"{exportFile}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith(expectedOutput, result.Output);

        Assert.True(File.Exists(exportFile));

        var content = await File.ReadAllTextAsync(exportFile);
        Assert.NotNull(content);

        var json = JsonSerializer.Deserialize<IDictionary<string, JsonElement>>(content);
        Assert.NotNull(json);
        Assert.True(json!.ContainsKey("key1"));
        Assert.Equal("value1", json["key1"].GetString());
        Assert.True(json.ContainsKey("key2"));
        Assert.False(json.ContainsKey("key3"));
        Assert.Equal(JsonValueKind.Object, json["key2"].ValueKind);

        var subJson = json["key2"].Deserialize<IDictionary<string, JsonElement>>();
        Assert.NotNull(subJson);
        Assert.True(subJson!.ContainsKey(""));
        Assert.Equal("value2", subJson[""].GetString());
        Assert.True(subJson.ContainsKey("key3"));
        Assert.Equal("value3", subJson["key3"].GetString());

        _factory.Mock<IValueService<IConfigurationValue>>(mock =>
        {
            mock.Verify(
                cs => cs.GetValuesAsync(It.Is<string>(s => s == client.ClientId), It.IsAny<CancellationToken>()),
                Times.Once);
        });
    }

    private static string CreateClientSecret()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }

    private async Task<IClient> ConnectAsync()
    {
        var clientId = Guid.NewGuid().ToString();
        var clientSecret = CreateClientSecret();
        const string friendlyName = "Test Client";
        var client = new TestClient
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            FriendlyName = friendlyName
        };

        using var httpClient = _factory.CreateClient();
        var expectedOutput = @"Successfully connected to " + httpClient.BaseAddress;

        var connectionString = $"Endpoint={httpClient.BaseAddress};ClientId={clientId};ClientSecret={clientSecret}";

        _factory.Mock<IClientService>(mock =>
        {
            mock.Setup(cs => cs.GetClientAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(client);
            mock.Setup(cs => cs.ReceiveClientSecretAsync(It.IsAny<IClient>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(clientSecret);
        });

        var result = await TestCli.ExecuteAsync($"connect \"{connectionString}\"");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith(expectedOutput, result.Output);

        _factory.Mock<IClientService>(mock =>
        {
            mock.Verify(cs => cs.GetClientAsync(It.Is<string>(s => s == clientId), It.IsAny<CancellationToken>()),
                Times.Once);
            mock.Verify(
                cs => cs.ReceiveClientSecretAsync(It.Is<IClient>(c => c.ClientId == clientId),
                    It.IsAny<CancellationToken>()), Times.Once);
        });

        return client;
    }
}
