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
using Microsoft.WindowsAzure.Storage.Blob;

namespace RenderUtils
{
    public class RenderLog
    {
        public const string LOG_CONTAINER_NAME = "render-logs";

        public RenderLog(string logName)
        {
            m_blobLog = renderStorage.Get().CreateBlob(LOG_CONTAINER_NAME.ToLower(), logName.ToLower() + ".txt", true);
            if (m_blobLog == null)
                System.Diagnostics.Trace.TraceWarning("Cannot create log <" + logName + ">");
            m_writeLock = new Object();
        }

        public void Info(string message)
        {
            addLineToLog("INFO", message);
        }

        public void Warning(string message)
        {
            addLineToLog("WARNING", message);
        }

        public void Error(string message)
        {
            addLineToLog("ERROR", message);
        }

        private void addLineToLog(string prefix, string message)
        {
            lock (m_writeLock)
            {
                if (m_blobLog == null)
                    return;
                string blobText = Utils.DownloadBlobText(m_blobLog);
                blobText += String.Format("{0} {1} {2}\r\n", DateTime.Now.ToString(), prefix, message);
                Utils.UpploadBlobText(m_blobLog, blobText);
            }
        }

        private Object m_writeLock;
        private CloudBlockBlob m_blobLog;
    }
}
