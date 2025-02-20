﻿using System.Runtime.CompilerServices;
using Azure.Data.Tables;
using Azure.Data.Tables.Poco;
using Comfyg.Store.Core.Abstractions.Permissions;

namespace Comfyg.Store.Core.Permissions;

internal class PermissionService : IPermissionService
{
    private readonly TypedTableClient<PermissionEntity> _permissions;
    private readonly TypedTableClient<PermissionEntityMirrored> _permissionsMirrored;

    public PermissionService(string systemId, TableServiceClient tableServiceClient)
    {
        if (systemId == null) throw new ArgumentNullException(nameof(systemId));
        if (tableServiceClient == null) throw new ArgumentNullException(nameof(tableServiceClient));

        _permissions = tableServiceClient.GetTableClient<PermissionEntity>()
            .OverrideTableName($"{systemId}{nameof(PermissionEntity)}");
        _permissionsMirrored = tableServiceClient.GetTableClient<PermissionEntityMirrored>()
            .OverrideTableName($"{systemId}{nameof(PermissionEntityMirrored)}");
    }

    public async Task<bool> IsPermittedAsync<T>(string owner, string targetId,
        CancellationToken cancellationToken = default)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (targetId == null) throw new ArgumentNullException(nameof(targetId));

        await _permissionsMirrored.CreateTableIfNotExistsAsync(cancellationToken);

        var filter = $"PartitionKey eq '{typeof(T).FullName}-{targetId.ToLower()}'";
        var ownedValues = await _permissionsMirrored.QueryAsync(filter, cancellationToken: cancellationToken)
            .ToArrayAsync(cancellationToken);

        // if no target exists we assume permission (to create it)
        return !ownedValues.Any() || ownedValues.Any(ov => ov.Owner == owner);
    }

    public async IAsyncEnumerable<IPermission> GetPermissionsAsync<T>(string owner,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));

        await _permissions.CreateTableIfNotExistsAsync(cancellationToken);

        var filter = $"PartitionKey eq '{owner}-{typeof(T).FullName}'";
        var permissions = _permissions.QueryAsync(filter, cancellationToken: cancellationToken);
        await foreach (var permission in permissions.WithCancellation(cancellationToken))
            yield return permission;
    }

    public async Task SetPermissionAsync<T>(string owner, string targetId,
        CancellationToken cancellationToken = default)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (targetId == null) throw new ArgumentNullException(nameof(targetId));

        await _permissions.CreateTableIfNotExistsAsync(cancellationToken);
        await _permissions
            .UpsertAsync(new PermissionEntity { Owner = owner, TargetId = targetId, TargetType = typeof(T) },
                cancellationToken: cancellationToken);

        await _permissionsMirrored.CreateTableIfNotExistsAsync(cancellationToken);
        await _permissionsMirrored
            .UpsertAsync(new PermissionEntityMirrored { Owner = owner, TargetId = targetId, TargetType = typeof(T) },
                cancellationToken: cancellationToken);
    }
}
