﻿using System;
using Rocket.API.Providers.Remoting;

namespace Rocket.Core.Providers.Remoting
{
    public class RocketRemotingProviderProxy : IRocketRemotingProvider
    {
        public void Load(bool isReload = false)
        {
            throw new NotImplementedException();
        }

        public RockedCommandExecute OnExecute()
        {
            throw new NotImplementedException();
        }

        public void Unload(bool isReload = false)
        {
            throw new NotImplementedException();
        }
    }
}