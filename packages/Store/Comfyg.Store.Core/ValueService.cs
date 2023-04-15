﻿using System.Runtime.CompilerServices;
using Azure.Data.Tables;
using Azure.Data.Tables.Poco;
using Comfyg.Store.Contracts;
using Comfyg.Store.Core.Abstractions;
using Comfyg.Store.Core.Abstractions.Changes;
using Comfyg.Store.Core.Abstractions.Permissions;

namespace Comfyg.Store.Core;

internal class ValueService<TValue, TEntity> : IValueService<TValue>
    where TValue : IComfygValue
    where TEntity : class, TValue, IComfygValueInitializer, new()
{
    private readonly TypedTableClient<TEntity> _values;
    private readonly IPermissionService _permissionService;
    private readonly IChangeService _changeService;

    public ValueService(string systemId, TableServiceClient tableServiceClient, IPermissionService permissionService,
        IChangeService changeService)
    {
        if (systemId == null) throw new ArgumentNullException(nameof(systemId));
        if (tableServiceClient == null) throw new ArgumentNullException(nameof(tableServiceClient));

        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _changeService = changeService ?? throw new ArgumentNullException(nameof(changeService));

        _values = tableServiceClient.GetTableClient<TEntity>().OverrideTableName($"{systemId}{typeof(TEntity).Name}");
    }

    public async Task AddValueAsync(string owner, string key, string value, string hash,
        CancellationToken cancellationToken = default)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (hash == null) throw new ArgumentNullException(nameof(hash));

        await _values.CreateTableIfNotExistsAsync(cancellationToken);

        var latest = await _values.GetIfExistsAsync(key.ToLower(), CoreConstants.LatestVersion,
            cancellationToken: cancellationToken);

        if (latest?.Hash == hash) return;

        await _values.AddAsync(
            new TEntity
            {
                Key = key,
                Value = value,
                Version = (long.MaxValue - DateTimeOffset.UtcNow.Ticks).ToString(),
                Hash = hash
            },
            cancellationToken: cancellationToken);
        await _values.UpsertAsync(
            new TEntity { Key = key, Value = value, Version = CoreConstants.LatestVersion, Hash = hash },
            cancellationToken: cancellationToken);

        await _changeService.LogChangeAsync<TValue>(key, ChangeType.Add, owner, cancellationToken);

        if (latest == null)
        {
            await _permissionService.SetPermissionAsync<TValue>(owner, key,
                Contracts.Permissions.Read | Contracts.Permissions.Write | Contracts.Permissions.Delete |
                Contracts.Permissions.Permit,
                cancellationToken);
        }
    }

    public async IAsyncEnumerable<TValue> GetValuesAsync(string owner,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));

        await _values.CreateTableIfNotExistsAsync(cancellationToken);

        var permissions =
            _permissionService.GetPermissionsAsync<TValue>(owner, Contracts.Permissions.Read, cancellationToken);
        await foreach (var permission in permissions.WithCancellation(cancellationToken))
        {
            var latest = await _values
                .GetIfExistsAsync(permission.TargetId, CoreConstants.LatestVersion,
                    cancellationToken: cancellationToken);

            if (latest == null) continue;

            yield return latest;
        }
    }

    public async Task<TValue?> GetValueAsync(string key, string version, CancellationToken cancellationToken = default)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (version == null) throw new ArgumentNullException(nameof(version));

        await _values.CreateTableIfNotExistsAsync(cancellationToken);

        return await _values.GetIfExistsAsync(key.ToLower(), version, cancellationToken: cancellationToken);
    }

    public async Task<TValue?> GetLatestValueAsync(string key, CancellationToken cancellationToken = default)
        => await GetValueAsync(key, CoreConstants.LatestVersion, cancellationToken);

    public async Task TagValueAsync(string owner, string key, string version, string tag,
        CancellationToken cancellationToken = default)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (version == null) throw new ArgumentNullException(nameof(version));
        if (tag == null) throw new ArgumentNullException(nameof(tag));

        await _values.CreateTableIfNotExistsAsync(cancellationToken);

        var original = await _values.GetIfExistsAsync(key.ToLower(), version, cancellationToken: cancellationToken);
        if (original == null) throw new InvalidOperationException("Value does not exist.");

        await _values.AddAsync(new TEntity
        {
            Key = key,
            Value = original.Value,
            Version = version,
            Hash = original.Hash,
            Tag = tag
        }, cancellationToken);

        await _changeService.LogChangeAsync<TValue>(key, ChangeType.Tag, owner, cancellationToken);
    }
}
