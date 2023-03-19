﻿using Comfyg.Contracts.Configuration;
using CoreHelpers.WindowsAzure.Storage.Table.Attributes;

namespace Comfyg.Core.Configuration;

[Storable(nameof(ConfigurationValueEntity))]
internal class ConfigurationValueEntity : IConfigurationValue, ISerializableComfygValue
{
    [PartitionKey] public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    [RowKey] public string Version { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}