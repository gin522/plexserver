﻿using Emby.Dlna.Common;
using System.Collections.Generic;

namespace Emby.Dlna.ConnectionManager
{
    public class ServiceActionListBuilder
    {
        public IEnumerable<ServiceAction> GetActions()
        {
            var list = new List<ServiceAction>
            {
                GetCurrentConnectionInfo(),
                GetProtocolInfo(),
                GetCurrentConnectionIDs(),
                ConnectionComplete(),
                PrepareForConnection()
            };

            return list;
        }

        private ServiceAction PrepareForConnection()
        {
            var action = new ServiceAction
            {
                Name = "PrepareForConnection"
            };

            action.ArgumentList.Add(new Argument
            {
                Name = "RemoteProtocolInfo",
                Direction = "in",
                RelatedStateVariable = "A_ARG_TYPE_ProtocolInfo"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "PeerConnectionManager",
                Direction = "in",
                RelatedStateVariable = "A_ARG_TYPE_ConnectionManager"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "PeerConnectionID",
                Direction = "in",
                RelatedStateVariable = "A_ARG_TYPE_ConnectionID"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "Direction",
                Direction = "in",
                RelatedStateVariable = "A_ARG_TYPE_Direction"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "ConnectionID",
                Direction = "out",
                RelatedStateVariable = "A_ARG_TYPE_ConnectionID"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "AVTransportID",
                Direction = "out",
                RelatedStateVariable = "A_ARG_TYPE_AVTransportID"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "RcsID",
                Direction = "out",
                RelatedStateVariable = "A_ARG_TYPE_RcsID"
            });

            return action;
        }
        
        private ServiceAction GetCurrentConnectionInfo()
        {
            var action = new ServiceAction
            {
                Name = "GetCurrentConnectionInfo"
            };

            action.ArgumentList.Add(new Argument
            {
                Name = "ConnectionID",
                Direction = "in",
                RelatedStateVariable = "A_ARG_TYPE_ConnectionID"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "RcsID",
                Direction = "out",
                RelatedStateVariable = "A_ARG_TYPE_RcsID"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "AVTransportID",
                Direction = "out",
                RelatedStateVariable = "A_ARG_TYPE_AVTransportID"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "ProtocolInfo",
                Direction = "out",
                RelatedStateVariable = "A_ARG_TYPE_ProtocolInfo"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "PeerConnectionManager",
                Direction = "out",
                RelatedStateVariable = "A_ARG_TYPE_ConnectionManager"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "PeerConnectionID",
                Direction = "out",
                RelatedStateVariable = "A_ARG_TYPE_ConnectionID"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "Direction",
                Direction = "out",
                RelatedStateVariable = "A_ARG_TYPE_Direction"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "Status",
                Direction = "out",
                RelatedStateVariable = "A_ARG_TYPE_ConnectionStatus"
            });

            return action;
        }

        private ServiceAction GetProtocolInfo()
        {
            var action = new ServiceAction
            {
                Name = "GetProtocolInfo"
            };

            action.ArgumentList.Add(new Argument
            {
                Name = "Source",
                Direction = "out",
                RelatedStateVariable = "SourceProtocolInfo"
            });

            action.ArgumentList.Add(new Argument
            {
                Name = "Sink",
                Direction = "out",
                RelatedStateVariable = "SinkProtocolInfo"
            });

            return action;
        }

        private ServiceAction GetCurrentConnectionIDs()
        {
            var action = new ServiceAction
            {
                Name = "GetCurrentConnectionIDs"
            };

            action.ArgumentList.Add(new Argument
            {
                Name = "ConnectionIDs",
                Direction = "out",
                RelatedStateVariable = "CurrentConnectionIDs"
            });

            return action;
        }

        private ServiceAction ConnectionComplete()
        {
            var action = new ServiceAction
            {
                Name = "ConnectionComplete"
            };

            action.ArgumentList.Add(new Argument
            {
                Name = "ConnectionID",
                Direction = "in",
                RelatedStateVariable = "A_ARG_TYPE_ConnectionID"
            });

            return action;
        }
    }
}
