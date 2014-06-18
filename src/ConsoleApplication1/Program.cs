using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.WebSockets;

namespace ConsoleApplication1
{
  class Program
  {
    static void Main(string[] args)
    {
      using (NetMQContext context = NetMQContext.Create())
      {
        using (WSRouter socket = new WSRouter(context))
        {          
          socket.Bind("tcp://localhost:80");

          while (true)
          {
            // in difference from NetMQ/ZeroMQ NetMQ.WebSockets doesn't support multipart messages, so the first frame is not the sender
            // to retrieve the sender access the Sender property on the socket object
            string message = socket.Receive();
            Console.WriteLine(message);
            
            // because multipart messages not supported, we have to set the recipient address
            socket.Recipient = socket.Sender;
            socket.Send("Response");
          }

          Console.ReadLine();
        }
      }
    }
  }
}
