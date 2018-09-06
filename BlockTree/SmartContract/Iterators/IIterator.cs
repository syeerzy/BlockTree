using BlockTree.SmartContract.Enumerators;
using BlockTree.VM;

namespace BlockTree.SmartContract.Iterators
{
    internal interface IIterator : IEnumerator
    {
        StackItem Key();
    }
}
