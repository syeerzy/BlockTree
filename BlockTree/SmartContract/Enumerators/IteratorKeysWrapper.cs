﻿using BlockTree.SmartContract.Iterators;
using BlockTree.VM;

namespace BlockTree.SmartContract.Enumerators
{
    internal class IteratorKeysWrapper : IEnumerator
    {
        private readonly IIterator iterator;

        public IteratorKeysWrapper(IIterator iterator)
        {
            this.iterator = iterator;
        }

        public void Dispose()
        {
            iterator.Dispose();
        }

        public bool Next()
        {
            return iterator.Next();
        }

        public StackItem Value()
        {
            return iterator.Key();
        }
    }
}
