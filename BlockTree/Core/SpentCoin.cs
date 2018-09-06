namespace BlockTree.Core
{
    public class SpentCoin
    {
        public TransactionOutput Output;
        public uint StartHeight;
        public uint EndHeight;

        public Fixed10 Value => Output.Value;
    }
}
