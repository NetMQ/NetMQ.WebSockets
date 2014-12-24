using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using WebSocket4Net;

namespace NetMQ.WebSockets.Tests
{
    [TestFixture]
    public class WSPublisherTests
    {
        [Test]
        public void PubSub()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (WebSocket4Net.WebSocket webSocket = new WebSocket("ws://localhost:82", "WSNetMQ"))
                {
                    using (WSPublisher publisher = context.CreateWSPublisher())
                    {
                        publisher.Bind("ws://localhost:82");

                        ManualResetEvent manualResetEvent = new ManualResetEvent(false);
                        webSocket.Opened += (sender, args) => manualResetEvent.Set();

                        webSocket.Open();
                        webSocket.Error += (sender, args) => Console.WriteLine("Error");
                        manualResetEvent.WaitOne();

                        Assert.AreEqual(webSocket.State, WebSocketState.Open);

                        byte[] subscription = new byte[3];
                        subscription[0] = 0;
                        subscription[1] = 1;
                        subscription[2] = (byte)'H';

                        // should exit the router thread
                        webSocket.Send(subscription, 0, subscription.Length);

                        // wait for the subscription to arrive
                        Thread.Sleep(1000);

                        byte[] receivedMessage = null;
                        manualResetEvent.Reset();

                        webSocket.DataReceived += (sender, args) =>
                        {
                            receivedMessage = args.Data;
                            manualResetEvent.Set();
                        };

                        publisher.Send("Hello");

                        Assert.IsTrue(manualResetEvent.WaitOne(1000));

                        Assert.AreEqual(0, receivedMessage[0]);
                        Assert.AreEqual('H', receivedMessage[1]);
                        Assert.AreEqual('e', receivedMessage[2]);
                        Assert.AreEqual('l', receivedMessage[3]);
                        Assert.AreEqual('l', receivedMessage[4]);
                        Assert.AreEqual('o', receivedMessage[5]);
                    }
                }
            }
        }
    }
}


