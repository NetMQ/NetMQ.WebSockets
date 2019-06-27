using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Sockets;

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
        
        internal const string BindCommand = "BIND";

        private NetMQActor m_actor;
        private PairSocket m_messagesPipe;        

        protected WSSocket(Func<int, IShimHandler> shimCreator)
        {
            int id = Interlocked.Increment(ref s_id);            

            m_messagesPipe = new PairSocket();
            m_messagesPipe.Bind(string.Format("inproc://wsrouter-{0}", id));

            m_messagesPipe.ReceiveReady += OnMessagePipeReceiveReady;

            m_actor = NetMQActor.Create(shimCreator(id));

            m_messagesPipe.ReceiveSignal();
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
            m_actor.SendMoreFrame(BindCommand).SendFrame(address);

            byte[] bytes = m_actor.ReceiveFrameBytes();
            int errorCode = BitConverter.ToInt32(bytes, 0);

            if (errorCode != 0)
            {
                throw NetMQException.Create((ErrorCode)errorCode);
            }
        }

        public string ReceiveFrameString()
        {
            return m_messagesPipe.ReceiveFrameString();
        }

        public byte[] ReceiveFrameBytes()
        {
            return m_messagesPipe.ReceiveFrameBytes();
        }

        public bool TryReceive(ref Msg msg, TimeSpan timeout)
        {
            return m_messagesPipe.TryReceive(ref msg, timeout);
        }

        public void SendFrame(byte[] frame, bool more)
        {
            m_messagesPipe.SendFrame(frame, more);
        }

        public void SendMoreFrame(byte[] frame)
        {
            m_messagesPipe.SendMoreFrame(frame);
        }

        public bool TrySend(ref Msg msg, TimeSpan timeout, bool more)
        {
            return m_messagesPipe.TrySend(ref msg, timeout, more);
        }                

        public void Dispose()
        {
            m_actor.Dispose();

            m_messagesPipe.Options.Linger = TimeSpan.Zero;
            m_messagesPipe.Dispose();
        }
    }
}
