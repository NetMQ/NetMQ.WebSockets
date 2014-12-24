using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Actors;
using NetMQ.InProcActors;
using NetMQ.Sockets;
using NetMQ.zmq;

namespace NetMQ.WebSockets
{
    public class WSRouter : WSSocket
    {
        class RouterShimHandler : BaseShimHandler
        {
            public RouterShimHandler(NetMQContext context)
                : base(context)
            {
            }

            protected override void OnOutgoingMessage(NetMQMessage message)
            {
                byte[] identity = message.Pop().ToByteArray();

                //  Each frame is a full ZMQ message with identity frame
                while (message.FrameCount > 0)
                {
                    var data = message.Pop().ToByteArray(false);
                    bool more = message.FrameCount > 0;

                    WriteOutgoing(identity, data, more);
                }
            }

            protected override void OnIncomingMessage(byte[] identity,NetMQMessage message)
            {
                message.Push(identity);

                WriteIngoing(message);
            }

            protected override void OnNewClient(byte[] identity)
            {
             
            }

            protected override void OnClientRemoved(byte[] identity)
            {
             
            }
        }

        public WSRouter(NetMQContext context)
            : base(context, new RouterShimHandler(context))
        {
        }
    }
}
