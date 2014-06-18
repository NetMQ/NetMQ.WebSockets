NetMQ.WebSockets
====

NetMQ WebSockets is an extension to NetMQ, implemented using Stream socket type and providing a NetMQ like interface.

NetMQ and ZeroMQ don't support pluggable transport, therefore the library provides its own socket object which is very similar to the NetMQ socket object.

Hopefully in the near future the library will be integrated into NetMQ as another transport.

Currently only the router pattern is implemented and you can only bind the socket.

You are probably asking yourselves, "If I can only bind the socket, then how can one connect to the socket?"
That's where [JSMQ](https://github.com/somdoron/JSMQ) comes into play. JSMQ is ZeroMQ/NetMQ javascript client which connect and talk to the NetMQ.WebSockets over WebSockets off course.

To install NetMQ.WebSockets, search for it on [nuget](https://www.nuget.org/packages/NetMQ.WebSockets/) and make sure to choose "Include Prerelease".


To install JSMQ you can dowload the JSMQ.JS file from [JSMQ github page](https://github.com/somdoron/JSMQ) or from [nuget](https://www.nuget.org/packages/JSMQ/) as well, just search JSMQ.

This is very early beta and not ready for production use, but don't let that stop you from trying it out, giving feedback, or even better sending a pull request.

Without further adieu:

```csharp
      using (NetMQContext context = NetMQContext.Create())
      {
        using (WSRouter socket = new WSRouter(context))
        {          
          socket.Bind("tcp://localhost:80");

          while (true)
          {
            // in difference from NetMQ/ZeroMQ NetMQ.WebSockets doesn't support multipart messages, 
            // so the first frame is not the sender
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

```html
<html>
	<script src="JSMQ.js" />	
	<script>
		var dealer = new Dealer();		
		
		// send ready will be raised when at least when socket got connected, 
		// if you try to send a message before the event the message will be dropped
		// the event will raised again only if the send method return false.		
		dealer.sendReady = function(socket)
			{ 				
				alert("ready to send");
			};
		
		// receive ready will be raised when there is message ready to be received, 
		// trying to receive message before this event will get you a null message
		// the event will be raised again only if you received null message, 
		// therefore every time the event is triggered you must conumse all the messages
		dealer.receiveReady = function(socket)
		{		
			var message = dealer.receive();
		
			while(message!=null)
			{				
				alert(message);
				message = dealer.receive();
			}
		};
		
		dealer.connect("ws://localhost:80");					
		
		function send()
		{
			// send a message to the zeromq server
			dealer.send("hello");
		}		
	</script>
	
	<body>
		<button  onclick="javascript: send()" >Send</button>			
	</body>
</html>
```


