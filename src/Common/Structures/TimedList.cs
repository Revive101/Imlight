/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Threading;

namespace Imlight.Common.Structures;

public class TimedList<T>
{
    private readonly int timeout;
    private readonly Dictionary<T, Timer> elements;

    public TimedList(int timeout)
    {
        this.timeout = timeout;
        this.elements = new Dictionary<T, Timer>();
    }

    public void Add(T element)
    {
        Timer timer = null;
        timer = new Timer((obj) =>
        {
            lock (elements)
            {
                elements.Remove((T)obj);
                timer.Dispose();
            }
        }, element, timeout, Timeout.Infinite);
        lock (elements)
        {
            elements[element] = timer;
        }
    }

    public bool Contains(T element)
    {
        lock (elements)
        {
            return elements.ContainsKey(element);
        }
    }
}
