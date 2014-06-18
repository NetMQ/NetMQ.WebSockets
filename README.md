NetMQ.WebSockets
====

NetMQ WebSockets is an extension to NetMQ, implemeted using Stream pattern and give NetMQ like interface.

NetMQ and ZeroMQ doesn't support pluggable transport, so the library provide it's own socket object which is very simalar to NetMQ socket.

Hopefully in the near future this will be integrated into NetMQ as a transport.

Currently only the router pattern is implemented and only bind is supported.

So who can talk to this extension if we can only bind? [JSMQ](https://github.com/somdoron/JSMQ) is ZeroMQ/NetMQ client in javascript which can talk to this extension over WebSockets.

To use NetMQ.WebSockets you need to download the source and compile, I promise to upload the library to nuget soon.

NetMQ.WebSockets example:

```csharp
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

```



