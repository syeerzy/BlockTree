﻿using BlockTree.VM;
using System.Collections.Generic;

namespace BlockTree.SmartContract.Iterators
{
    internal class MapWrapper : IIterator
    {
        private readonly IEnumerator<KeyValuePair<StackItem, StackItem>> enumerator;

        public MapWrapper(IEnumerable<KeyValuePair<StackItem, StackItem>> map)
        {
            this.enumerator = map.GetEnumerator();
        }

        public void Dispose()
        {
            enumerator.Dispose();
        }

        public StackItem Key()
        {
            return enumerator.Current.Key;
        }

        public bool Next()
        {
            return enumerator.MoveNext();
        }

        public StackItem Value()
        {
            return enumerator.Current.Value;
        }
    }
}
