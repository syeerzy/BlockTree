using BlockTree.VM;

namespace BlockTree.SmartContract
{
    internal class StorageContext : IInteropInterface
    {
        public UInt160 ScriptHash;
        public bool IsReadOnly;

        public byte[] ToArray()
        {
            return ScriptHash.ToArray();
        }
    }
}
