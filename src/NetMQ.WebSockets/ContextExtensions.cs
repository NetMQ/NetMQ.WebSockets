using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetMQ.WebSockets
{
    public static class ContextExtensions
    {
        public static WSRouter CreateWSRouter(this NetMQContext context)
        {
            return new WSRouter(context);
        }

        public static WSPublisher CreateWSPublisher(this NetMQContext context)
        {
            return new WSPublisher(context);
        }
    }
}
