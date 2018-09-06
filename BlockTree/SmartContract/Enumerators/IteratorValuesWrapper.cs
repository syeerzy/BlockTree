using BlockTree.SmartContract.Iterators;
using BlockTree.VM;

namespace BlockTree.SmartContract.Enumerators
{
    internal class IteratorValuesWrapper : IEnumerator
    {
        private readonly IIterator iterator;

        public IteratorValuesWrapper(IIterator iterator)
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
            return iterator.Value();
        }
    }
}
