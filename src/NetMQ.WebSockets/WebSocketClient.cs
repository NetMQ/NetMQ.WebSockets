using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NetMQ.Sockets;
using NetMQ.zmq;

namespace NetMQ.WebSockets
{
    enum WebSocketClientState
    {
        Closed, Handshake, Ready
    }

    class NetMQMessageEventArgs : EventArgs
    {
        public NetMQMessageEventArgs(byte[] identity, NetMQMessage message)
        {
            Identity = identity;
            Message = message;
        }

        public byte[] Identity { get; private set; }
        public NetMQMessage Message { get; private set; }
    }

    class WebSocketClient : IDisposable
    {
        private WebSocketClientState m_state;
        private const string MagicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private Decoder m_decoder;
        private readonly NetMQSocket m_streamSocket;

        private NetMQMessage m_outgoingMessage;

        internal WebSocketClient(NetMQSocket streamSocket, byte[] identity)
        {
            m_state = WebSocketClientState.Closed;
            m_streamSocket = streamSocket;
            m_outgoingMessage = null;

            Identity = identity;
        }

        public byte[] Identity { get; private set; }

        public WebSocketClientState State
        {
            get { return m_state; }
        }

        public event EventHandler<NetMQMessageEventArgs> IncomingMessage;

        public void OnDataReady()
        {
            switch (m_state)
            {
                case WebSocketClientState.Closed:
                    m_state = WebSocketClientState.Handshake;
                    string clientHandshake = m_streamSocket.ReceiveString();

                    string[] lines = clientHandshake.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    string key;

                    if (ValidateClientHandshake(lines, out key))
                    {
                        string acceptKey = GenerateAcceptKey(key);

                        try
                        {
                            m_streamSocket.SendMore(Identity, Identity.Length, true);
                            m_streamSocket.Send("HTTP/1.1 101 Switching Protocols\r\n" +
                                                "Upgrade: websocket\r\n" +
                                                "Connection: Upgrade\r\n" +
                                                "Sec-WebSocket-Accept: " + acceptKey + "\r\n" +
                                                "Sec-WebSocket-Protocol: WSNetMQ\r\n\r\n");

                            m_decoder = new Decoder();
                            m_decoder.Message += OnMessage;
                            m_state = WebSocketClientState.Ready;
                        }
                        catch (NetMQException)
                        {
                            m_state = WebSocketClientState.Closed;
                        }
                    }
                    else
                    {
                        m_state = WebSocketClientState.Closed;

                        try
                        {
                            m_streamSocket.SendMore(Identity, Identity.Length, true);
                            m_streamSocket.Send("HTTP/1.1 400 Bad Request\r\nSec-WebSocket-Version: 13\r\n");

                            // invalid request, close the socket and raise closed event
                            m_streamSocket.SendMore(Identity, Identity.Length, true);
                            m_streamSocket.Send("");
                        }
                        catch (NetMQException ex)
                        {

                        }
                    }

                    break;
                case WebSocketClientState.Ready:
                    byte[] message = m_streamSocket.Receive();
                    m_decoder.Process(message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Opcode == OpcodeEnum.Close)
            {
                // send close command to the socket
                try
                {
                    m_streamSocket.SendMore(Identity, Identity.Length, true);
                    m_streamSocket.Send("");
                }
                catch (NetMQException)
                {
                }

                m_state = WebSocketClientState.Closed;
            }
            else if (e.Opcode == OpcodeEnum.Binary)
            {
                if (m_outgoingMessage == null)
                {
                    m_outgoingMessage = new NetMQMessage();
                }

                m_outgoingMessage.Append(e.Payload);

                if (!e.More)
                {
                    if (IncomingMessage != null)
                    {
                        IncomingMessage(this, new NetMQMessageEventArgs(Identity, m_outgoingMessage));
                    }
                    m_outgoingMessage = null;
                }
            }
            else if (e.Opcode == OpcodeEnum.Ping)
            {
                byte[] pong = new byte[2 + e.Payload.Length];
                pong[0] = 0x8A; // Pong and Final
                pong[1] = (byte)(e.Payload.Length & 127);
                Buffer.BlockCopy(e.Payload, 0, pong, 2, e.Payload.Length);

                m_streamSocket.SendMore(Identity, Identity.Length, true);
                m_streamSocket.Send(pong);
            }
        }

        bool ValidateClientHandshake(string[] lines, out string key)
        {
            key = null;

            // first line should be the GET
            if (lines.Length == 0 || !lines[0].StartsWith("GET"))
                return false;

            if (!lines.Any(l => l.StartsWith("Host:")))
                return false;

            // look for upgrade command
            if (!lines.Any(l => l.Trim().Equals("Upgrade: websocket", StringComparison.OrdinalIgnoreCase)))
                return false;

            if (!lines.Any(l => { var lt = l.Trim(); return lt.StartsWith("Connection: ", StringComparison.OrdinalIgnoreCase) && lt.Split(new char[]{',', ':'}).Any(p => p.Trim().Equals("Upgrade", StringComparison.OrdinalIgnoreCase)); }))
                return false;

            if (!lines.Any(l => l.Trim().Equals("Sec-WebSocket-Version: 13", StringComparison.OrdinalIgnoreCase)))
                return false;

            // look for websocket key
            string keyLine = lines.FirstOrDefault(l => l.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(keyLine))
                return false;

            key = keyLine.Substring(keyLine.IndexOf(':') + 1).Trim();

            return true;
        }

        string GenerateAcceptKey(string requestKey)
        {
            string data = requestKey + MagicString;

            using (SHA1Managed sha1Managed = new SHA1Managed())
            {
                byte[] hash = sha1Managed.ComputeHash(Encoding.ASCII.GetBytes(data));

                return Convert.ToBase64String(hash);
            }
        }

        public bool Send(byte[] message, bool dontWait, bool more)
        {
            int frameSize = 2 + 1 + message.Length;
            int payloadStartIndex = 2;
            int payloadLength = message.Length + 1;

            if (payloadLength > 125)
            {
                frameSize += 2;
                payloadStartIndex += 2;

                if (payloadLength > ushort.MaxValue)
                {
                    frameSize += 6;
                    payloadStartIndex += 6;
                }
            }

            byte[] frame = new byte[frameSize];

            frame[0] = (byte)0x81; // Text and Final      

            // No mask
            frame[1] = 0x00;

            if (payloadLength <= 125)
            {
                frame[1] |= (byte)(payloadLength & 127);
            }
            else
            {
                // TODO: implement
            }

            // more byte
            frame[payloadStartIndex] = (byte)(more ? '1' : '0');
            payloadStartIndex++;

            // payload
            Buffer.BlockCopy(message, 0, frame, payloadStartIndex, message.Length);

            try
            {
                m_streamSocket.SendMore(Identity, Identity.Length, dontWait);
                m_streamSocket.Send(frame, frame.Length, dontWait);

                return true;
            }
            catch (AgainException againException)
            {
                return false;
            }
            catch (NetMQException exception)
            {
                m_state = WebSocketClientState.Closed;
                throw exception;
            }
        }

        public void Close()
        {
            // TODO: send close message     
            m_streamSocket.Send(Identity, Identity.Length, true, true);
            m_streamSocket.Send("");

            m_state = WebSocketClientState.Closed;
        }

        public void Dispose()
        {
            m_decoder.Message -= OnMessage;
        }
    }
}
