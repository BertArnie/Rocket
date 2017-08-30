﻿using System;
using Rocket.Core;
using Rocket.Plugins.ScriptBase;

namespace Rocket.Plugins.ClearScript
{
    public abstract class ClearScriptEngine : ScriptEngine
    {
        protected override ScriptResult ExecuteFile(string path, string entryPoint, ref IScriptContext context, ScriptPluginMeta meta,
            bool createPluginInstanceOnNull = false)
        {
            Microsoft.ClearScript.ScriptEngine engine = (Microsoft.ClearScript.ScriptEngine)context?.BindingsObj;
            if (context == null)
            {
                engine = CreateNewEngine();
                ScriptRocketPlugin pl = null;
                if (createPluginInstanceOnNull)
                {
                    pl = new ScriptRocketPlugin();
                    pl.PluginMeta = meta;
                }

                context = new ScriptContext<Microsoft.ClearScript.ScriptEngine>(pl, this, engine);
                if(pl != null)
                    pl.ScriptContext = context;
                RegisterContext(context);
            }

            if (engine == null)
            {
                return new ScriptResult(ScriptExecutionResult.FAILED_MISC);
            }

            engine.AllowReflection = false; //todo: make configurable
            engine.EnableAutoHostVariables = true;
            var ret = engine.Invoke(entryPoint, GetScriptIniter(context));
            var res = new ScriptResult(ScriptExecutionResult.SUCCESS);
            res.HasReturn = true; //unknown thanks to JavaScripts non existant type safety, yay!
            res.Return = ret;
            return res;
        }

        public override void RegisterType(string name, Type type, IScriptContext context)
        {
            var eng = (Microsoft.ClearScript.ScriptEngine)context.BindingsObj;
            eng.AddHostType(name, type);
        }

        protected abstract Microsoft.ClearScript.ScriptEngine CreateNewEngine();
    }
}