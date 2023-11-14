using System;
using System.Collections.Generic;
using System.Threading;

namespace Imlight.CoreLib.Shared.Networking;

public class TokenBucket {
    private readonly int _maxTokens;
    private readonly int _tokensPerSecond;
    private readonly Queue<DateTime> _tokenBucket;
    private int _failedAcquistionCount;

    public TokenBucket(int maxTokens, int tokensPerSecond) {
        this._maxTokens = maxTokens;
        this._tokensPerSecond = tokensPerSecond;
        this._tokenBucket = new Queue<DateTime>(maxTokens);
    }

    public bool TryAcquire() {
        lock (_tokenBucket) {
            // Remove expired tokens
            while (_tokenBucket.Count > 0 && DateTime.Now - _tokenBucket.Peek() > TimeSpan.FromSeconds(1)) {
                _tokenBucket.Dequeue();
            }

            // Check if there are enough tokens
            if (_tokenBucket.Count < _maxTokens && _tokenBucket.Count < _tokensPerSecond) {
                // Add a new token to the bucket
                _tokenBucket.Enqueue(DateTime.Now);
                return true;
            }

            _failedAcquistionCount++;
            return false;
        }
    }

    public int GetFailedAcquisitionCount() {
        return _failedAcquistionCount;
    }
}
