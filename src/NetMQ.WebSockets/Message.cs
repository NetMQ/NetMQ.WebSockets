using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ.zmq;

namespace NetMQ.WebSockets
{
  public class Message
  {
    public Message(byte[] source, byte[] data)
    {
      Source = source;
      Data = data;
    }

    public byte[] Source { get; private set; } 
    public byte[] Data { get; private set; }
  }
}
