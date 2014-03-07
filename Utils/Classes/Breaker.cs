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
using System.Diagnostics;
using System.Threading;

namespace RenderUtils
{
    public class Breaker
    {
        #region PublicMethods

        public Breaker(Int64 sendImageInterval, Int64 renderTime, double requiredSpp,
            LuxEngine engine, RenderLog log)
        {
            m_totalRenderTime = renderTime * 1000; //time in millisec
            m_requiredSpp = requiredSpp;
            m_sendImageInterval = sendImageInterval * 1000;
            m_timeToNextUpdate = m_sendImageInterval;
            m_engine = engine;
            m_log = log;
            m_log.Info("Required SPP: " + m_requiredSpp);
            m_currentSpp = 0;
            m_log.Info("Breaker initialized");
        }

        public void Start()
        {
            m_stopWatch.Start();  
        }

        public void Stop()
        {
            m_stopWatch.Stop();
        }

        public void Reset()
        {
            m_stopWatch.Reset();
            m_currentSpp = 0;
            m_totalRenderTime = 0;
            m_requiredSpp = 0;
            m_sendImageInterval = 0;
            m_timeToNextUpdate = 0;
        }

        public bool DoSendUpdate()
        {
            if (m_stopWatch.Elapsed.TotalMilliseconds >= m_timeToNextUpdate)
            {
                m_timeToNextUpdate += m_sendImageInterval;
                return true;
            }
            else
                return false;
        }

        public void CountSpp()
        {
            m_currentSpp += m_engine.GetSpp();
        }

        public bool IsTimeToStop()
        {
            if (m_totalRenderTime != 0)
            {
                if (m_totalRenderTime <= m_stopWatch.Elapsed.TotalMilliseconds)
                    return true;
                else
                    return false;
            }
            if (m_requiredSpp != 0)
            {
                if (m_requiredSpp <= m_currentSpp)
                    return true;
                else
                    return false;
            }
            m_log.Error("Not specified way to stop rendering. Required time: " + m_totalRenderTime + ". SPP: " + m_requiredSpp);
            return true;
        }

        #endregion

        #region PrivateFields
        Int64 m_totalRenderTime; //total time of rendering scene
        Int64 m_timeToNextUpdate; //time left to next message to merger
        Int64 m_sendImageInterval; //interval of time after each image will be sending to client
        double m_requiredSpp;
        double m_currentSpp;
        Stopwatch m_stopWatch = new Stopwatch(); //see http://msdn.microsoft.com/en-us/library/system.diagnostics.stopwatch.aspx
        LuxEngine m_engine;
        RenderLog m_log;
        #endregion
    }
}
