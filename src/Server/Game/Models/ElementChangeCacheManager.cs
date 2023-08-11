using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raven.Client.Documents;

namespace Imlight.Server.Game.Models;

public class ElementChangeCacheManager
{
    private readonly IDocumentStore _documentStore;
    private readonly Dictionary<string, IChangeCache> _changeCaches;
    private readonly ulong _accountId;

    public ElementChangeCacheManager(IDocumentStore documentStore, ulong accountId)
    {
        this._accountId = accountId;
        this._documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
        this._changeCaches = new Dictionary<string, IChangeCache>();
    }

    /// <summary>
    /// Enqueues a new element to be persistently saved to the <see cref="DocumentStore"/> database.
    /// </summary>
    /// <param name="elementName">The name of the element.</param>
    /// <param name="batchSize">The amount of requests that must be received for this specific element before
    /// the changes are saved to the database.</param>
    /// <param name="change">The new value of the element.</param>
    /// <typeparam name="T">The type of element.</typeparam>
    public void EnqueueChange<T>(string elementName, byte batchSize, T change)
    {
        if (!_changeCaches.TryGetValue(elementName, out var changeCache))
        {
            // Make a new change cache if one does not exist.
            changeCache = new ElementChangeCache<T>(_accountId, elementName, batchSize);
            _changeCaches[elementName] = changeCache;
        }

        changeCache.EnqueueChange(change);
    }
    
    /// <summary>
    /// Flushes the batched changes of a specific element.
    /// </summary>
    /// <param name="elementName"></param>
    /// <param name="change"></param>
    /// <typeparam name="T"></typeparam>
    public async Task FlushChangeAsync<T>(string elementName, T change)
    {
        if (!_changeCaches.TryGetValue(elementName, out var changeCache))
        {
            // Make a new change cache if one does not exist.
            changeCache = new ElementChangeCache<T>(_accountId, elementName, 1);
            _changeCaches[elementName] = changeCache;
        }

        _changeCaches[elementName].EnqueueChange(change);
        await changeCache.FlushChangesAsync();
    }

    /// <summary>
    /// Flushes all the batched changes of the manager.
    /// </summary>
    public async Task FlushAllChangesAsync()
    {
        foreach (var changeCache in _changeCaches.Values)
        {
            await changeCache.FlushChangesAsync();
        }
    }
}