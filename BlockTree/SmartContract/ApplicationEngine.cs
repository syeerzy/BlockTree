﻿using BlockTree.Core;
using BlockTree.IO.Caching;
using BlockTree.VM;
using BlockTree.VM.Types;
using System.Collections;
using System.Linq;
using System.Numerics;
using System.Text;

namespace BlockTree.SmartContract
{
    public class ApplicationEngine : ExecutionEngine
    {
        #region Limits
        /// <summary>
        /// Max value for SHL and SHR
        /// </summary>
        private const int Max_SHL_SHR = ushort.MaxValue;
        /// <summary>
        /// Min value for SHL and SHR
        /// </summary>
        private const int Min_SHL_SHR = -Max_SHL_SHR;
        /// <summary>
        /// Set the max size allowed size for BigInteger
        /// </summary>
        private const int MaxSizeForBigInteger = 32;
        /// <summary>
        /// Set the max Stack Size
        /// </summary>
        private const uint MaxStackSize = 2 * 1024;
        /// <summary>
        /// Set Max Item Size
        /// </summary>
        private const uint MaxItemSize = 1024 * 1024;
        /// <summary>
        /// Set Max Invocation Stack Size
        /// </summary>
        private const uint MaxInvocationStackSize = 1024;
        /// <summary>
        /// Set Max Array Size
        /// </summary>
        private const uint MaxArraySize = 1024;
        #endregion

        private const long ratio = 100000;
        private const long gas_free = 10 * 100000000;
        private readonly long gas_amount;
        private long gas_consumed = 0;
        private readonly bool testMode;

        private readonly CachedScriptTable script_table;

        public TriggerType Trigger { get; }
        public Fixed10 GasConsumed => new Fixed10(gas_consumed);

        public ApplicationEngine(TriggerType trigger, IScriptContainer container, IScriptTable table, InteropService service, Fixed10 gas, bool testMode = false)
            : base(container, Cryptography.Crypto.Default, table, service)
        {
            this.gas_amount = gas_free + gas.GetData();
            this.testMode = testMode;
            this.Trigger = trigger;
            if (table is CachedScriptTable)
            {
                this.script_table = (CachedScriptTable)table;
            }
        }

        private bool CheckArraySize(OpCode nextInstruction)
        {
            int size;
            switch (nextInstruction)
            {
                case OpCode.PACK:
                case OpCode.NEWARRAY:
                case OpCode.NEWSTRUCT:
                    {
                        if (CurrentContext.EvaluationStack.Count == 0) return false;
                        size = (int)CurrentContext.EvaluationStack.Peek().GetBigInteger();
                    }
                    break;
                case OpCode.SETITEM:
                    {
                        if (CurrentContext.EvaluationStack.Count < 3) return false;
                        if (!(CurrentContext.EvaluationStack.Peek(2) is Map map)) return true;
                        StackItem key = CurrentContext.EvaluationStack.Peek(1);
                        if (key is ICollection) return false;
                        if (map.ContainsKey(key)) return true;
                        size = map.Count + 1;
                    }
                    break;
                case OpCode.APPEND:
                    {
                        if (CurrentContext.EvaluationStack.Count < 2) return false;
                        if (!(CurrentContext.EvaluationStack.Peek(1) is Array array)) return false;
                        size = array.Count + 1;
                    }
                    break;
                default:
                    return true;
            }
            return size <= MaxArraySize;
        }

        private bool CheckInvocationStack(OpCode nextInstruction)
        {
            switch (nextInstruction)
            {
                case OpCode.CALL:
                case OpCode.APPCALL:
                    if (InvocationStack.Count >= MaxInvocationStackSize) return false;
                    return true;
                default:
                    return true;
            }
        }

        private bool CheckItemSize(OpCode nextInstruction)
        {
            switch (nextInstruction)
            {
                case OpCode.PUSHDATA4:
                    {
                        if (CurrentContext.InstructionPointer + 4 >= CurrentContext.Script.Length)
                            return false;
                        uint length = CurrentContext.Script.ToUInt32(CurrentContext.InstructionPointer + 1);
                        if (length > MaxItemSize) return false;
                        return true;
                    }
                case OpCode.CAT:
                    {
                        if (CurrentContext.EvaluationStack.Count < 2) return false;
                        int length = CurrentContext.EvaluationStack.Peek(0).GetByteArray().Length + CurrentContext.EvaluationStack.Peek(1).GetByteArray().Length;
                        if (length > MaxItemSize) return false;
                        return true;
                    }
                default:
                    return true;
            }
        }

        /// <summary>
        /// Check if the BigInteger is allowed for numeric operations
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>Return True if are allowed, otherwise False</returns>
        private bool CheckBigInteger(BigInteger value)
        {
            return value == null ? false :
                value.ToByteArray().Length <= MaxSizeForBigInteger;
        }

        /// <summary>
        /// Check if the BigInteger is allowed for numeric operations
        /// </summary> 
        private bool CheckBigIntegers(OpCode nextInstruction)
        {
            switch (nextInstruction)
            {
                case OpCode.SHL:
                    {
                        BigInteger ishift = CurrentContext.EvaluationStack.Peek(0).GetBigInteger();

                        if ((ishift > Max_SHL_SHR || ishift < Min_SHL_SHR))
                            return false;

                        BigInteger x = CurrentContext.EvaluationStack.Peek(1).GetBigInteger();

                        if (!CheckBigInteger(x << (int)ishift))
                            return false;

                        break;
                    }
                case OpCode.SHR:
                    {
                        BigInteger ishift = CurrentContext.EvaluationStack.Peek(0).GetBigInteger();

                        if ((ishift > Max_SHL_SHR || ishift < Min_SHL_SHR))
                            return false;

                        BigInteger x = CurrentContext.EvaluationStack.Peek(1).GetBigInteger();

                        if (!CheckBigInteger(x >> (int)ishift))
                            return false;

                        break;
                    }
                case OpCode.INC:
                    {
                        BigInteger x = CurrentContext.EvaluationStack.Peek().GetBigInteger();

                        if (!CheckBigInteger(x) || !CheckBigInteger(x + 1))
                            return false;

                        break;
                    }
                case OpCode.DEC:
                    {
                        BigInteger x = CurrentContext.EvaluationStack.Peek().GetBigInteger();

                        if (!CheckBigInteger(x) || (x.Sign <= 0 && !CheckBigInteger(x - 1)))
                            return false;

                        break;
                    }
                case OpCode.ADD:
                    {
                        BigInteger x2 = CurrentContext.EvaluationStack.Peek().GetBigInteger();
                        BigInteger x1 = CurrentContext.EvaluationStack.Peek(1).GetBigInteger();

                        if (!CheckBigInteger(x2) || !CheckBigInteger(x1) || !CheckBigInteger(x1 + x2))
                            return false;

                        break;
                    }
                case OpCode.SUB:
                    {
                        BigInteger x2 = CurrentContext.EvaluationStack.Peek().GetBigInteger();
                        BigInteger x1 = CurrentContext.EvaluationStack.Peek(1).GetBigInteger();

                        if (!CheckBigInteger(x2) || !CheckBigInteger(x1) || !CheckBigInteger(x1 - x2))
                            return false;

                        break;
                    }
                case OpCode.MUL:
                    {
                        BigInteger x2 = CurrentContext.EvaluationStack.Peek().GetBigInteger();
                        BigInteger x1 = CurrentContext.EvaluationStack.Peek(1).GetBigInteger();

                        int lx1 = x1 == null ? 0 : x1.ToByteArray().Length;

                        if (lx1 > MaxSizeForBigInteger)
                            return false;

                        int lx2 = x2 == null ? 0 : x2.ToByteArray().Length;

                        if ((lx1 + lx2) > MaxSizeForBigInteger)
                            return false;

                        break;
                    }
                case OpCode.DIV:
                    {
                        BigInteger x2 = CurrentContext.EvaluationStack.Peek().GetBigInteger();
                        BigInteger x1 = CurrentContext.EvaluationStack.Peek(1).GetBigInteger();

                        if (!CheckBigInteger(x2) || !CheckBigInteger(x1))
                            return false;

                        break;
                    }
                case OpCode.MOD:
                    {
                        BigInteger x2 = CurrentContext.EvaluationStack.Peek().GetBigInteger();
                        BigInteger x1 = CurrentContext.EvaluationStack.Peek(1).GetBigInteger();

                        if (!CheckBigInteger(x2) || !CheckBigInteger(x1))
                            return false;

                        break;
                    }
            }

            return true;
        }

        private bool CheckStackSize(OpCode nextInstruction)
        {
            int size = 0;
            if (nextInstruction <= OpCode.PUSH16)
                size = 1;
            else
                switch (nextInstruction)
                {
                    case OpCode.DEPTH:
                    case OpCode.DUP:
                    case OpCode.OVER:
                    case OpCode.TUCK:
                    case OpCode.NEWMAP:
                        size = 1;
                        break;
                    case OpCode.UNPACK:
                        StackItem item = CurrentContext.EvaluationStack.Peek();
                        if (item is Array array)
                            size = array.Count;
                        else
                            return false;
                        break;
                }
            if (size == 0) return true;
            size += InvocationStack.Sum(p => p.EvaluationStack.Count + p.AltStack.Count);
            if (size > MaxStackSize) return false;
            return true;
        }

        private bool CheckDynamicInvoke(OpCode nextInstruction)
        {
            if (nextInstruction == OpCode.APPCALL || nextInstruction == OpCode.TAILCALL)
            {
                for (int i = CurrentContext.InstructionPointer + 1; i < CurrentContext.InstructionPointer + 21; i++)
                {
                    if (CurrentContext.Script[i] != 0) return true;
                }
                // if we get this far it is a dynamic call
                // now look at the current executing script
                // to determine if it can do dynamic calls
                ContractState contract = script_table.GetContractState(CurrentContext.ScriptHash);
                return contract.HasDynamicInvoke;
            }
            return true;
        }

        public new bool Execute()
        {
            try
            {
                while (!State.HasFlag(VMState.HALT) && !State.HasFlag(VMState.FAULT))
                {
                    if (CurrentContext.InstructionPointer < CurrentContext.Script.Length)
                    {
                        OpCode nextOpcode = CurrentContext.NextInstruction;

                        gas_consumed = checked(gas_consumed + GetPrice(nextOpcode) * ratio);
                        if (!testMode && gas_consumed > gas_amount)
                        {
                            State |= VMState.FAULT;
                            return false;
                        }

                        if (!CheckItemSize(nextOpcode) ||
                            !CheckStackSize(nextOpcode) ||
                            !CheckArraySize(nextOpcode) ||
                            !CheckInvocationStack(nextOpcode) ||
                            !CheckBigIntegers(nextOpcode) ||
                            !CheckDynamicInvoke(nextOpcode))
                        {
                            State |= VMState.FAULT;
                            return false;
                        }
                    }
                    StepInto();
                }
            }
            catch
            {
                State |= VMState.FAULT;
                return false;
            }
            return !State.HasFlag(VMState.FAULT);
        }

        protected virtual long GetPrice(OpCode nextInstruction)
        {
            if (nextInstruction <= OpCode.PUSH16) return 0;
            switch (nextInstruction)
            {
                case OpCode.NOP:
                    return 0;
                case OpCode.APPCALL:
                case OpCode.TAILCALL:
                    return 10;
                case OpCode.SYSCALL:
                    return GetPriceForSysCall();
                case OpCode.SHA1:
                case OpCode.SHA256:
                    return 10;
                case OpCode.HASH160:
                case OpCode.HASH256:
                    return 20;
                case OpCode.CHECKSIG:
                    return 100;
                case OpCode.CHECKMULTISIG:
                    {
                        if (CurrentContext.EvaluationStack.Count == 0) return 1;
                        int n = (int)CurrentContext.EvaluationStack.Peek().GetBigInteger();
                        if (n < 1) return 1;
                        return 100 * n;
                    }
                default: return 1;
            }
        }

        protected virtual long GetPriceForSysCall()
        {
            if (CurrentContext.InstructionPointer >= CurrentContext.Script.Length - 3)
                return 1;
            byte length = CurrentContext.Script[CurrentContext.InstructionPointer + 1];
            if (CurrentContext.InstructionPointer > CurrentContext.Script.Length - length - 2)
                return 1;
            string api_name = Encoding.ASCII.GetString(CurrentContext.Script, CurrentContext.InstructionPointer + 2, length);
            switch (api_name)
            {
                case "System.Runtime.CheckWitness":
                case "BlockTree.Runtime.CheckWitness":
                    return 200;
                case "System.Blockchain.GetHeader":
                case "BlockTree.Blockchain.GetHeader":
                    return 100;
                case "System.Blockchain.GetBlock":
                case "BlockTree.Blockchain.GetBlock":
                    return 200;
                case "System.Blockchain.GetTransaction":
                case "BlockTree.Blockchain.GetTransaction":
                    return 100;
                case "System.Blockchain.GetTransactionHeight":
                case "BlockTree.Blockchain.GetTransactionHeight":
                    return 100;
                case "BlockTree.Blockchain.GetAccount":
                    return 100;
                case "BlockTree.Blockchain.GetValidators":
                    return 200;
                case "BlockTree.Blockchain.GetAsset":
                    return 100;
                case "System.Blockchain.GetContract":
                case "BlockTree.Blockchain.GetContract":
                    return 100;
                case "BlockTree.Transaction.GetReferences":
                case "BlockTree.Transaction.GetUnspentCoins":
                    return 200;
                case "BlockTree.Account.SetVotes":
                    return 1000;
                case "BlockTree.Validator.Register":
                    return 1000L * 100000000L / ratio;
                case "BlockTree.Asset.Create":
                    return 5000L * 100000000L / ratio;
                case "BlockTree.Asset.Renew":
                    return (byte)CurrentContext.EvaluationStack.Peek(1).GetBigInteger() * 5000L * 100000000L / ratio;
                case "BlockTree.Contract.Create":
                case "BlockTree.Contract.Migrate":
                    long fee = 100L;

                    ContractPropertyState contract_properties = (ContractPropertyState)(byte)CurrentContext.EvaluationStack.Peek(3).GetBigInteger();

                    if (contract_properties.HasFlag(ContractPropertyState.HasStorage))
                    {
                        fee += 400L;
                    }
                    if (contract_properties.HasFlag(ContractPropertyState.HasDynamicInvoke))
                    {
                        fee += 500L;
                    }
                    return fee * 100000000L / ratio;
                case "System.Storage.Get":
                case "BlockTree.Storage.Get":
                    return 100;
                case "System.Storage.Put":
                case "BlockTree.Storage.Put":
                    return ((CurrentContext.EvaluationStack.Peek(1).GetByteArray().Length + CurrentContext.EvaluationStack.Peek(2).GetByteArray().Length - 1) / 1024 + 1) * 1000;
                case "System.Storage.Delete":
                case "BlockTree.Storage.Delete":
                    return 100;
                default:
                    return 1;
            }
        }

        public static ApplicationEngine Run(byte[] script, IScriptContainer container = null, Block persisting_block = null)
        {
            if (persisting_block == null)
                persisting_block = new Block
                {
                    Version = 0,
                    PrevHash = Blockchain.Default.CurrentBlockHash,
                    MerkleRoot = new UInt256(),
                    Timestamp = Blockchain.Default.GetHeader(Blockchain.Default.Height).Timestamp + Blockchain.SecondsPerBlock,
                    Index = Blockchain.Default.Height + 1,
                    ConsensusData = 0,
                    NextConsensus = Blockchain.Default.GetHeader(Blockchain.Default.Height).NextConsensus,
                    Script = new Witness
                    {
                        InvocationScript = new byte[0],
                        VerificationScript = new byte[0]
                    },
                    Transactions = new Transaction[0]
                };
            DataCache<UInt160, AccountState> accounts = Blockchain.Default.GetStates<UInt160, AccountState>();
            DataCache<UInt256, AssetState> assets = Blockchain.Default.GetStates<UInt256, AssetState>();
            DataCache<UInt160, ContractState> contracts = Blockchain.Default.GetStates<UInt160, ContractState>();
            DataCache<StorageKey, StorageItem> storages = Blockchain.Default.GetStates<StorageKey, StorageItem>();
            CachedScriptTable script_table = new CachedScriptTable(contracts);
            using (StateMachine service = new StateMachine(persisting_block, accounts, assets, contracts, storages))
            {
                ApplicationEngine engine = new ApplicationEngine(TriggerType.Application, container, script_table, service, Fixed10.Zero, true);
                engine.LoadScript(script);
                engine.Execute();
                return engine;
            }
        }
    }
}
