using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Common
{
    public class ObservableQueue<T> : Queue<T>, INotifyCollectionChanged, IDisposable
    {

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableQueue()
        {
        }

        public ObservableQueue(IEnumerable<T> collection) : base(collection)
        {
            for (int i = 0; i < collection.Count(); i++)
                base.Enqueue(collection.ElementAt(i));
        }

        public ObservableQueue(List<T> collection) : base(collection)
        {
            for (int i = 0; i < collection.Count(); i++)
                base.Enqueue(collection.ElementAt(i));
        }

        public ObservableQueue(int capacity) : base(capacity)
        {
        }

        public new virtual void Clear()
        {
            base.Clear();
            this.CollectionChanged(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public new virtual void Enqueue(T item)
        {
            base.Enqueue(item);
            this.CollectionChanged(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
        }

        public new virtual void Dequeue()
        {
            var lastItem = base.Dequeue();
            this.CollectionChanged(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, lastItem));
        }

        ~ObservableQueue()
        {
            base.Clear();
        }

        public void Dispose()
        {
            base.Clear();
        }
    }
}
