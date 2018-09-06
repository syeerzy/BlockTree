using BlockTree.VM;
using System.Collections.Generic;

namespace BlockTree.SmartContract.Enumerators
{
    internal class ArrayWrapper : IEnumerator
    {
        private readonly IEnumerator<StackItem> enumerator;

        public ArrayWrapper(IEnumerable<StackItem> array)
        {
            this.enumerator = array.GetEnumerator();
        }

        public void Dispose()
        {
            enumerator.Dispose();
        }

        public bool Next()
        {
            return enumerator.MoveNext();
        }

        public StackItem Value()
        {
            return enumerator.Current;
        }
    }
}
