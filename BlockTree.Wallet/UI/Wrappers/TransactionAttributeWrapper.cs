﻿using BlockTree.Core;
using System.ComponentModel;

namespace BlockTree.UI.Wrappers
{
    internal class TransactionAttributeWrapper
    {
        public TransactionAttributeUsage Usage { get; set; }
        [TypeConverter(typeof(HexConverter))]
        public byte[] Data { get; set; }

        public TransactionAttribute Unwrap()
        {
            return new TransactionAttribute
            {
                Usage = Usage,
                Data = Data
            };
        }
    }
}
