/**********************************************************************************
*    Copyright (C) 2014 by AMC Bridge (see: http://amcbridge.com/)                *
*                                                                                 *
*    This file is part of LuxRender for Cloud.                                    *
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;

namespace RenderUtils.QueueMessage
{
    public class MessageQueue<T> where T:class
    {
        public MessageQueue(string suffix = "", bool toClear = false)
        {
            m_suffix = suffix;
            m_binFormatter = new BinaryFormatter();
            m_writeLock = new Object();
            // Queue name is based on message type, e.g. DispatcherMessage => DispatcherMessageQueue
            string queueName = typeof(T).Name + "Queue" + suffix;
            m_queue = renderStorage.Get().GetOrCreateQueue(queueName);
            if (m_queue == null)
                throw new Exception("Cannot create <" + queueName + "> queue");
            if (toClear)
                Clear();
        }

        public void AddMessage(T message)
        {
            if (m_queue == null)
                return;

            lock (m_writeLock)
            {
                MemoryStream memStream = new MemoryStream();
                m_binFormatter.Serialize(memStream, message);

                CloudQueueMessage cloudMessage = new CloudQueueMessage(memStream.GetBuffer());
                m_queue.AddMessage(cloudMessage);
            }
        }
        /// <summary>
        /// Wait for message with requested type in 'this' queue
        /// </summary>
        /// <returns>first message in queue if it matches for request type. Null if exited by condition</returns>
        public T WaitForMessage(Type type, FinishCondition finish = null, LoopAction action = null)
        {
            while (true)
            {
                if (finish != null && finish())
                    return null;
                if (action != null)
                    action.Execute();
                CloudQueueMessage startMsg = GetMessage();
                if (startMsg == null)
                {
                    System.Threading.Thread.Sleep(1000);
                    continue;
                }
                DeleteMessage(startMsg);
                T message = ConvertMessage(startMsg);
                if (message.GetType() == type)
                {
                    return message;
                }
            }
        }

        public T ConvertMessage(CloudQueueMessage message)
        {
            Object decodedMessage;
            lock (m_writeLock)
            {
                MemoryStream memStream = new MemoryStream(message.AsBytes);
                decodedMessage = m_binFormatter.Deserialize(memStream);
            }
            
            T convertedMessage = decodedMessage as T;
            if (convertedMessage == null)
                throw new Exception("Incorrect type of message");

            return convertedMessage;
        }

        public T GetMessageByType(Type type)
        {
            if (m_queue == null)
                return null;
            CloudQueueMessage startMsg = GetMessage();
            if (startMsg == null)
            {
                return null;
            }
            DeleteMessage(startMsg);
            T message = ConvertMessage(startMsg);
            if (message.GetType() == type)
            {
                return message;
            }
            return null;
        }

        public CloudQueueMessage GetMessage()
        {
            if (m_queue == null)
                return null;
            lock (m_writeLock)
            {
                return m_queue.GetMessage();
            }
        }

        public CloudQueueMessage PeekMessage()
        {
            if (m_queue == null)
                return null;
            lock (m_writeLock)
            {
                return m_queue.PeekMessage();
            }
        }

        public void DeleteMessage(CloudQueueMessage msg)
        {
            if (m_queue == null)
                return;

            lock (m_writeLock)
            {
                m_queue.DeleteMessage(msg);
            }
        }

        public void Clear()
        {
            if (m_queue == null)
                return;

            lock (m_writeLock)
            {
                m_queue.Clear();
            }
        }

        //deleting queue and all messages in it
        public void Delete()
        {
            m_queue.DeleteIfExists();
        }

        public string getSuffix()
        {
            return m_suffix;
        }

        private string m_suffix;
        private BinaryFormatter m_binFormatter;
        private CloudQueue m_queue;
        private Object m_writeLock;

        public delegate bool FinishCondition();
    }

}
