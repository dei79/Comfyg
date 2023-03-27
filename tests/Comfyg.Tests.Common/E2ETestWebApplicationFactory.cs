﻿using Comfyg.Store.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Comfyg.Tests.Common;

public sealed class E2ETestWebApplicationFactory<TEntryPoint> : IDisposable where TEntryPoint : class
{
    private readonly IList<HttpClient> _clients = new List<HttpClient>();
    private readonly Mocks _mocks = new();

    private WebApplication? _webApplication;

    public void ResetMocks() => _mocks.ResetMocks();

    public void Mock<T>(Action<Mock<T>> mockProvider) where T : class => _mocks.Mock(mockProvider);

    private Mock<T> GetMock<T>() where T : class => _mocks.GetMock<T>();

    private void EnsureWebApplication()
    {
        if (_webApplication != null) return;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        var mvcBuilder = builder.Services.AddControllers();
        mvcBuilder.PartManager.ApplicationParts.Add(new AssemblyPart(typeof(TEntryPoint).Assembly));
        
        builder.UseComfygStoreApi();

        builder.WebHost.ConfigureServices(_mocks.ConfigureServices);

        _webApplication = builder.Build();

        _webApplication.MapControllers();

        _webApplication.StartAsync().GetAwaiter().GetResult();
    }

    public HttpClient CreateClient()
    {
        EnsureWebApplication();

        var client = new HttpClient();
        client.BaseAddress = new Uri(_webApplication!.Urls.First());

        _clients.Add(client);

        return client;
    }

    public void Dispose()
    {
        _webApplication?.StopAsync().GetAwaiter().GetResult();
        _webApplication = null;

        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();
    }
}
