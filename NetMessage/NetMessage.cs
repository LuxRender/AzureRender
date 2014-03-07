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
using System.Collections.Generic;
using System.Text;

namespace NetworkMessage
{
    [Serializable]
    public abstract class NetMessage
    {
    }

    [Serializable]
    public class StatusMessage : NetMessage
    {
        public enum StatusEnum
        {
            OK,
            Failed,
            Inform,
            Aborted,
            Allocated,
            Authorized,
            Unauthorized
        }

        public StatusMessage(StatusEnum status)
        {
            Status = status;
        }

        public StatusMessage(StatusEnum status, string message)
        {
            Status = status;
            Message = message;
        }

        public StatusEnum Status { get; protected set; }
        public string Message { get; protected set; }
    }

    [Serializable]
    public class SceneMessage : NetMessage
    {
        public SceneMessage(int instanceCnt,
                            int updateTime,
                            int totalTime,
                            int sceneFileSize,
                            string sceneFileName,
                            string login,
                            string pwd, 
                            double requiredSpp)
        {
            InstanceCnt = instanceCnt;
            UpdateTime = updateTime;
            TotalTime = totalTime;
            SceneFileSize = sceneFileSize;
            SceneFileName = sceneFileName;
            Login = login;
            Pwd = pwd;
            RequiredSpp = requiredSpp;
        }

        public int InstanceCnt { get; protected set; }
        public int UpdateTime { get; protected set; }
        public int TotalTime { get; protected set; }
        public int SceneFileSize { get; protected set; }
        public string SceneFileName { get; protected set; }
        public string Login { get; protected set; }
        public string Pwd { get; protected set; }
        public double RequiredSpp { get; protected set; }
    }

    [Serializable]
    public class FlmMessage : NetMessage
    {
        public FlmMessage(string flmUri)
        {
            FlmUri = flmUri;
        }

        public string FlmUri { get; protected set; }
    }

    [Serializable]
    public class ImageMessage : NetMessage
    {
        public ImageMessage(int fileSize, float percentageCompleted, double spp)
        {
            FileSize = fileSize;
            PercentageCompleted = percentageCompleted;
            SPP = spp;
        }
        public int FileSize { get; protected set; }
        public float PercentageCompleted { get; protected set; }
        public double SPP { get; protected set; }
    }

    [Serializable]
    public class RenderFinishMessage : NetMessage
    {
        public RenderFinishMessage(string flmUri, double spp)
        {
            FlmUri = flmUri;
            SPP = spp;
        }
        public string FlmUri { get; protected set; }
        public double SPP { get; protected set; }
    }

    [Serializable]
    public class AuthorizationResultMessage : NetMessage
    {
        public AuthorizationResultMessage(bool isAuthorized)
        {
            IsAuthorized = isAuthorized;
        }
        public bool IsAuthorized { get; protected set; }
    }

    //TODO: User and password should be encoded.
    [Serializable]
    public class AuthorizationMessage : NetMessage
    {
        public AuthorizationMessage(string login, string password )
        {
            Login = login;
            Pwd = password;
        }
        
        public string Login { get; protected set; }
        public string Pwd { get; protected set; }
    }
}
