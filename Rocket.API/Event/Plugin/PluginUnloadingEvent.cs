﻿using Rocket.API.Plugins;

namespace Rocket.API.Event.Plugin
{
    public class PluginUnloadingEvent : PluginEvent
    {
        public PluginUnloadingEvent(IPlugin plugin) : base(plugin)
        {
        }
    }
}