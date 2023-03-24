﻿using System.Text.Json.Serialization;
using Comfyg.Contracts.Authentication;

namespace Comfyg.Contracts.Responses;

public sealed class SetupClientResponse
{
    [JsonConverter(typeof(ContractConverter<IClient, Client>))]
    public IClient Client { get; }

    public string ClientSecret { get; }

    public SetupClientResponse(IClient client, string clientSecret)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        ClientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
    }
}
