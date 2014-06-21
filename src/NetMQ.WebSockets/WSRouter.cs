using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ.zmq;

namespace NetMQ.WebSockets
{
    public class WSRouter : WSSocket
    {
        private int m_clientCounter;

        private bool m_outMore = false;
        private bool m_inMore = false;

        private bool m_firstFrame = false;

        private byte[] m_destinationBlob;

        private byte[] m_prefetched ;

        internal WSRouter(NetMQContext context)
            : base(context)
        {

        }

        protected override string BytesToString(byte[] data)
        {
            // first frame is the identity and should be encoded with ASCII
            if (m_firstFrame)
            {
                return Encoding.ASCII.GetString(data);
            }

            return base.BytesToString(data);
        }

        protected override byte[] StringToBytes(string data)
        {
            // first frame is the identity and should be encoded with ASCII
            if (!m_outMore)
            {
                return Encoding.ASCII.GetBytes(data);
            }

            return base.StringToBytes(data);
        }

        protected internal override void XSend(byte[] message, bool dontWait, bool more)
        {
            if (!m_outMore)
            {
                m_destinationBlob = message;
            }
            else
            {
                WriteMessage(m_destinationBlob, message, dontWait, more);    
            }

            m_outMore = more;
        }

        protected internal override bool XReceive(out byte[] message, out bool more)
        {
            m_firstFrame = false; 

            if (m_prefetched != null)
            {
                more = m_inMore;
                message = m_prefetched;
                m_prefetched = null;
                return true;
            }

            byte[] identity;

            var isMessageRead = ReadMessage(out identity, out message, out more);

            if (isMessageRead)
            {                
                if (!m_inMore)
                {
                    m_prefetched = message;
                    message = identity;
                    m_inMore = more;
                    more = true;
                    m_firstFrame = true;
                    return true;
                }

                m_inMore = more;

                return true;
            }

            return false;
        }

        protected internal override bool XHasIn()
        {
            return m_prefetched != null || !IsMessageQueueEmpty;
        }

        protected internal override bool XHasOut()
        {
            return m_clientCounter > 0;
        }

        protected internal override void AttachClient(zmq.Blob identity)
        {
            m_clientCounter++;
        }

        protected internal override void ClientTerminated(zmq.Blob identity)
        {
            m_clientCounter--;
        }
    }
}
