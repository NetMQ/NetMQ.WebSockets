using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.zmq;
using NUnit.Framework;
using WebSocket4Net;

namespace NetMQ.WebSockets.Tests
{
    [TestFixture]
    public class WSRouterTests
    {
        [Test]
        public void PingPongTest()
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                bool messageArrived;

                Task task = Task.Factory.StartNew(() =>
                {
                    using (WSRouter router = context.CreateWSRouter())
                    {
                        router.Bind("ws://localhost:82");
                        router.Receive();
                    }
                });

                WebSocket4Net.WebSocket webSocket = new WebSocket("ws://localhost:82", "WSNetMQ");
                webSocket.EnableAutoSendPing = true;
                webSocket.AutoSendPingInterval = 1; // one second
                
                
                ManualResetEvent manualResetEvent = new ManualResetEvent(false);
                webSocket.Opened += (sender, args) => manualResetEvent.Set();
                
                webSocket.Open();
                webSocket.Error += (sender, args) => Console.WriteLine("Error");
                manualResetEvent.WaitOne();

                // ping was supposed to be send
                Thread.Sleep(5000);

                Assert.AreEqual(webSocket.State, WebSocketState.Open);

                // should exit the router thread
                webSocket.Send("0Hello");

                task.Wait();
            }

        }
    }
}
