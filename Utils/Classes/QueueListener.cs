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
using Microsoft.WindowsAzure.Storage.Queue;
using RenderUtils.QueueMessage;
using NetworkMessage;

namespace RenderUtils
{
    public interface ExitConditions
    {
        bool IsExit();
    }

    public class QueueListener<T> where T:class
    {
        public QueueListener(MessageQueue<T> listenedQueue, RenderLog log)
        {
            m_messageResponses = new Dictionary<string, QueueListener<T>.MessageResponse>();
            m_listenedQueue = listenedQueue;
            m_log = log;
        }

        public void AddResponse(string messageName, MessageResponse methodName)
        {
            m_messageResponses.Add(messageName, methodName);
        }

        /// <summary>
        /// Executes loop with getting messages from other roles
        /// </summary>
        /// <param name="exitConditions">List of delegates that return true if there is finish</param>
        public void Run(ExitConditions exitConditions = null)
        {
            bool finish = false;
            while (!finish)    //particular scene merging
            {
                if (exitConditions != null && exitConditions.IsExit())
                {
                    return;
                }
                CloudQueueMessage msg = m_listenedQueue.GetMessage();
                if (msg == null)
                {
                    System.Threading.Thread.Sleep(1000);
                    continue;
                }
                m_listenedQueue.DeleteMessage(msg);
                T merMes = m_listenedQueue.ConvertMessage(msg);
                if (m_messageResponses.ContainsKey(merMes.GetType().Name))
                {
                    MessageResponse actionForMessage = m_messageResponses[merMes.GetType().Name];
                    finish = actionForMessage(merMes);
                }
                else
                {
                    m_log.Warning("Incorrect type of message");
                }
            }
        }

        public delegate bool MessageResponse(T message);
        Dictionary<string, MessageResponse> m_messageResponses;
        MessageQueue<T> m_listenedQueue;
        RenderLog m_log;
    }
}
