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
      NetMQContext context = NetMQContext.Create();
      WSRouter webSocketPublisher = new WSRouter(context);
      webSocketPublisher.AutoSetRecipientAsSender = true;
      webSocketPublisher.Bind(string.Format("tcp://localhost:{0}", args[0]));

      while (true)
      {
        string message = webSocketPublisher.Receive();
        Console.WriteLine(message);

        //Console.ReadLine();
        
        webSocketPublisher.Send("Hello back");                
      }
            
      Console.ReadLine();
    }
  }
}
