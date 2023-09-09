/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Imlight.Common.Utilities;
using Imlight.Server.Game.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;

namespace Imlight.Server.WizardData;

public class ElementChangeCacheManager : IDisposable
{
    private readonly IDocumentStore _documentStore;
    private readonly Dictionary<string, object> _changeCaches;
    private readonly ulong _charId;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly int _uploadIntervalInMinutesInMilliseconds;

    // ctor
    public ElementChangeCacheManager(IDocumentStore documentStore, ulong charId, byte uploadIntervalInMinutes)
    {
        this._charId = charId;
        this._documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
        this._changeCaches = new Dictionary<string, object>();
        this._cancellationTokenSource = new CancellationTokenSource();
        this._uploadIntervalInMinutesInMilliseconds = uploadIntervalInMinutes * 60 * 1000;

        Task.Run(DoSaveInterval);
    }
    
    /// <summary>
    /// Enqueues a change to be flushed on interval.
    /// </summary>
    /// <param name="elementName"></param>
    /// <param name="change"></param>
    /// <typeparam name="T"></typeparam>
    public void EnqueueChange<T>(string elementName, T change)
    {
        _changeCaches[elementName] = change;
    }
    
    /// <summary>
    /// Enqueues a change to be flushed immediately.
    /// </summary>
    /// <param name="elementName"></param>
    /// <param name="change"></param>
    /// <typeparam name="T"></typeparam>
    public void EnqueueImmediateChange<T>(string elementName, T change)
    {
        _changeCaches[elementName] = change;
        FlushChangeAsync(new KeyValuePair<string, object>(elementName, change)).Wait();
    }

    /// <summary>
    /// Flushes all the batched changes of the manager.
    /// </summary>
    public async Task FlushAllChangesAsync()
    {
        foreach (var changeCache in _changeCaches)
        {
            await FlushChangeAsync(changeCache);
        }
    }
    
    private async Task DoSaveInterval()
    {
        // While loop with cancellation token.
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            await Task.Delay(_uploadIntervalInMinutesInMilliseconds);
            await FlushAllChangesAsync();
        }
    }
    
    private async Task FlushChangeAsync(KeyValuePair <string, object> changeCache)
    {
        // Serialize the most recent change as json.
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(changeCache.Value);
        
        // Patch.
        var patchRequest = new PatchByQueryOperation($"from \"Characters\" as c " +
                                                     $"where c.CharId = {_charId} " +
                                                     $"update {{ c.{changeCache.Key} = {json} }}");
        
        try
        {
            var operation = await _documentStore.Operations.SendAsync(patchRequest);
            await operation.WaitForCompletionAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            Log.Error("Could not save persistent stat {0} for reason: {1}",
                Log.Args(changeCache.Key, ex.Message));
        }
    }

    public void Dispose()
    {
        _documentStore?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}