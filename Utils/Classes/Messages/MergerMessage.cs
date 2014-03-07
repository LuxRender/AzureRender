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
    public abstract class MergerMessage
    {
    }

    [Serializable]
    public class RenderingFinishMessage : MergerMessage
    {
        public RenderingFinishMessage(int roleID, string sceneName, int sceneID, bool isError)
        {
            SceneID = sceneID;
            RoleID = roleID;
            SceneName = sceneName;
            IsError = isError;
        }
        public bool IsError { get; protected set; }
        public int SceneID { get; protected set; }
        public int RoleID { get; protected set; }
        public string SceneName { get; protected set; }
    }

    [Serializable]
    public class ToMergeMessage : MergerMessage
    {
        public ToMergeMessage(string flm, string sceneUri, int id)
        {
            ID = id;
            Flm = flm; //flm URI
            SceneUri = sceneUri; //scene file URI
        }
        public string Flm { get; protected set; }
        public int ID { get; protected set; }
        public string SceneUri { get; protected set; }
    }

    [Serializable]
    public class StartMergeMessage : MergerMessage
    {
        public StartMergeMessage(int renderRoleCnt, int rendTime, int updateTime, string sessionId, int sceneId, double requiredSpp)
        {
            RenderTime = rendTime;
            UpdateTime = updateTime;
            RenderRoleCnt = renderRoleCnt;
            SessionId = sessionId;
            SceneId = sceneId;
            RequiredSpp = requiredSpp;
        }
        public string SessionId { get; protected set; }
        public int RenderTime { get; protected set; }
        public int UpdateTime { get; protected set; }
        public int RenderRoleCnt { get; protected set; }
        public int SceneId { get; protected set; }
        public double RequiredSpp { get; protected set; }
    }

    [Serializable]
    public class AbortMergeMessage : MergerMessage
    {
        public AbortMergeMessage(string sceneName, int sceneID)
        {
            SceneName = sceneName;
            SceneID = sceneID;
        }
        public int SceneID { get; protected set; }
        public string SceneName { get; protected set; }
    }

}
