﻿using Comfyg.Store.Contracts;
using Comfyg.Store.Contracts.Requests;

namespace Comfyg.Client.Operations;

public interface IComfygValueOperations<T> : IDisposable where T : IComfygValue
{
    IAsyncEnumerable<T> GetValuesAsync(DateTimeOffset? since = null, CancellationToken cancellationToken = default);

    Task AddValuesAsync(AddValuesRequest<T> request, CancellationToken cancellationToken = default);
}
