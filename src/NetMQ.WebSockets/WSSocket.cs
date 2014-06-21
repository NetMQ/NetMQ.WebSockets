using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ.zmq;

namespace NetMQ.WebSockets
{
    public enum WriteResult
    {
        OK, Again, HostUnreachable
    }

    public class WSSocketEventArgs : EventArgs
    {
        public WSSocketEventArgs(WSSocket wsSocket)
        {
            WSSocket = wsSocket;
        }


        public WSSocket WSSocket { get; private set; }
    }

    public abstract class WSSocket : IDisposable
    {
        private readonly NetMQContext m_context;
        private Dictionary<Blob, WebSocketClient> m_clients;
        private Queue<Message> m_incomingMessagesQueue;
        private Queue<Blob> m_clientTerminated;

        private NetMQSocket m_streamSocket;        

        public WSSocket(NetMQContext context)
        {
            m_context = context;
            m_clients = new Dictionary<Blob, WebSocketClient>();
            m_clientTerminated = new Queue<Blob>();
            m_incomingMessagesQueue = new Queue<Message>();

            m_streamSocket = m_context.CreateStreamSocket();
            m_streamSocket.ReceiveReady += OnReceiveReady;
            m_streamSocket.SendReady += OnSendReady;
        }        

        protected internal abstract void XSend(byte[] message, bool dontWait, bool more);
        protected internal abstract bool XReceive(out byte[] message, out bool more);

        protected internal abstract bool XHasIn();
        protected internal abstract bool XHasOut();

        protected internal abstract void AttachClient(Blob identity);
        protected internal abstract void ClientTerminated(Blob identity);

        internal NetMQSocket Socket
        {
            get
            {
                return m_streamSocket;
            }
        }

        protected bool IsMessageQueueEmpty
        {
            get
            {
                return m_incomingMessagesQueue.Count == 0;
            }
        }

        public event EventHandler<WSSocketEventArgs> ReceiveReady;
        public event EventHandler<WSSocketEventArgs> SendReady;

        public bool More { get; private set; }

        protected virtual void Process(bool untilMessageAvailable)
        {
            try
            {
                // when client get terminated during send operation the terimated method will get called when processed next
                while (m_clientTerminated.Count != 0)
                {
                    var clientIdentity = m_clientTerminated.Dequeue();

                    WebSocketClient client;

                    if (m_clients.TryGetValue(clientIdentity, out client))
                    {
                        client.Dispose();

                        m_clients.Remove(clientIdentity);
                        ClientTerminated(clientIdentity);
                    }
                }

                // process messages until again exception arrived
                // TODO: expose the events options from netmq options to avoid the exception
                while (true)
                {
                    bool more;

                    // we only wait if we asked to process until message available and message still not available
                    bool shouldWait = untilMessageAvailable && m_incomingMessagesQueue.Count == 0;

                    Blob identity =
                        new Blob(m_streamSocket.Receive(shouldWait ? SendReceiveOptions.None : SendReceiveOptions.DontWait));

                    WebSocketClient client;
                    if (!m_clients.TryGetValue(identity, out client))
                    {
                        client = new WebSocketClient(m_streamSocket, m_incomingMessagesQueue, identity);
                        m_clients.Add(identity, client);
                    }

                    var stateBefore = client.State;

                    // tell the client data is waiting to be processed
                    client.OnDataReady();

                    if (client.State == WebSocketClientState.Ready && stateBefore != client.State)
                    {
                        AttachClient(client.Identity);
                    }
                    else if (client.State == WebSocketClientState.Closed)
                    {
                        m_clients.Remove(client.Identity);

                        if (stateBefore == WebSocketClientState.Ready)
                        {
                            ClientTerminated(client.Identity);
                        }

                        client.Dispose();
                    }
                }
            }
            catch (AgainException ex)
            {
                // just ignore
            }
        }

        public void Bind(string address)
        {         
            if (!address.StartsWith("ws://"))
                throw new NotSupportedException("only WS scheme is supported");

            m_streamSocket.Bind(address.Replace("ws://", "tcp://"));

            Process(false);
        }

        protected virtual byte[] StringToBytes(string data)
        {
            return Encoding.UTF8.GetBytes(data);
        }

        protected virtual string BytesToString(byte[] data)
        {
            return Encoding.UTF8.GetString(data);
        }

        public WSSocket SendMore(string message, bool dontWait = false)
        {
            Send(message, dontWait, true);
            return this;
        }

        public void Send(string message, bool dontWait = false, bool more = false)
        {
            Process(false);

            byte[] messageBytes = StringToBytes(message);

            XSend(messageBytes, dontWait, more);
        }

        public string Receive(bool dontWait = false)
        {
            bool waitUntilMessage = !dontWait;

            if (XHasIn())
            {
                waitUntilMessage = false;
            }

            Process(waitUntilMessage);

            byte[] messageBytes;
            bool more;

            if (XReceive(out messageBytes, out more))
            {
                More = more;
                return BytesToString(messageBytes);
            }
            else
            {
                More = false;
                throw AgainException.Create();
            }
        }

        public bool HasIn()
        {
            Process(false);

            return XHasIn();
        }

        public bool HasOut()
        {
            Process(false);

            return XHasOut();
        }

        protected WriteResult WriteMessage(byte[] identity, byte[] message, bool dontWait, bool more)
        {
            WebSocketClient client;

            if (m_clients.TryGetValue(new Blob(identity), out client))
            {
                // if client is not active we don't try to send the message
                if (client.State == WebSocketClientState.Ready)
                {
                    try
                    {
                        bool isMessageSent = client.Send(message, dontWait, more);

                        if (client.State == WebSocketClientState.Closed)
                        {
                            m_clientTerminated.Enqueue(client.Identity);
                        }

                        return isMessageSent ? WriteResult.OK : WriteResult.Again;
                    }
                    catch (NetMQException ex)
                    {
                        return WriteResult.HostUnreachable;
                    }
                }
            }

            return WriteResult.HostUnreachable;
        }

        protected bool ReadMessage(out byte[] identity, out byte[] data, out bool more)
        {
            if (m_incomingMessagesQueue.Count > 0)
            {
                Message message = m_incomingMessagesQueue.Dequeue();

                identity = message.Source;
                data = message.Data;
                more = message.More;

                return true;
            }
            else
            {
                identity = null;
                data = null;
                more = false;

                return false;
            }
        }

        private void OnReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            if (HasIn())
            {
                if (ReceiveReady != null)
                {
                    ReceiveReady(this, new WSSocketEventArgs(this));
                }    
            }            
        }

        private void OnSendReady(object sender, NetMQSocketEventArgs e)
        {
            if (HasOut())
            {
                if (SendReady != null)
                {
                    SendReady(this, new WSSocketEventArgs(this));
                }
            }      
        }

        public virtual void Dispose()
        {
            Process(false);

            foreach (var webSocketClient in m_clients.Values)
            {
                webSocketClient.Close();
                webSocketClient.Dispose();
            }

            m_streamSocket.ReceiveReady -= OnReceiveReady;
            m_streamSocket.SendReady -= OnSendReady;
            m_streamSocket.Dispose();
        }
    }
}
