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

namespace RenderUtils
{
    public class SceneID
    {
        public static SceneID Get()
        {
            if (m_sceneIdInstance == null)
                m_sceneIdInstance = new SceneID();
            return m_sceneIdInstance; 
        }

        public int GetNewSceneID()
        {
            lock (m_sceneIdLock)
            {
                return m_newSceneNumber++;
            }
        }

        private SceneID()
        {
            m_newSceneNumber = 0;
        }

        private static SceneID m_sceneIdInstance;
        private int m_newSceneNumber;
        private Object m_sceneIdLock = new Object();
    }
}
