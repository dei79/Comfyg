﻿using Azure.Security.KeyVault.Secrets;
using Comfyg.Authentication.Abstractions;
using Comfyg.Core.Abstractions.Secrets;
using Comfyg.Core.Secrets;
using CoreHelpers.WindowsAzure.Storage.Table;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Comfyg.Authentication;

public static class ComfygAuthenticationExtensions
{
    public static AuthenticationBuilder AddComfygAuthentication(this IServiceCollection serviceCollection,
        Action<ComfygAuthenticationOptions> optionsConfigurator)
    {
        if (serviceCollection == null) throw new ArgumentNullException(nameof(serviceCollection));
        if (optionsConfigurator == null) throw new ArgumentNullException(nameof(optionsConfigurator));

        ComfygAuthenticationOptions OptionsProvider()
        {
            var options = new ComfygAuthenticationOptions();
            optionsConfigurator(options);
            return options;
        }

        IStorageContext StorageContextProvider()
        {
            var options = OptionsProvider();
            if (options.AzureTableStorageConnectionString == null)
                throw new InvalidOperationException("Missing AzureTableStorageConnectionString option.");
            return new StorageContext(options.AzureTableStorageConnectionString);
        }

        ISecretService SecretServiceProvider(IServiceProvider provider)
        {
            var options = OptionsProvider();

            if (options.EncryptionKey != null)
            {
                return new EncryptionBasedSecretService(options.EncryptionKey);
            }

            if (!options.UseKeyVault)
                throw new InvalidOperationException(
                    "Neither encryption nor Azure Key Vault is configured. Use either ComfygAuthenticationOptions.UseEncryption or ComfygAuthenticationOptions.UseKeyVault to configure secret handling.");

            return new KeyVaultSecretService(nameof(Comfyg) + nameof(Authentication),
                provider.GetRequiredService<SecretClient>());
        }

        serviceCollection.AddSingleton<IClientService, ClientService>(provider =>
            new ClientService(StorageContextProvider(), SecretServiceProvider(provider)));

        serviceCollection.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ComfygJwtBearerOptions>();
        serviceCollection.AddSingleton<ComfygSecurityTokenHandler>();

        return serviceCollection
            .AddAuthentication(nameof(Comfyg))
            .AddJwtBearer(nameof(Comfyg), options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateIssuer = false
                };
            });
    }
}