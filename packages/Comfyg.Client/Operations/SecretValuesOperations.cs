﻿using System.Net.Http.Json;
using Comfyg.Contracts.Requests;
using Comfyg.Contracts.Responses;
using Comfyg.Contracts.Secrets;

namespace Comfyg.Client.Operations;

internal class SecretValuesOperations : IComfygValuesOperations<ISecretValue>
{
    private readonly ComfygClient _client;

    public SecretValuesOperations(ComfygClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<GetValuesResponse<ISecretValue>> GetValuesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client
            .SendRequestAsync(() => new HttpRequestMessage(HttpMethod.Get, "secrets"),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Invalid status code when trying to get secret values.", null,
                response.StatusCode);

        return (await response.Content
            .ReadFromJsonAsync<GetSecretValuesResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false))!;
    }

    public async Task<GetValuesResponse<ISecretValue>> GetValuesFromDiffAsync(DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var response = await _client
            .SendRequestAsync(
                () => new HttpRequestMessage(HttpMethod.Get, $"secrets/fromDiff?since={since.ToUniversalTime():s}Z"),
                cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Invalid status code when trying to get secret values from diff.",
                null, response.StatusCode);

        return (await response.Content
            .ReadFromJsonAsync<GetSecretValuesResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false))!;
    }

    public async Task AddValuesAsync(AddValuesRequest<ISecretValue> request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var response = await _client.SendRequestAsync(() => new HttpRequestMessage(HttpMethod.Post, "secrets")
        {
            Content = JsonContent.Create((AddSecretValuesRequest)request)
        }, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Invalid status code when trying to add secret values.", null,
                response.StatusCode);
    }

    public async Task<GetDiffResponse> GetDiffAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var response = await _client
            .SendRequestAsync(
                () => new HttpRequestMessage(HttpMethod.Get, $"diff/secrets?since={since.ToUniversalTime():s}Z"),
                cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Invalid status code when trying to get secrets diff.", null,
                response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<GetDiffResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false))!;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}