﻿using Comfyg.Configuration;
using Microsoft.Extensions.Configuration;

namespace Comfyg;

public static class ComfygExtensions
{
    public static IConfigurationBuilder AddComfyg(this IConfigurationBuilder configurationBuilder,
        Action<ComfygOptions> optionsConfigurator)
    {
        if (configurationBuilder == null) throw new ArgumentNullException(nameof(configurationBuilder));
        if (optionsConfigurator == null) throw new ArgumentNullException(nameof(optionsConfigurator));

        configurationBuilder.Add(new ComfygConfigurationSource(optionsConfigurator));

        return configurationBuilder;
    }
}