NetMQ.WebSockets
====

NetMQ WebSockets is an extension to NetMQ, implemented using Stream socket type and providing a NetMQ like interface.

NetMQ and ZeroMQ don't support pluggable transport, therefore the library provides its own socket object which is very similar to the NetMQ socket object.

Hopefully in the near future the library will be integrated into NetMQ as another transport.

Currently only the router and publisher patterns are implemented and you can only bind the socket.

You are probably asking yourselves, "If I can only bind the socket, then how can one connect to the socket?"
That's where [JSMQ](https://github.com/zeromq/JSMQ) comes into play. JSMQ is ZeroMQ/NetMQ javascript client which connect and talk to the NetMQ.WebSockets, over WebSockets off course.

To install NetMQ.WebSockets, search for it on [nuget](https://www.nuget.org/packages/NetMQ.WebSockets/) and make sure to choose "Include Prerelease".


To install JSMQ you can dowload the JSMQ.JS file from [JSMQ github page](https://github.com/zeromq/JSMQ) or from [nuget](https://www.nuget.org/packages/JSMQ/) as well, just search JSMQ.

This is early beta and not ready for production use, but don't let that stop you from trying it out, giving feedback, or even better sending a pull request.

Without further adieu, following is a small chat example:

```csharp
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
```

For JSMQ example please visit the [JSMQ github page](https://github.com/somdoron/JSMQ).


