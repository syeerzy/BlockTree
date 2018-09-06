﻿using BlockTree.Core;
using System.ComponentModel;
using System.Drawing.Design;

namespace BlockTree.UI.Wrappers
{
    internal class InvocationTransactionWrapper : TransactionWrapper
    {
        [Editor(typeof(ScriptEditor), typeof(UITypeEditor))]
        [TypeConverter(typeof(HexConverter))]
        public byte[] Script { get; set; }
        [TypeConverter(typeof(Fixed8Converter))]
        public Fixed10 Gas { get; set; }

        public InvocationTransactionWrapper()
        {
            this.Version = 1;
        }

        public override Transaction Unwrap()
        {
            InvocationTransaction tx = (InvocationTransaction)base.Unwrap();
            tx.Script = Script;
            tx.Gas = Gas;
            return tx;
        }
    }
}
