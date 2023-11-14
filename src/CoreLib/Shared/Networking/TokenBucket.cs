using System;
using System.Collections.Generic;
using System.Threading;

namespace Imlight.CoreLib.Shared.Networking;

public class TokenBucket {
    private readonly int _maxTokens;
    private readonly int _tokensPerSecond;
    private int _tokens;
    private int _failedAcquisitionCount;
    private DateTime _lastRefillTime;
    private readonly object _tokenBucketLock = new object();

    public TokenBucket(int maxTokens, int tokensPerSecond) {
        this._maxTokens = maxTokens;
        this._tokensPerSecond = tokensPerSecond;
        this._tokens = _maxTokens;
        this._lastRefillTime = DateTime.Now;
    }

    public bool TryAcquire() {
        lock (_tokenBucketLock) {
            RefillTokens();

            if (_tokens > 0) {
                _tokens--;
                return true;
            }

            return false;
        }
    }

    public int GetFailedAcquisitionCount() {
        return _failedAcquisitionCount;
    }

    private void RefillTokens() {
        DateTime now = DateTime.Now;
        double elapsedSeconds = (now - _lastRefillTime).TotalSeconds;
        int tokensToAdd = (int) (elapsedSeconds * _tokensPerSecond);

        if (tokensToAdd > 0) {
            _tokens = Math.Min(_tokens + tokensToAdd, _maxTokens);
            _lastRefillTime = now;
        }
    }
}
