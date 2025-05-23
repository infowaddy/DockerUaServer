using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opc.Ua.Server;
using Opc.Ua;

namespace DockerUaServer
{
    public class DockerUaServerSessionManager : SessionManager
    {
        public DockerUaServerSessionManager(IServerInternal server, ApplicationConfiguration configuration)
            : base(server, configuration)
        {
        }
    }
}
