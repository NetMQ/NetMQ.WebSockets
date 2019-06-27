﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.Sockets;

namespace NetMQ.WebSockets
{
    abstract class BaseShimHandler : IShimHandler
    {
        private int m_id;

        private PairSocket m_messagesPipe;
        private StreamSocket m_stream;

        private NetMQPoller m_poller;

        private Dictionary<byte[], WebSocketClient> m_clients;

        public BaseShimHandler(int id)
        {                                    
            m_id = id;

            m_clients = new Dictionary<byte[], WebSocketClient>(new ByteArrayEqualityComparer());
        }        

        protected abstract void OnOutgoingMessage(NetMQMessage message);
        protected abstract void OnIncomingMessage(byte[] identity, NetMQMessage message);

        protected abstract void OnNewClient(byte[] identity);
        protected abstract void OnClientRemoved(byte[] identity);

        protected void WriteOutgoing(byte[] identity, byte[] message, bool more)
        {
            var outgoingData = Encode(message, more);

            m_stream.SendMoreFrame(identity);
            m_stream.SendFrame(outgoingData);
        }

        protected void WriteIngoing(NetMQMessage message)
        {
            m_messagesPipe.SendMultipartMessage(message);
        }        

        public void Run(PairSocket shim)
        {
            shim.SignalOK();

            shim.ReceiveReady += OnShimReady;

            m_messagesPipe = new PairSocket();
            m_messagesPipe.Connect(string.Format("inproc://wsrouter-{0}", m_id));
            m_messagesPipe.ReceiveReady += OnMessagePipeReady;

            m_stream = new StreamSocket();
            m_stream.ReceiveReady += OnStreamReady;

            m_poller = new NetMQPoller();
            m_poller.Add(m_messagesPipe);
            m_poller.Add(shim);
            m_poller.Add(m_stream);

            m_messagesPipe.SignalOK();

            m_poller.Run();

            m_messagesPipe.Dispose();
            m_stream.Dispose();
        }        

        private void OnShimReady(object sender, NetMQSocketEventArgs e)
        {
            string command = e.Socket.ReceiveFrameString();

            if (command == WSSocket.BindCommand)
            {
                string address = e.Socket.ReceiveFrameString();

                int errorCode = 0;

                try
                {
                    m_stream.Bind(address.Replace("ws://", "tcp://"));
                }
                catch (NetMQException ex)
                {
                    errorCode = (int)ex.ErrorCode;
                }

                byte[] bytes = BitConverter.GetBytes(errorCode);
                e.Socket.SendFrame(bytes);
            }
            else if (command == NetMQActor.EndShimMessage)
            {
                m_poller.Stop();
            }
        }

        private void OnStreamReady(object sender, NetMQSocketEventArgs e)
        {
            byte[] identity = m_stream.ReceiveFrameBytes();


            if (!m_clients.TryGetValue(identity, out WebSocketClient client))
            {
                client = new WebSocketClient(m_stream, identity);
                client.IncomingMessage += OnIncomingMessage;
                m_clients.Add(identity, client);

                OnNewClient(identity);
            }

            client.OnDataReady();

            if (client.State == WebSocketClientState.Closed)
            {
                m_clients.Remove(identity);
                client.IncomingMessage -= OnIncomingMessage;
                OnClientRemoved(identity);
            }
        }

        private void OnIncomingMessage(object sender, NetMQMessageEventArgs e)
        {
            OnIncomingMessage(e.Identity, e.Message);
        }

        private void OnMessagePipeReady(object sender, NetMQSocketEventArgs e)
        {
            NetMQMessage request = m_messagesPipe.ReceiveMultipartMessage();
            
            OnOutgoingMessage(request);          
        }

        private static byte[] Encode(byte[] data, bool more)
        {
            int frameSize = 2 + 1 + data.Length;
            int payloadStartIndex = 2;
            int payloadLength = data.Length + 1;

            if (payloadLength > 125)
            {
                frameSize += 2;
                payloadStartIndex += 2;

                if (payloadLength > 0xFFFF) // 2 bytes max value
                {
                    frameSize += 6;
                    payloadStartIndex += 6;
                }
            }

            byte[] outgoingData = new byte[frameSize];

            outgoingData[0] = (byte)0x82; // Binary and Final      

            // No mask
            outgoingData[1] = 0x00;

            if (payloadLength <= 125)
            {
                outgoingData[1] |= (byte)(payloadLength & 127);
            }
            else if (payloadLength <= 0xFFFF) // maximum size of short
            {
                outgoingData[1] |= 126;
                outgoingData[2] = (byte)((payloadLength >> 8) & 0xFF);
                outgoingData[3] = (byte)(payloadLength & 0xFF);
            }
            else
            {
                outgoingData[1] |= 127;
                outgoingData[2] = 0;
                outgoingData[3] = 0;
                outgoingData[4] = 0;
                outgoingData[5] = 0;
                outgoingData[6] = (byte)((payloadLength >> 24) & 0xFF);
                outgoingData[7] = (byte)((payloadLength >> 16) & 0xFF);
                outgoingData[8] = (byte)((payloadLength >> 8) & 0xFF);
                outgoingData[9] = (byte)(payloadLength & 0xFF);
            }

            // more byte
            outgoingData[payloadStartIndex] = (byte)(more ? 1 : 0);
            payloadStartIndex++;

            // payload
            Buffer.BlockCopy(data, 0, outgoingData, payloadStartIndex, data.Length);
            return outgoingData;
        }
    }
}
