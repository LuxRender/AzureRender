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
    public abstract class RenderMessage
    {
    }

    [Serializable]
    public class IsFreeMessage : RenderMessage
    {
        public IsFreeMessage(string sessionGuid, int sceneId)
        {
            SceneId = sceneId;
            SessionId = sessionGuid;
        }
        public int SceneId { get; protected set; }
        public string SessionId { get; protected set; }
    }

    [Serializable]
    public class ToRenderMessage : RenderMessage
    {
        public ToRenderMessage(string sceneName, int haltTime, int interval,
            int sceneId, string abortQueueSuffix, double requiredSpp)
        {
            SceneName = sceneName; //URI of unique blob with scene file
            HaltTime = haltTime;
            WriteInterval = interval;
            SceneId = sceneId;
            AbortQueueSuffix = abortQueueSuffix;
            RequiredSpp = requiredSpp;
        }
        public string SceneName { get; protected set; }
        public int HaltTime { get; protected set; }
        public int WriteInterval { get; protected set; }
        public int Hspp { get; protected set; }
        public int SceneId { get; protected set; }
        public string AbortQueueSuffix { get; protected set; }
        public double RequiredSpp { get; protected set; }
    }

    [Serializable]
    public class ToAbortRenderMessage : RenderMessage
    {
    }
}
