/**********************************************************************************
*    Copyright (C) 2014 by AMC Bridge (see: http://amcbridge.com/)                *
*                                                                                 *
*    This file is part of LuxRender for Cloud.                                         *
*                                                                                 *
*    LuxRender for Cloud is free software: you can redistribute it and/or modify  *
*    it under the terms of the GNU General Public License as published by         *
*    the Free Software Foundation, either version 3 of the License, or            *
*    (at your option) any later version.                                          *
*                                                                                 *
*    LuxRender for Cloud is distributed in the hope that it will be useful,       *
*    but WITHOUT ANY WARRANTY; without even the implied warranty of               *
*    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the                *
*    GNU General Public License for more details.                                 *
*                                                                                 *
*    You should have received a copy of the GNU General Public License            *
*    along with LuxRender for Cloud. If not, see <http://www.gnu.org/licenses/>.  *
*                                                                                 *
*    This project is based on Lux Renderer ; see http://www.luxrender.net         *
***********************************************************************************/

using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;

namespace NetworkMessage
{
    public class NetMessageHandler
    {
        #region Events

        public event EventHandler FileSendingEvent;
        public event EventHandler FileSended;

        #endregion

        #region Constructor

        public NetMessageHandler(TcpClient client, int readTimeout = -1)
        {
            m_BinFormatter = new BinaryFormatter();
            m_Client = client;
            m_NetStream = client.GetStream();

            if (readTimeout != -1)
                m_NetStream.ReadTimeout = readTimeout * 1000;
        }

        #endregion

        #region Public logic

        public void SendSynchMessage(NetMessage message)
        {
            MemoryStream memStream = new MemoryStream();
            m_BinFormatter.Serialize(memStream, message);

            int length = (int)memStream.Length;
            byte[] header = BitConverter.GetBytes(length);

            const int IntSize = sizeof(int);
            int sendingDataSize = IntSize + (int)memStream.Length;
            byte[] bufer = new byte[sendingDataSize];

            Buffer.BlockCopy(header, 0, bufer, 0, IntSize);
            Buffer.BlockCopy(memStream.GetBuffer(), 0, bufer, IntSize, (int)memStream.Length);

            m_NetStream.Write(bufer, 0, sendingDataSize);

            if (m_Client.Connected == false)
                throw new Exception("Connection is broken off.");
        }

        public NetMessage GetSynchMessage()
        {
            //There was no message sent. Stream is empty.
            if (!m_NetStream.DataAvailable)
                return null;

            //get header
            int sizeInt = sizeof(int);
            byte[] header = new byte[sizeInt];
            int recvSize = m_NetStream.Read(header, 0, sizeInt);

            // if this message is empty then this situation is correct 
            // and this means that message was sent for testing connection
            if (recvSize == 0)
                return null;

            if (recvSize != sizeInt)
                throw new Exception("Received data of header has inccorect size.");


            int bufferSize = BitConverter.ToInt32(header, 0); ;
            byte[] buffer = new byte[bufferSize];

            //get message as array bytes
            recvSize = m_NetStream.Read(buffer, 0, bufferSize);
            if (recvSize != bufferSize)
                throw new Exception("Received data of message has inccorect size.");

            //convert bytes to message
            NetMessage recvMessage = (NetMessage)convertMessage(buffer);
            if (recvMessage == null)
                throw new Exception("Type of received data is incorrect.");

            return recvMessage;
        }

        public void GetFile(byte[] sceneFile)
        {
            int offset = 0;
            int recvSize = 0;

            do
            {
                if (offset + m_Client.ReceiveBufferSize > sceneFile.Length)
                    recvSize = m_NetStream.Read(sceneFile, offset, sceneFile.Length - offset);
                else
                    recvSize = m_NetStream.Read(sceneFile, offset, m_Client.ReceiveBufferSize);

                if (recvSize <= 0)
                    throw new Exception("Error receiving scene.");
                offset += recvSize;
            }
            while (offset != sceneFile.Length);
        }

        public bool Connected()
        {
            return m_Client.Connected;
        }

        public void SendFile(byte[] sceneFile)
        {
            m_offset = 0;
            EventHandler temp = FileSendingEvent;
            do
            {
                int size = m_Client.SendBufferSize;
                if (m_offset + m_Client.SendBufferSize > sceneFile.Length)
                    size = sceneFile.Length - m_offset;


                if (m_NetStream.CanWrite)
                    m_NetStream.Write(sceneFile, m_offset, size);
                else
                    throw new Exception("Error sending a scene");
                if (m_Client.Connected == false)
                    throw new Exception("Connection is broken off.");

                m_offset += size;

                if( temp!=null )
                {
                    temp( this, new EventArgs() );
                }
            }
            while (m_offset != sceneFile.Length);

            temp = FileSended;
            if( temp!=null )
            {
                temp( this, new EventArgs() );
            }
        }

        #endregion

        #region Properties

        public int Offset
        {
            get
            {
                return m_offset;
            }
        }

        #endregion

        #region Private logic

        private Object convertMessage(byte[] buffer)
        {
            MemoryStream memStream = new MemoryStream(buffer);
            return m_BinFormatter.Deserialize(memStream);
        }

        #endregion

        #region Private fields

        private BinaryFormatter m_BinFormatter = null;

        private TcpClient m_Client = null;

        private NetworkStream m_NetStream = null;

        int m_offset = 0;

        #endregion
    }
}
