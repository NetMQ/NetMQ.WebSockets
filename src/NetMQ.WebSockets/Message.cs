using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMQ.WebSockets
{
  public class Message
  {
    public Message(byte[] source, byte[] data, bool more)
    {
      Source = source;
      Data = data;
        More = more;
    }

    public byte[] Source { get; private set; } 
    public byte[] Data { get; private set; }
      public bool More { get; private set; }
  }
}
