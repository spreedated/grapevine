using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;

namespace Grapevine
{
    public class ListenerPrefixCollection : IListenerPrefixCollection
    {
        public int Count => this.PrefixCollection.Count;

        public bool IsReadOnly => this.PrefixCollection.IsReadOnly;

        public bool IsSynchronized => this.PrefixCollection.IsSynchronized;

        protected HttpListenerPrefixCollection PrefixCollection;

        public ListenerPrefixCollection(HttpListenerPrefixCollection prefixes)
        {
            this.PrefixCollection = prefixes;
        }

        public void Add(string item) => this.PrefixCollection.Add(item);

        public void Clear() => this.PrefixCollection.Clear();

        public bool Contains(string item) => this.PrefixCollection.Contains(item);

        public void CopyTo(Array array, int arrayIndex) => this.PrefixCollection.CopyTo(array, arrayIndex);

        public void CopyTo(string[] array, int arrayIndex) => this.PrefixCollection.CopyTo(array, arrayIndex);

        public IEnumerator<string> GetEnumerator() => this.PrefixCollection.GetEnumerator();

        public bool Remove(string item) => this.PrefixCollection.Remove(item);

        IEnumerator IEnumerable.GetEnumerator() => this.PrefixCollection.GetEnumerator();
    }
}