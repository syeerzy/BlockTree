using BlockTree.IO;
using BlockTree.IO.Json;
using BlockTree.VM;
using BlockTree.Wallets;
using System;
using System.IO;

namespace BlockTree.Core
{
    /// <summary>
    /// 交易输出
    /// </summary>
    public class TransactionOutput : IInteropInterface, ISerializable
    {
        /// <summary>
        /// 资产编号
        /// </summary>
        public UInt256 AssetId;
        /// <summary>
        /// 金额
        /// </summary>
        public Fixed10 Value;
        /// <summary>
        /// 收款地址
        /// </summary>
        public UInt160 ScriptHash;

        public int Size => AssetId.Size + Value.Size + ScriptHash.Size;

        void ISerializable.Deserialize(BinaryReader reader)
        {
            this.AssetId = reader.ReadSerializable<UInt256>();
            this.Value = reader.ReadSerializable<Fixed10>();
            if (Value <= Fixed10.Zero) throw new FormatException();
            this.ScriptHash = reader.ReadSerializable<UInt160>();
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(AssetId);
            writer.Write(Value);
            writer.Write(ScriptHash);
        }

        /// <summary>
        /// 将交易输出转变为json对象
        /// </summary>
        /// <param name="index">该交易输出在交易中的索引</param>
        /// <returns>返回json对象</returns>
        public JObject ToJson(ushort index)
        {
            JObject json = new JObject();
            json["n"] = index;
            json["asset"] = AssetId.ToString();
            json["value"] = Value.ToString();
            json["address"] = Wallet.ToAddress(ScriptHash);
            return json;
        }
    }
}
