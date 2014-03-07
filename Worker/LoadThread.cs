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
using RenderUtils;
using System.Threading;

namespace Worker
{
    public class LoadThread
    {
        public LoadThread(RenderLog log, LuxEngine engine, Scene scene)
        {
            m_log = log;
            m_engine = engine;
            m_scene = scene;
        }

        public bool Start()
        {
            try
            {
                m_engine.Cleanup();
            }
            catch (Exception e)
            {
                m_log.Warning(e.Message);
                m_engine.Exit();
                m_engine.Cleanup();
                m_log.Info("m_engine restarted");
            }
            m_isParseSuccessful = true;

            Thread loadEngineThread = new Thread(new ThreadStart(sceneLoadingThread));
            loadEngineThread.Start();

            // Wait until scene is parsed..
            Thread.Sleep(1000);
            while (!m_engine.IsSceneReady())
            {
                Thread.Sleep(1000);
            }

            if (!m_isParseSuccessful)
            {
                // we need to close engine and cleanup everything, so worker can render next scene
                m_log.Error("Scene parsing failed!");
                return false;
            }
            return true;
        }

        private void sceneLoadingThread()
        {
            // Load and parse lux scene in the engine
            try
            {
                m_isParseSuccessful = m_engine.LoadScene(m_scene.ScenePath);  //new thread stuck here in case of successful parsing and continue render
            }
            catch (Exception e)
            {
                m_log.Error("Error when parcing scene" + e.Message);
            }
        }

        private bool m_isParseSuccessful;
        private RenderLog m_log;
        private LuxEngine m_engine;
        private Scene m_scene;

    }
}
