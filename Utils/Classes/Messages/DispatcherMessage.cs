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

namespace RenderUtils.QueueMessage
{
    [Serializable]
    public abstract class DispetcherMessage
    {
        protected DispetcherMessage(string sessionId)
        {
            SessionId = sessionId;
        }

        public string SessionId { get; protected set; }
    }

    [Serializable]
    public class WorkerIsReady : DispetcherMessage
    {
        public WorkerIsReady(string sessionId) :
            base(sessionId) { }
    }

    [Serializable]
    public class MergerIsReady : DispetcherMessage
    {
        public MergerIsReady(string sessionId, string suffix) :
            base(sessionId)
        {
            MergerQueueSuffix = suffix;
        }
        public string MergerQueueSuffix { get; protected set; }
    }

    [Serializable]
    public class MergerUpdateMessage : DispetcherMessage
    {
        public MergerUpdateMessage(string imagePath, string sessionId, float percentageCompleted, double spp, double requiredSpp
            ) : base(sessionId)
        {
            ImagePath = imagePath;
            PercentageCompleted = percentageCompleted;
            SPP = spp;
            RequiredSpp = requiredSpp;
        }
        public double RequiredSpp { get; protected set; }
        public double SPP { get; protected set; }
        public string ImagePath { get; protected set; }
        public float PercentageCompleted { get; protected set; } //part of scene that is already rendered
    }

    [Serializable]
    public class MergerUpdateFailedMessage : DispetcherMessage
    {
        public MergerUpdateFailedMessage(string error, string sessionId) :
            base(sessionId)
        {
            ErrorMessage = error;
        }
        public string ErrorMessage { get; protected set; }
    }


    [Serializable]
    public class MergerFinishMessage : DispetcherMessage
    {
        public MergerFinishMessage(int mergerRoleID, string sessionId, string flmUri, double spp, bool isError) :
            base(sessionId)
        {
            IsError = isError;
            MergerRoleID = mergerRoleID;
            FlmUri = flmUri;
            SPP = spp;
        }
        public bool IsError { get; protected set; }
        public string FlmUri { get; protected set; }
        public int MergerRoleID { get; protected set; }
        public double SPP { get; protected set; }
    }
}
