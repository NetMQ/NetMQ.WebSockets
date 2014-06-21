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
                using (WSRouter router = context.CreateWSRouter())
                using (WSPublisher publisher = context.CreateWSPublisher())
                {
                    router.Bind("ws://localhost:80");                    
                    publisher.Bind("ws://localhost:81");

                    router.ReceiveReady += (sender, eventArgs) =>
                    {
                        string identity = router.Receive();
                        string message = router.Receive();

                        router.SendMore(identity).Send("OK");

                        publisher.SendMore("chat").Send(message);
                    };
                        
                    Poller poller = new Poller();
                    poller.AddWSSocket(router);

                    // we must add the publisher to the poller although we are not registering to any event.
                    // The internal stream socket handle connections and subscriptions and use the events internally
                    poller.AddWSSocket(publisher);
                    poller.Start();

                }
            }
        }
    }
}
