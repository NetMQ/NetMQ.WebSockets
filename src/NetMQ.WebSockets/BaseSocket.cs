using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ.zmq;

namespace NetMQ.WebSockets
{
  public abstract class WSSocketBase : IDisposable
  {
    private readonly NetMQContext m_context;
    private Dictionary<Blob, WebSocketClient> m_clients;    
    private Queue<Message> m_incomingMessagesQueue;
    private Queue<Blob> m_clientTerminated; 

    private NetMQSocket m_streamSocket;        

    public WSSocketBase(NetMQContext context)
    {
      m_context = context;
      m_clients = new Dictionary<Blob, WebSocketClient>();
      m_clientTerminated = new Queue<Blob>();
      m_incomingMessagesQueue = new Queue<Message>();

      m_streamSocket = m_context.CreateStreamSocket();
    }   

    protected internal abstract void XSend(byte[] message, bool dontWait);
    protected internal abstract byte[] XReceive();

    protected internal abstract bool XHasIn();
    protected internal abstract bool XHasOut();

    protected internal abstract void AttachClient(Blob identity);
    protected internal abstract void ClientTerminated(Blob identity);

    /// <summary>
    /// Socket exposed for polling.
    /// Must call HasIn or HasOut before assuming messages are ready
    /// </summary>
    public NetMQSocket Socket
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
    
    private void Process(bool untilMessageAvailable)
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
          bool shouldWait = untilMessageAvailable && m_incomingMessagesQueue.Count==0;

          Blob identity = new Blob(m_streamSocket.Receive(shouldWait ? SendReceiveOptions.None : SendReceiveOptions.DontWait));
          
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
      m_streamSocket.Bind(address);
    }

    public void Send(string message, bool dontWait = false)
    {
      Process(false);

      byte[] messageBytes = Encoding.UTF8.GetBytes(message);

      XSend(messageBytes, dontWait);
    }

    public string Receive(bool dontWait = false)
    {
      Process(!dontWait);

      byte[] messageBytes = XReceive();

      if (messageBytes != null)
      {
        return Encoding.UTF8.GetString(messageBytes);  
      }
      else
      {
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

    protected bool WriteMessage(byte[] identity, byte[] message, bool dontWait)
    {
      WebSocketClient client;

      if (m_clients.TryGetValue(new Blob(identity), out client))
      {
        // if client is not active we don't try to send the message
        if (client.State == WebSocketClientState.Ready)
        {
          bool isMessageSent = client.Send(message, dontWait);

          if (client.State == WebSocketClientState.Closed)
          {
            m_clientTerminated.Enqueue(client.Identity);
          }

          return isMessageSent;
        }
      }

      // the client doesn't exist or not ready to receive messages
      throw NetMQException.Create(ErrorCode.EHOSTUNREACH);
    }
    
    protected bool ReadMessage(out byte[] identity, out byte[] data)
    {
      if (m_incomingMessagesQueue.Count > 0)
      {
        Message message = m_incomingMessagesQueue.Dequeue();

        identity = message.Source;
        data = message.Data;

        return true;
      }
      else
      {
        identity = null;
        data = null;

        return false;
      }                 
    }
   
    public virtual  void Dispose()
    {
      Process(false);

      foreach (var webSocketClient in m_clients.Values)
      {
        webSocketClient.Close();
        webSocketClient.Dispose();
      }

      m_streamSocket.Dispose();
    }
  }
}
