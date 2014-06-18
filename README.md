NetMQ.WebSockets
====

NetMQ WebSockets is an extension to NetMQ, implemeted using Stream socket type and give NetMQ like interface.

NetMQ and ZeroMQ doesn't support pluggable transport, so the library provide its own socket object which is very similar to NetMQ socket.

Hopefully in the near future this will be integrated into NetMQ as another transport.

Currently only the router pattern is implemented and only bind is supported.

So who can talk to this extension if we can only bind? [JSMQ](https://github.com/somdoron/JSMQ) is ZeroMQ/NetMQ client in javascript which can talk to this extension over WebSockets.

To use NetMQ.WebSockets you need to download the source and compile, I promise to upload the library to nuget soon.

This is very early beta and not ready for production use, but please use and give feedback, or even better send a pull request.

NetMQ.WebSockets example:

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

and the javascript example, you can dowload the JSMQ.JS file from [JSMQ github page](https://github.com/somdoron/JSMQ).

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


