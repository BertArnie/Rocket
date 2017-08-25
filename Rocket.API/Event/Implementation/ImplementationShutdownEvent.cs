﻿using Rocket.API.Providers.Implementation;

namespace Rocket.API.Event.Implementation
{
    public class ImplementationShutdownEvent : ImplementationEvent
    {
        public ImplementationShutdownEvent(IGameProvider implementation) : base(implementation)
        {
        }
    }
}