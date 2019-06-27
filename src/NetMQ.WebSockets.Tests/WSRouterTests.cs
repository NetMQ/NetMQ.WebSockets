using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using WebSocket4Net;

namespace NetMQ.WebSockets.Tests
{
    [TestFixture]
    public class WSRouterTests
    {
        [Test]
        public void PingPong()
        {
            using (WebSocket4Net.WebSocket webSocket = new WebSocket("ws://localhost:82", "WSNetMQ"))
            {
                webSocket.EnableAutoSendPing = true;
                webSocket.AutoSendPingInterval = 1; // one second

                using (WSRouter router = new WSRouter())
                {
                    router.Bind("ws://localhost:82");

                    ManualResetEvent manualResetEvent = new ManualResetEvent(false);
                    webSocket.Opened += (sender, args) => manualResetEvent.Set();

                    webSocket.Open();
                    webSocket.Error += (sender, args) => Console.WriteLine("Error");
                    manualResetEvent.WaitOne();

                    Thread.Sleep(5000);

                    Assert.AreEqual(webSocket.State, WebSocketState.Open);
                }
            }
        }

        [Test]
        public void RequestReply()
        {
            using (WebSocket4Net.WebSocket webSocket = new WebSocket("ws://localhost:82", "WSNetMQ"))
            {

                using (WSRouter router = new WSRouter())
                {
                    router.Bind("ws://localhost:82");

                    ManualResetEvent manualResetEvent = new ManualResetEvent(false);
                    webSocket.Opened += (sender, args) => manualResetEvent.Set();

                    webSocket.Open();
                    webSocket.Error += (sender, args) => Console.WriteLine("Error");
                    manualResetEvent.WaitOne();

                    Assert.AreEqual(webSocket.State, WebSocketState.Open);

                    byte[] message = new byte[2];
                    message[0] = 0;
                    message[1] = (byte)'H';

                    // should exit the router thread
                    webSocket.Send(message, 0, message.Length);

                    byte[] identity = router.ReceiveFrameBytes();
                    string msg = router.ReceiveFrameString();

                    Assert.AreEqual("H", msg);

                    byte[] receivedMessage = null;
                    manualResetEvent.Reset();

                    webSocket.DataReceived += (sender, args) =>
                    {
                        receivedMessage = args.Data;
                        manualResetEvent.Set();
                    };

                    router.SendMoreFrame(identity);
                    router.SendFrame("W");

                    Assert.IsTrue(manualResetEvent.WaitOne(1000));

                    Assert.AreEqual(0, receivedMessage[0]);
                    Assert.AreEqual('W', receivedMessage[1]);
                }
            }
        }
    }
}
