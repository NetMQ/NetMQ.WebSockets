using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetMQ.zmq;

namespace NetMQ.WebSockets
{
    public class WSPublisher : WSSocket
    {
        //  List of all subscriptions mapped to corresponding pipes.
        private readonly Mtrie m_subscriptions;

        private bool m_more;

        private static readonly Mtrie.MtrieDelegate s_markAsMatching;
        private static readonly Mtrie.MtrieDelegate s_SendUnsubscription;

        private List<Blob> m_matching;
        private int m_active = 0;

        static WSPublisher()
        {
            s_markAsMatching = (pipe, data, arg) =>
            {
                WSPublisher self = (WSPublisher)arg;

                NetMQ.zmq.Utils.Swap(self.m_matching, self.m_matching.IndexOf(pipe), self.m_active);
                self.m_active++;
            };

            s_SendUnsubscription = (pipe, data, arg) =>
            {
            };
        }

        internal WSPublisher(NetMQContext context)
            : base(context)
        {
            m_more = false;

            m_subscriptions = new Mtrie();
            m_matching = new List<Blob>();
        }

        protected internal override void AttachClient(zmq.Blob identity)
        {
            m_matching.Add(identity);
        }

        protected internal override void ClientTerminated(zmq.Blob identity)
        {
            m_subscriptions.RemoveHelper(identity, s_SendUnsubscription, this);

            int index = m_matching.IndexOf(identity);

            if (index < m_active)
            {
                m_active--;
            }

            m_matching.Remove(identity);
        }

        protected internal override void XSend(byte[] message, bool dontWait, bool more)
        {
            if (!m_more)
            {
                m_subscriptions.Match(message, message.Length, s_markAsMatching, this);
            }

            for (int i = 0; i < m_active; i++)
            {
                WriteMessage(m_matching[i].Data, message, dontWait, more);
            }

            if (!more)
            {
                m_active = 0;
            }

            m_more = more;
        }

        protected internal override bool XReceive(out byte[] message, out bool more)
        {
            throw NetMQException.Create("Messages cannot be received from PUB socket", ErrorCode.ENOTSUP);
        }

        protected override void Process(bool untilMessageAvailable)
        {
            base.Process(untilMessageAvailable);

            byte[] identity;
            byte[] data;
            bool more;

            while (ReadMessage(out identity, out data, out more))
            {
                if (data.Length > 0 && (data[0] == '1' || data[0] == '0'))
                {
                    if (data[0] == '0')
                    {
                        m_subscriptions.Remove(data, 1, new Blob(identity));                        
                    }
                    else
                    {
                        m_subscriptions.Add(data, 1, new Blob(identity));
                    }
                }
            }
        }

        protected internal override bool XHasIn()
        {
            return false;
        }

        protected internal override bool XHasOut()
        {
            return m_matching.Count > 0;
        }


    }
}
