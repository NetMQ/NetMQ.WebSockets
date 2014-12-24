using System;
using System.Collections.Generic;
using System.Data.Odbc;
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
    public class WSSocketEventArgs : EventArgs
    {
        public WSSocketEventArgs(WSSocket wsSocket)
        {
            WSSocket = wsSocket;
        }


        public WSSocket WSSocket { get; private set; }
    }

    public class WSSocket : IDisposable, IOutgoingSocket, IReceivingSocket, ISocketPollable
    {
        private static int s_id = 0;

        private readonly NetMQContext m_context;

        internal const string BindCommand = "BIND";

        private Actor<int> m_actor;
        private PairSocket m_messagesPipe;        

        protected WSSocket(NetMQContext context, IShimHandler<int> shimHandler )
        {
            int id = Interlocked.Increment(ref s_id);
            m_context = context;

            m_messagesPipe = context.CreatePairSocket();
            m_messagesPipe.Bind(string.Format("inproc://wsrouter-{0}", id));

            m_messagesPipe.ReceiveReady += OnMessagePipeReceiveReady;

            m_actor = new Actor<int>(context, shimHandler, id);

            m_messagesPipe.WaitForSignal();
        }

        private void OnMessagePipeReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            var temp = ReceiveReady;
            if (temp != null)
            {
                temp(this, new WSSocketEventArgs(this));
            }
        }

        public EventHandler<WSSocketEventArgs> ReceiveReady;        

        NetMQSocket ISocketPollable.Socket
        {
            get { return m_messagesPipe; }
        }

        public void Bind(string address)
        {
            m_actor.SendMore(BindCommand).Send(address);

            byte[] bytes = m_actor.Receive();
            int errorCode = BitConverter.ToInt32(bytes, 0);

            if (errorCode != 0)
            {
                throw NetMQException.Create((ErrorCode)errorCode);
            }
        }

        public void Send(ref Msg msg, SendReceiveOptions options)
        {
            m_messagesPipe.Send(ref msg, options);
        }

        public void Receive(ref Msg msg, SendReceiveOptions options)
        {
            m_messagesPipe.Receive(ref msg, options);
        }

        public void Dispose()
        {
            m_actor.Dispose();

            m_messagesPipe.Options.Linger = TimeSpan.Zero;
            m_messagesPipe.Dispose();
        }
    }
}
