NetMQ.WebSockets
====

NetMQ WebSockets is an extension to NetMQ, implemented using Stream socket type and providing a NetMQ like interface.

NetMQ and ZeroMQ don't support pluggable transport, therefore the library provides its own socket object which is very similar to the NetMQ socket object.

Hopefully in the near future the library will be integrated into NetMQ as another transport.

Currently only the router and publisher patterns are implemented and you can only bind the socket.

You are probably asking yourselves, "If I can only bind the socket, then how can one connect to the socket?"
That's where [JSMQ](https://github.com/somdoron/JSMQ) comes into play. JSMQ is ZeroMQ/NetMQ javascript client which connect and talk to the NetMQ.WebSockets, over WebSockets off course.

To install NetMQ.WebSockets, search for it on [nuget](https://www.nuget.org/packages/NetMQ.WebSockets/) and make sure to choose "Include Prerelease".


To install JSMQ you can dowload the JSMQ.JS file from [JSMQ github page](https://github.com/somdoron/JSMQ) or from [nuget](https://www.nuget.org/packages/JSMQ/) as well, just search JSMQ.

This is early beta and not ready for production use, but don't let that stop you from trying it out, giving feedback, or even better sending a pull request.

Without further adieu:

```csharp
using (NetMQContext context = NetMQContext.Create())
{
    using (WSRouter router = context.CreateWSRouter())
    using (WSPublisher publisher = context.CreateWSPublisher())
    {
        router.Bind("ws://localhost:80");                    
        publisher.Bind("ws://localhost:81");

        router.ReceiveReady += (sender, eventArgs) =>
        {
            string identity = eventArgs.WSSocket.Receive();
            string message = eventArgs.WSSocket.Receive();

            eventArgs.WSSocket.SendMore(identity).Send("OK");

            eventArgs.WSSocket.SendMore("chat").Send(message);
        };
            
        Poller poller = new Poller();
        poller.AddWSSocket(router);

        // we must add the publisher to the poller although we are not registering to any event.
        // the protocol processing is happening in the user thread, without adding the publisher to the poller
        // the next time the publisher will accept a socket or receive a subscription is only when send is called.
        // when socket is added to the poller the processing is happen everytime data is ready to be processed
        poller.AddWSSocket(publisher);
        poller.Start();

    }
}
```

For JSMQ example please visit the [JSMQ github page](https://github.com/somdoron/JSMQ).


