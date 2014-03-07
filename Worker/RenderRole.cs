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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading;
using System.Timers;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using RenderUtils;
using NetworkMessage;
using RenderUtils.QueueMessage;

namespace Worker
{
    public class RenderRole : RoleEntryPoint
    {
        #region Methods

        public override void Run()
        {
            try
            {
                Trace.TraceInformation("Listening for queue messages...");
                m_log.Info("Worker is started.\nListening for queue messages...");
                MessageQueue<RenderMessage> localQueue = null;
                while (true)
                {
                    //check if worker is allocated and ready to work
                    RenderMessage rendMes = m_renderHandler.WaitForMessage(typeof(IsFreeMessage));
                    m_log.Info("IsFreeMessage");
                    IsFreeMessage isFreeMessage = rendMes as IsFreeMessage;
                    MessageQueue<DispetcherMessage> dispHandler = new MessageQueue<DispetcherMessage>(isFreeMessage.SceneId.ToString()); //create queue to dispatcher thread
                    localQueue = new MessageQueue<RenderMessage>(isFreeMessage.SceneId.ToString());
                    m_log.Info("sending 'I am ready' message");
                    dispHandler.AddMessage(new WorkerIsReady(isFreeMessage.SessionId)); //signal to dispatcher: "Ready to work!"
                    m_aborted = false;
                    m_log.Info("/IsFreeMessage");

                    rendMes = localQueue.WaitForMessage(typeof(ToRenderMessage));
                    m_log.Info("Got messege: " + rendMes.GetType().Name);
                    m_log.Info("ToRenderMessage start");
                    ToRenderMessage message = rendMes as ToRenderMessage;
                    initializeRender(message);
                    startRender();
                    m_log.Info("Abort queue name suffix: " + message.AbortQueueSuffix);
                    startWaitingProcess(message.AbortQueueSuffix);
                    renderFinished();
                    m_log.Info("/ToRenderMessage");
                }
            }
            catch (Exception e)
            {
                interruptRender(e.Message);                
            }
        }

        private void initializeRender(ToRenderMessage message)
        {
            string sceneBlobUri = message.SceneName;
            m_sceneID = message.SceneId;
            m_sceneUri = message.SceneName;
            m_mergerHandler = new MessageQueue<MergerMessage>(message.SceneId.ToString());
            m_breaker = new Breaker(message.WriteInterval, message.HaltTime,
                message.RequiredSpp, m_engine, m_log);

            m_log.Info("Scene start." + DateTime.Now);
            m_scene = new Scene(sceneBlobUri, m_localDirectory);
            m_log.Info("Scene finish." + DateTime.Now);
            m_log.Info("Scene created");
            m_log.Info(m_scene.SceneDirectory);
            m_log.Info(m_scene.ScenePath);
        }

        private void startRender()
        {
            try
            {
                m_log.Info("Render started");
                Thread thread = new Thread(new ThreadStart(render));
                thread.Start();
            }
            catch (Exception e)
            {
                Trace.TraceError("Exception while rendering. Message: '{0}', {1}", e.Message, RoleEnvironment.CurrentRoleInstance.Id);
                m_log.Error("Render exception: " + e.Message);
            }
            m_isRendering = true;
        }

        /// <summary>
        /// Listen abort queue for abort message untill render is finished.
        /// </summary>
        /// <param name="suffix">Suffix of abort queue name</param>
        private void startWaitingProcess(string suffix)
        {
            //create Queue for abort message            
            try
            {
                m_abortQueue = new MessageQueue<RenderMessage>(suffix);
            }
            catch (Exception e)
            {
                m_log.Error("Error creating queue: " + e.Message);
                abort();
            }
            RenderMessage rendMessage = m_abortQueue.WaitForMessage(typeof(ToAbortRenderMessage), roleIsFinish);
            if (rendMessage != null)
            {
                m_log.Info("message type: " + rendMessage.GetType().Name);
                m_log.Info("Abort message");
                abort();
            }
        }

        private void renderFinished()
        {
            try
            {
                m_scene.DeleteScene(m_log);
                if (m_abortQueue != null)
                    m_abortQueue.Delete();
            }
            catch (Exception ex)
            {
                m_log.Warning("Delete scene error: " + ex.Message + ". Should be deleted by last worker");
            }
        }

        private void interruptRender(string errorMsg)
        {
            Trace.TraceError("Exception when processing queue item. Message: '{0}'", errorMsg);
            m_log.Error("Exception when processing queue item. Message: " + errorMsg);
            int id = Utils.RoleID();
            RenderingFinishMessage finishMessage = new RenderingFinishMessage(id, null, m_sceneID, true); //if error with scene 
            m_mergerHandler.AddMessage(finishMessage);             //the best way is just free resources 
            abort();
            renderFinished();
        }

        private void render()
        {
            loadSceneAndStartRender();
            m_breaker.Start(); //start timer
            using (BreakerHandler breakerHandler = new BreakerHandler(m_breaker))
            {
                while (true)
                {
                    if (m_aborted)
                    {
                        onRenderFinished();
                        m_aborted = false;
                        return;
                    }
                    if (m_breaker.IsTimeToStop())
                    {
                        onRenderFinished();
                        break;
                    }
                    if (m_breaker.DoSendUpdate())
                    {
                        saveAndSendFlm();
                    }
                    else
                        wait();
                }
            }
        }

        private void loadSceneAndStartRender()
        {
            try
            {
                LoadThread loadRenderThread = new LoadThread(m_log, m_engine, m_scene);
                if (loadRenderThread.Start())
                {
                    //starting rendering for each availible logical kernel
                    int coresCount = Environment.ProcessorCount;
                    int newThreadsNumber = 1;
                    m_log.Info("Cores count: " + coresCount.ToString());
                    for (int i = 0; i < coresCount - 1; ++i)
                    {
                        newThreadsNumber = m_engine.AddThread();
                    }
                    m_log.Info("Now rendering in " + newThreadsNumber.ToString() + " threads");
                    m_log.Info("Parse successful!");
                }
            }
            catch (Exception e)
            {
                m_log.Error("Error starting render: " + e.Message);
            }
        }

        private void saveAndSendFlm()
        {
            try
            {
                m_log.Info("save flm start");
                string flmName = saveFLM();
                m_log.Info("save flm finish");
                ToMergeMessage message = new ToMergeMessage(flmName, m_sceneUri, m_sceneID);
                m_mergerHandler.AddMessage(message);
                m_log.Info("Messege sent to merger");
            }
            catch (Exception e)
            {
                m_log.Error("flm save UNHANDLED error: " + e.Message);
            }
        }

        private string saveFLM()
        {
            try
            {
                m_engine.PauseRendering();
                m_breaker.Stop();
                m_breaker.CountSpp();
                m_log.Info("Updating framebuffer");
                m_engine.UpdateFramebuffer();
                m_log.Info("Saving flm to stream");
                byte[] flmBuffer = m_engine.SaveFLMToStream(); //flm is reseted here after saving
                if (flmBuffer == null)
                    return string.Empty;
                m_engine.ResumeRendering();
                m_breaker.Start();
                m_log.Info("Creating new blob");
                CloudBlockBlob outputBlob = renderStorage.Get().CreateBlob(BlobName.FILM_BLOB, Guid.NewGuid().ToString() + ".flm");
                m_log.Info("Upploading flm to blob: " + outputBlob.Name);
                Utils.UpploadBlobByteArray(outputBlob, flmBuffer);
                m_log.Info("Flm uploaded successfully. URI: " + outputBlob.Uri.ToString());
                return outputBlob.Uri.ToString();
            }
            catch (Exception e)
            {
                m_log.Error("Error saving FILM: " + e.Message);
                return string.Empty;
            }
        }

        private void onRenderFinished()
        {
            m_engine.Exit();
            if (!m_aborted)
            {
                int id = Utils.RoleID();
                RenderingFinishMessage finishMessage = new RenderingFinishMessage(id, Scene.GetFileName(m_scene.InputPath), m_sceneID, false);
                m_mergerHandler.AddMessage(finishMessage);             //render has been finished
            }
            m_isRendering = false;
            m_log.Info("Timer finished");
        }

        private void abort()
        {
            try
            {
                m_aborted = true;
                m_log.Info("Aborted by user");
            }
            catch (Exception e)
            {
                m_log.Error("Error when aborting: " + e.Message);
            }
        }

        public override bool OnStart()
        {
            try
            {
                ServicePointManager.DefaultConnectionLimit = 12;
                RoleEnvironment.Changing += roleEnvironmentChanging;
                Microsoft.WindowsAzure.CloudStorageAccount.SetConfigurationSettingPublisher((configName, configSetter) =>
                {
                    configSetter(RoleEnvironment.GetConfigurationSettingValue(configName));
                });
                // Each render role has own log file
                m_log = new RenderLog("renderlog" + Utils.RoleID().ToString());

                initQueue();
                initBlob();
                initDisk();
                m_log.Info("OnStart successfull");
                m_engine = new LuxEngine(m_log);
                m_engine.Init();
            }
            catch
            {
                m_log.Error("OnStart failed");
                RoleEnvironment.RequestRecycle(); 
                Trace.TraceWarning("Method OnStart() of WorkerRole is failed.");
                return false;
            }

            return base.OnStart();
        }

        private void initQueue()
        {
            try
            {
                m_renderHandler = new MessageQueue<RenderMessage>();
                m_mergerHandler = null;
                m_abortQueue = null;
            }
            catch (Exception ex)
            {
                m_log.Error("InitQueue failed: " + ex.Message);
            }
        }

        private void initBlob()
        {
            renderStorage.Get().CreateContainer(BlobName.SCENE_BLOB, false);
            renderStorage.Get().CreateContainer(BlobName.FILM_BLOB, false);
            renderStorage.Get().CreateContainer(BlobName.DRIVES_BLOB, false);
        }

        private void initDisk()
        {
            LocalResource localCache = RoleEnvironment.GetLocalResource("InstanceDriveCache");
            Microsoft.WindowsAzure.StorageClient.CloudDrive.InitializeCache(localCache.RootPath, localCache.MaximumSizeInMegabytes);

            CloudBlobContainer driveContainer = renderStorage.Get().CreateContainer(BlobName.DRIVES_BLOB, false);
            CloudPageBlob pageBlob = driveContainer.GetPageBlobReference("renderVHD" + RoleEnvironment.CurrentRoleInstance.Id + ".vhd");

            Microsoft.WindowsAzure.CloudStorageAccount StorageClientStorageAccount = Microsoft.WindowsAzure.CloudStorageAccount.FromConfigurationSetting("DataConnectionString");
            Microsoft.WindowsAzure.StorageClient.CloudDrive drive = Microsoft.WindowsAzure.StorageClient.CloudStorageAccountCloudDriveExtensions.CreateCloudDrive(StorageClientStorageAccount, pageBlob.Uri.ToString());

            try
            {
                drive.CreateIfNotExist(localCache.MaximumSizeInMegabytes);
                drive.Mount(0, Microsoft.WindowsAzure.StorageClient.DriveMountOptions.None);
                System.IO.Directory.SetCurrentDirectory(drive.LocalPath);
                m_localDirectory = drive.LocalPath;
            }
            catch (Microsoft.WindowsAzure.StorageClient.CloudDriveException ex)
            {
                m_log.Error("Create disk error: " + ex.Message);
            }
        }

        private bool roleIsFinish()
        {
            return !m_isRendering;
        }

        private void wait()
        {
            Thread.Sleep(1000);
        }

        private void roleEnvironmentChanging(object sender, RoleEnvironmentChangingEventArgs e)
        {
            // If a configuration setting is changing
            if (e.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
            {
                // Set e.Cancel to true to restart this role instance
                e.Cancel = true;
            }
        }

        #endregion

        #region Fields

        LuxEngine m_engine;
        RenderLog m_log;

        MessageQueue<RenderMessage> m_renderHandler;
        MessageQueue<MergerMessage> m_mergerHandler;
        MessageQueue<RenderMessage> m_abortQueue; //queue for abort message by dispatcher
        bool m_isRendering = false;

        Scene m_scene = null;

        int m_sceneID;
        string m_localDirectory;
        bool m_aborted;
        Breaker m_breaker;

        private string m_sceneUri = string.Empty;

        #endregion
    }
}
