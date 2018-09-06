using BlockTree.Core;
using System;

namespace BlockTree.Implementations.Blockchains.LevelDB
{
    public class ApplicationExecutedEventArgs : EventArgs
    {
        public Transaction Transaction { get; internal set; }
        public ApplicationExecutionResult[] ExecutionResults { get; internal set; }
    }
}
