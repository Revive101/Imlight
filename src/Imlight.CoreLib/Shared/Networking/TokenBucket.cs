/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Threading;

namespace Imlight.CoreLib.Shared.Networking;

public class TokenBucket {

    private readonly int _maxTokens;
    private readonly int _tokensPerSecond;
    private int _tokens;
    private int _failedAcquisitionCount;
    private DateTime _lastRefillTime;
    private readonly Lock _tokenBucketLock = new();

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
