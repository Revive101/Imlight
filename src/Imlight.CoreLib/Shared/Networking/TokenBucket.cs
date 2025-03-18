/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;

namespace Imlight.CoreLib.Shared.Networking;

public class TokenBucket {

    private readonly int _maxTokens;
    private readonly int _tokensPerSecond;
    private int _tokens;
    private int _failedAcquisitionCount;
    private DateTime _lastRefillTime;
    private readonly object _tokenBucketLock = new();

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

            _failedAcquisitionCount++;

            return false;
        }
    }

    public int GetFailedAcquisitionCount() 
        => _failedAcquisitionCount;

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
