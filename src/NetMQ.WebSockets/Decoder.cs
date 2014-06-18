using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NetMQ.zmq;

namespace NetMQ.WebSockets
{
  enum OpcodeEnum : byte
  {
    Continuation = 0, Text = 0x01, Binary = 0x03, Close = 0x08, Ping = 0x09, Pong = 0xA
  }

  class MessageEventArgs : EventArgs
  {
    public MessageEventArgs(OpcodeEnum opcode, byte[] payload)
    {
      Opcode = opcode;
      Payload = payload;    
    }

    public OpcodeEnum Opcode { get; private set; }
    public byte[] Payload { get; private set; }   
  }

  class Decoder
  {
    const byte FinalBit = 0x80;
    private const byte MaskedBit = 0x80;

    enum State
    {
      NewMessage, SecondByte, ShortSize, ShortSize2,
      LongSize, LongSize2, LongSize3, LongSize4, LongSize5, LongSize6, LongSize7, LongSize8,
      Mask, Mask2, Mask3, Mask4, Payload, PayloadInProgress,
    }

    private State m_state;

    private bool m_final;
    private OpcodeEnum m_opcode;
    private bool m_isMaksed;
    private byte[] m_mask = new byte[4];

    private int m_payloadLength;
    private byte[] m_payload;
    private int m_payloadIndex;

    public event EventHandler<MessageEventArgs> Message;

    public void Process(byte[] message)
    {
      int i = 0;

      while (i < message.Length)
      {
        switch (m_state)
        {         
          case State.Payload:
            m_payloadIndex = 0;
            m_payload = new byte[m_payloadLength];
            goto case State.PayloadInProgress;

          case State.PayloadInProgress:                       
            int bytesToRead = m_payloadLength - m_payloadIndex;

            if (bytesToRead > (message.Length - i))
            {
              bytesToRead = message.Length - i;
            }            

            Buffer.BlockCopy(message, i, m_payload, m_payloadIndex, bytesToRead);            

            if (m_isMaksed)
            {
              for (int j = m_payloadIndex; j < m_payloadIndex + bytesToRead; j++)
              {
                m_payload[j] = (byte)(m_payload[j] ^ m_mask[j % 4]);
              }
            }

            m_payloadIndex += bytesToRead;
            i += bytesToRead;

            if (m_payloadIndex < m_payloadLength)
            {
              m_state = State.PayloadInProgress;
            }
            else
            {
              var temp = Message;
              if (temp != null)
              {
                temp(this, new MessageEventArgs(m_opcode, m_payload));
              }

              m_state = State.NewMessage;
            }

            break;
          default:
            Process(message[i]);
            i++;
            break;
        }
      }
    }

    void Process(byte b)
    {
      switch (m_state)
      {
        case State.NewMessage:
          m_final = (b & FinalBit) != 0;
          m_opcode = (OpcodeEnum)(b & 0xF);
          m_state = State.SecondByte;
          break;
        case State.SecondByte:
          m_isMaksed = (b & MaskedBit) != 0;
          byte length = (byte)(b & 0x7F);

          if (length < 126)
          {
            m_payloadLength = length;
            m_state = m_isMaksed ? State.Mask : State.Payload;
          }
          else if (length == 126)
          {
            m_state = State.ShortSize;
          }
          else
          {
            m_state = State.LongSize;
          }
          break;
        case State.Mask:
          m_mask[0] = b;
          m_state = State.Mask2;
          break;
        case State.Mask2:
          m_mask[1] = b;
          m_state = State.Mask3;
          break;
        case State.Mask3:
          m_mask[2] = b;
          m_state = State.Mask4;
          break;
        case State.Mask4:
          m_mask[3] = b;
          m_state = State.Payload;
          break;
        case State.ShortSize:
          m_payloadLength = b << 8;
          m_state = State.ShortSize2;
          break;
        case State.ShortSize2:
          m_payloadLength |= b;
          m_state = m_isMaksed ? State.Mask : State.Payload;
          break;
        case State.LongSize:          
          m_payloadLength = 0;

          // must be zero, max message size is MaxInt
          Debug.Assert(b==0);
          m_state = State.LongSize2;
          break;
        case State.LongSize2:
          // must be zero, max message size is MaxInt
          Debug.Assert(b == 0);
          m_state = State.LongSize3;
          break;
        case State.LongSize3:
          // must be zero, max message size is MaxInt
          Debug.Assert(b == 0);
          m_state = State.LongSize4;
          break;
        case State.LongSize4:
          // must be zero, max message size is MaxInt
          Debug.Assert(b == 0);
          m_state = State.LongSize5;
          break;
        case State.LongSize5:
          m_payloadLength |= b << 24;
          m_state = State.LongSize6;
          break;
        case State.LongSize6:
          m_payloadLength |= b << 16;
          m_state = State.LongSize7;
          break;
        case State.LongSize7:
          m_payloadLength |= b << 8;
          m_state = State.LongSize8;
          break;
        case State.LongSize8:
          m_payloadLength |= b;
          m_state = m_isMaksed ? State.Mask : State.Payload;
          break;
      }
    }

  }
}
