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
            using (WSRouter router = new WSRouter())
            using (WSPublisher publisher = new WSPublisher())
            {
                router.Bind("ws://localhost:80");
                publisher.Bind("ws://localhost:81");

                router.ReceiveReady += (sender, eventArgs) =>
                {
                    byte[] identity = router.ReceiveFrameBytes();
                    string message = router.ReceiveFrameString();

                    router.SendMoreFrame(identity);
                    router.SendFrame("OK");

                    publisher.SendMoreFrame("chat");
                    publisher.SendFrame(message);
                };
                        
                NetMQPoller poller = new NetMQPoller();
                poller.Add(router);

                // we must add the publisher to the poller although we are not registering to any event.
                // The internal stream socket handle connections and subscriptions and use the events internally
                poller.Add(publisher);
                poller.Run();

            }
        }
    }
}
