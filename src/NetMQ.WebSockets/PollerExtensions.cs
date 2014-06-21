using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.WebSockets
{
    public static class PollerExtensions
    {
        public static void AddWSSocket(this Poller poller, WSSocket socket)
        {
            poller.AddSocket(socket.Socket);
        }
    }
}
