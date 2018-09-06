using BlockTree.IO;
using BlockTree.IO.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace BlockTree.Core
{
    public class InvocationTransaction : Transaction
    {
        public byte[] Script;
        public Fixed10 Gas;

        public override int Size => base.Size + Script.GetVarSize();

        public override Fixed10 SystemFee => Gas;

        public InvocationTransaction()
            : base(TransactionType.InvocationTransaction)
        {
        }

        protected override void DeserializeExclusiveData(BinaryReader reader)
        {
            if (Version > 1) throw new FormatException();
            Script = reader.ReadVarBytes(65536);
            if (Script.Length == 0) throw new FormatException();
            if (Version >= 1)
            {
                Gas = reader.ReadSerializable<Fixed10>();
                if (Gas < Fixed10.Zero) throw new FormatException();
            }
            else
            {
                Gas = Fixed10.Zero;
            }
        }

        public static Fixed10 GetGas(Fixed10 consumed)
        {
            Fixed10 gas = consumed - Fixed10.FromDecimal(10);
            if (gas <= Fixed10.Zero) return Fixed10.Zero;
            return gas.Ceiling();
        }

        protected override void SerializeExclusiveData(BinaryWriter writer)
        {
            writer.WriteVarBytes(Script);
            if (Version >= 1)
                writer.Write(Gas);
        }

        public override JObject ToJson()
        {
            JObject json = base.ToJson();
            json["script"] = Script.ToHexString();
            json["gas"] = Gas.ToString();
            return json;
        }

        public override bool Verify(IEnumerable<Transaction> mempool)
        {
            if (Gas.GetData() % 100000000 != 0) return false;
            return base.Verify(mempool);
        }
    }
}
