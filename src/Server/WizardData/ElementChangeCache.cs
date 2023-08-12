/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Threading.Tasks;
using Imlight.Common.Utilities;
using Imlight.Server.Game.Models;
using Imlight.Server.WizardData.Implementations;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;

namespace Imlight.Server.WizardData;

public class ElementChangeCache<T> : IChangeCache
{
    private readonly IDocumentStore _documentStore;
    private readonly ulong _charId;
    private readonly string _elementName;
    private readonly byte _batchSize;
    private T _mostRecentChange;
    private int _changesReceived;

    // ctor
    public ElementChangeCache(ulong charId, string elementName, byte batchSize)
    {
        this._charId = charId;
        this._elementName = elementName;
        this._documentStore = DocumentStoreSingleton.Store ?? throw new ArgumentNullException(nameof(DocumentStoreSingleton));
        this._batchSize = batchSize;
    }

    public void EnqueueChange(object change)
    {
        if (change is T typedChange)
        {
            _mostRecentChange = typedChange;
            _changesReceived++;

            if (_changesReceived >= _batchSize)
            {
                // Send this process to a random task as to not block threads.
                #pragma warning disable CS4014
                FlushChangesAsync();
                #pragma warning restore CS4014
            }
        }
        else
        {
            Log.Error("Invalid change type. Expected {0}, but received {1}",
                Log.Args(typeof(T).Name, change.GetType().Name));
        }
    }
    
    public async Task FlushChangesAsync()
    {
        // Serialize the most recent change as json.
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(_mostRecentChange);
        
        // Patch.
        var patchRequest = new PatchByQueryOperation($"from \"Characters\" as c " +
                                                     $"where c.CharId = {_charId} " +
                                                     $"update {{ c.{_elementName} = {json} }}");
        
        try
        {
            var operation = await _documentStore.Operations.SendAsync(patchRequest);
            await operation.WaitForCompletionAsync();

            _changesReceived = 0;
        }
        catch (Exception ex)
        {
            Log.Error("Could not save persistent stat {0} for reason: {1}",
                Log.Args(_elementName, ex.Message));
        }
    }
}