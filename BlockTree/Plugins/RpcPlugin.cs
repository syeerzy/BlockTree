﻿using Microsoft.AspNetCore.Http;
using BlockTree.IO.Json;
using System.Collections.Generic;

namespace BlockTree.Plugins
{
    public abstract class RpcPlugin : Plugin
    {
        private static readonly List<RpcPlugin> instances = new List<RpcPlugin>();

        public new static IEnumerable<RpcPlugin> Instances => instances;

        protected RpcPlugin()
        {
            instances.Add(this);
        }

        internal protected virtual JObject OnProcess(HttpContext context, string method, JArray _params) => null;
    }
}
