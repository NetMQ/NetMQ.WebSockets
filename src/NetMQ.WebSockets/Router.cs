using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ.zmq;

namespace NetMQ.WebSockets
{
  public class WSRouter : WSSocketBase
  {
    private int m_clientCounter;

    public WSRouter(NetMQContext context) : base(context)
    {

    }

    public byte[] Recipient { get; set; }

    public byte[] Sender { get; set; }

    public bool AutoSetRecipientAsSender { get; set; }

    protected internal override void XSend(byte[] message, bool dontWait)
    {
      if (Recipient == null)
      {
        // no recipient for message
        throw NetMQException.Create(ErrorCode.EHOSTUNREACH);
      }

      WriteMessage(Recipient, message, dontWait);
    }

    protected internal override byte[] XReceive()
    {
      byte[] identity;
      byte[] data;

      var isMessageRead = ReadMessage(out identity, out data);

      if (isMessageRead)
      {
        Sender = identity;

        if (AutoSetRecipientAsSender)
        {
          Recipient = Sender;
        }

        return data;
      }

      Sender = null;

      return null;      
    }

    protected internal override bool XHasIn()
    {
      return !IsMessageQueueEmpty;
    }

    protected internal override bool XHasOut()
    {
      return m_clientCounter > 0;
    }

    protected internal override void AttachClient(zmq.Blob identity)
    {
      m_clientCounter++;
    }

    protected internal override void ClientTerminated(zmq.Blob identity)
    {
      m_clientCounter--;
    }
  }
}
