using BlockTree.VM;
using System;

namespace BlockTree.SmartContract.Enumerators
{
    internal interface IEnumerator : IDisposable, IInteropInterface
    {
        bool Next();
        StackItem Value();
    }
}
