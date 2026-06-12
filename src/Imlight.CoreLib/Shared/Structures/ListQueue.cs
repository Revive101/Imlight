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
using System.Collections.Generic;

namespace Imlight.CoreLib.Shared.Structures;

public class ListQueue<T> : List<T> {

    public new void Add(T item) => throw new NotSupportedException();
    public new void AddRange(IEnumerable<T> collection) => throw new NotSupportedException();
    public new void Insert(int index, T item) => throw new NotSupportedException();
    public new void InsertRange(int index, IEnumerable<T> collection) => throw new NotSupportedException();
    public new void Reverse() => throw new NotSupportedException();
    public new void Reverse(int index, int count) => throw new NotSupportedException();
    public new void Sort() => throw new NotSupportedException();
    public new void Sort(Comparison<T> comparison) => throw new NotSupportedException();
    public new void Sort(IComparer<T> comparer) => throw new NotSupportedException();
    public new void Sort(int index, int count, IComparer<T> comparer) => throw new NotSupportedException();

    public void Enqueue(T item) => base.Add(item);

    public T Dequeue() {
        var t = base[0];
        RemoveAt(0);
        
        return t;
    }

    public T Peek() => base[0];
    
}
