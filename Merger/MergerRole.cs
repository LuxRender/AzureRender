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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Data.SqlClient;
using RenderUtils;
using NetworkMessage;
using RenderUtils.QueueMessage;
using RenderUtils.StorageManager;
using System.Timers;


namespace Merger
{
    public class MergerRole : RoleEntryPoint
    {
        public class MergerExitCondition : ExitConditions
        {
            public MergerExitCondition(Condition simpleExitCondition = null)
            {
                m_simpleExitCondition = simpleExitCondition;
            }

            public bool IsExit()
            {
                return (m_simpleExitCondition != null) && m_simpleExitCondition();
            }

            public delegate bool Condition();
            Condition m_simpleExitCondition;
        }

        #region Methods

        public override void Run()
        {
            try
            {
                Trace.TraceInformation("Listening for merger queue messages...");
                m_log.Info("Listening for merger queue messages...");
                m_makePicStopwatch = new Stopwatch();
                m_makePicStopwatch.Start();
                while (true)    //run process
                {
                    prepareMerger();
                    QueueListener<MergerMessage> queueListener = 
                        new QueueListener<MergerMessage>(m_instanceQueue, m_log);
                    queueListener.AddResponse("ToMergeMessage", toMergeMessageEvent);
                    queueListener.AddResponse("RenderingFinishMessage", renderingFinishMessageEvent);
                    MergerExitCondition exitCondition = new MergerExitCondition(isFinish);
                    queueListener.Run(exitCondition);
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Exception when processing queue item. Message: ", e.Message);
                m_log.Error("Exception when processing queue item. Message: " + e.Message);
            }
        }

        private void prepareMerger()
        {
            MergerMessage merMessage = m_mergerHandler.WaitForMessage(typeof(StartMergeMessage));//(StartMergeMessage));
            m_log.Info("StartMergeMessage");
            m_engine.Cleanup();  //make engine ready and set samples number to 0
            m_maxThreadsCount = Environment.ProcessorCount * 2;
            m_threadsCount = 0;
            StartMergeMessage startMergeMes = merMessage as StartMergeMessage;
            m_scene = new Utils.SceneBlobReferenceMessage(startMergeMes, false, startMergeMes.SceneId);
            m_log.Info("Scene ID: " + startMergeMes.SceneId);
            if (startMergeMes.RenderTime != 0)
            {
                m_totalRenderTime = startMergeMes.RenderTime * startMergeMes.RenderRoleCnt; //total time of rendering
                m_oneBatchInPercents = (float)(startMergeMes.UpdateTime * 100.0) / (float)m_totalRenderTime; //one worker update in percents
            }
            if (startMergeMes.RequiredSpp != 0)
            {
                m_stopBySpp = true;
            }
            m_requiredSpp = startMergeMes.RequiredSpp;
            m_log.Info("One worker update = " + m_oneBatchInPercents + "%");
            m_percentageCompleted = 0; //rendered part of scene in percents
            m_dispatcherHandler = new MessageQueue<DispetcherMessage>(startMergeMes.SceneId.ToString()); //create queue to dispatcher thread
            m_dispatcherQueues.Add(startMergeMes.SceneId, m_dispatcherHandler);
            m_instanceQueue = new MessageQueue<MergerMessage>(startMergeMes.SceneId.ToString(), true);
            m_abortQueue = new MessageQueue<MergerMessage>(startMergeMes.SceneId.ToString() + "abort", true);
            m_log.Info("sending 'I am ready' message");
            m_dispatcherHandler.AddMessage(new MergerIsReady(startMergeMes.SessionId, startMergeMes.SceneId.ToString())); //signal to dispatcher: "Ready to work!"
            m_isMerging = true;
            m_log.Info("/StartMergeMessage");
        }

        /// <summary>
        /// Occurs when merger gets message to merge flm
        /// </summary>
        /// <param name="merMessage">Message from worker</param>
        /// <returns>True is merging process should be ended</returns>
        private bool toMergeMessageEvent(MergerMessage merMessage)
        {
            try
            {
                while (true)
                {
                    if (m_threadsCount < m_maxThreadsCount)
                    {
                        Thread thread = new Thread(() => updateMessageEvent(merMessage));
                        thread.Start();
                        m_log.Info("New thread started. Thread count: " + m_threadsCount.ToString());
                        break;
                    }
                    else
                    {
                        Thread.Sleep(3000);
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                m_log.Error("ToMergeMessage ex: " + ex.Message);
                ToMergeMessage toMergeMessege = merMessage as ToMergeMessage;
                MergerUpdateFailedMessage errMes = new MergerUpdateFailedMessage(ex.Message, m_scene.StartMessage.SessionId);
                m_dispatcherQueues[toMergeMessege.ID].AddMessage(errMes);
                return true;
            }
        }

        /// <summary>
        /// Occurs when merger gets message of render finish
        /// </summary>
        /// <param name="merMessage">Message from worker</param>
        private bool renderingFinishMessageEvent(MergerMessage merMessage)
        {
            RenderingFinishMessage finishMsg = merMessage as RenderingFinishMessage;
            m_log.Info("RenderingFinishMessage.\nScene ID: " + finishMsg.SceneID);
            m_scene.IncrementFinishedRolesNumber();
            m_log.Info("Instances finished: " + m_scene.InstancesFinished + "\nInstances required: " + m_scene.StartMessage.RenderRoleCnt + " ID: " + finishMsg.SceneID);
            if (m_scene.StartMessage.RenderRoleCnt == m_scene.InstancesFinished)
            {
                finishRender(finishMsg);
                cleanup(finishMsg);
                m_log.Info("Merger finished");
                return true;
            }
            else
            {
                m_log.Info("Continue with other instances");
                return false;
            }
        }

        private bool isFinish()
        {
            return !m_isMerging || isAbort();
        }

        private void updateMessageEvent(MergerMessage merMessage)
        {
            m_log.Info("ToMergeMessage. ID: " + m_scene.SceneID.ToString());
            lock (m_downloadFlmLock)
            {
                ++m_threadsCount;
            }
            ToMergeMessage toMergeMessege = merMessage as ToMergeMessage;
            Utils.ThreadLocalSceneBlob threadSceneNode = new Utils.ThreadLocalSceneBlob();
            m_log.Info("InitializeSceneNode. ID: " + m_scene.SceneID.ToString());
            initializeSceneNode(toMergeMessege, threadSceneNode);
            m_log.Info("/InitializeSceneNode. ID: " + m_scene.SceneID.ToString());
            m_log.Info("Merge. ID: " + m_scene.SceneID.ToString());
            merge(threadSceneNode);
            m_log.Info("/Merge. ID: " + m_scene.SceneID.ToString());
            //scene name means .png blob uri
            lock (m_makePicLock)
            {
                if (!m_stopBySpp)
                    m_percentageCompleted += m_oneBatchInPercents;
                if (m_makePicStopwatch.ElapsedMilliseconds > DocConstants.MAKE_PICTURE_HOLDUP)
                {
                    createImage(threadSceneNode.OutputBlob);
                    double spp = m_engine.GetSppOfLoadedFLM();
                    m_log.Info("SPP: " + spp);
                    if (m_stopBySpp)
                        m_percentageCompleted = (float)((spp * 100.0) / m_scene.StartMessage.RequiredSpp);
                    m_log.Info("Completed " + m_percentageCompleted);
                    MergerUpdateMessage updateMsg = new MergerUpdateMessage(threadSceneNode.SceneName,
                        m_scene.StartMessage.SessionId, m_percentageCompleted, spp, m_requiredSpp);
                    m_dispatcherQueues[toMergeMessege.ID].AddMessage(updateMsg);
                    m_makePicStopwatch.Restart();
                }
                --m_threadsCount;
            }
        }

        private void initializeSceneNode(ToMergeMessage toMergeMessege, Utils.ThreadLocalSceneBlob localSceneNode)
        {
            CloudBlockBlob flmBlob = null;
            CloudBlockBlob outputBlob = null;
            string sceneName = null;

            string guid = Guid.NewGuid().ToString();
            sceneName = guid + ".png";

            m_log.Info("Getting container");
            BlobContainerPermissions permissions = renderStorage.Get().GetPermissions(BlobName.FILM_BLOB);
            permissions.PublicAccess = BlobContainerPublicAccessType.Container;
            renderStorage.Get().SetPermissions(BlobName.FILM_BLOB, permissions);

            m_log.Info("Getting blob reference");
            flmBlob = renderStorage.Get().CreateBlob(BlobName.FILM_BLOB, toMergeMessege.Flm);  //here new flm part(rendered by worker) is placed
            outputBlob = renderStorage.Get().CreateBlob(BlobName.IMAGE_BLOB, sceneName);      //image will be stored here
            m_log.Info("/Getting blob reference");

            localSceneNode.FlmBlob = flmBlob;  //new worker's flm file(sent by worker to merge it into main file)
            localSceneNode.OutputBlob = outputBlob;    //image will be stored here
            localSceneNode.SceneName = sceneName;  //new GUID blob
        }

        private void merge(Utils.ThreadLocalSceneBlob currentScene)
        {
            try
            {
                byte[] buffer = Utils.DownloadBlobToArray(currentScene.FlmBlob);
                currentScene.FlmBlob.Delete(); //deleting blob with flm saved by worker
                m_log.Info("FLM downloaded. Blob deleted. Scene ID: " + m_scene.SceneID);
                int size = buffer.Length;
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(buffer, 0, ptr, size);  //copy new flm from buffer to ptr
                lock (m_downloadFlmLock)
                {
                    if (m_engine.Samples == 0)
                    {
                        m_log.Info("Loading FLM to RAM. Scene ID: " + m_scene.SceneID);
                        m_engine.LoadFLMFromStream(ptr, size, currentScene.SceneName);
                        m_engine.UpdateFLMFromStream(ptr, size); //count samples that were added in engine
                    }
                    else
                    {
                        m_log.Info("Updating FLM. Scene ID: " + m_scene.SceneID);
                        m_engine.UpdateFLMFromStream(ptr, size);
                    }
                    Marshal.FreeHGlobal(ptr);
                    m_log.Info("Update frame buffer. Scene ID: " + m_scene.SceneID);
                    m_engine.UpdateFramebuffer();
                }
                m_log.Info("/Update frame buffer. Scene ID: " + m_scene.SceneID);
            }
            catch (Exception ex)
            {
                m_log.Error("Merge error. " + ex.Message);
            }
        }

        /// <summary>
        /// Waits for merging process and sends finish message to dispatcher.
        /// </summary>
        /// <param name="finishMsg">Finish message from worker</param>
        private void finishRender(RenderingFinishMessage finishMsg)
        {
            try
            {
                waitForThreads();   //if at least one thread stuck here whole role will stop work
                                    //but this sceario is very unlikely
                m_log.Info("getting flm URI");
                double spp = -1;
                string completedFlmUri = string.Empty;
                if (!finishMsg.IsError)
                {
                    completedFlmUri = saveFlmToBlob(m_scene.StartMessage.SessionId);
                    if (m_stopBySpp)
                        spp = m_scene.StartMessage.RequiredSpp;
                    else
                        spp = m_engine.GetSppOfLoadedFLM();
                    m_log.Info("Sending message to dispatcher");
                }
                int id = Utils.RoleID();
                MergerFinishMessage msg = new MergerFinishMessage(id, m_scene.StartMessage.SessionId, completedFlmUri, spp,
                    finishMsg.IsError);
                m_dispatcherQueues[finishMsg.SceneID].AddMessage(msg);
            }
            catch (Exception e)
            {
                m_log.Warning("Error on worker. Probably error with scene. " + e.Message);
            }
        }

        private string saveFlmToBlob(string blobName)
        {
            BlobContainerPermissions permissions = renderStorage.Get().GetPermissions(BlobName.FILM_BLOB);
            permissions.PublicAccess = BlobContainerPublicAccessType.Container;
            renderStorage.Get().SetPermissions(BlobName.FILM_BLOB, permissions);
            CloudBlockBlob blob = renderStorage.Get().CreateBlob(BlobName.FILM_BLOB, blobName);
            m_engine.UpdateFramebuffer();
            m_log.Info("Saving flm to stream");
            byte[] flmBuffer = m_engine.SaveFLMToStream();
            if (flmBuffer == null)
                return string.Empty;
            Utils.UpploadBlobByteArray(blob, flmBuffer);
            return blob.Uri.AbsoluteUri;
        }

        private bool isAbort()
        {
            //check for abort message
            MergerMessage abortMessage = m_abortQueue.GetMessageByType(typeof(AbortMergeMessage));
            if (abortMessage != null)
            {
                waitForThreads();
                m_log.Info("RenderingAbortMessage. ID: " + m_abortQueue.getSuffix().ToString());
                AbortMergeMessage abortMes = abortMessage as AbortMergeMessage;
                m_engine.Cleanup();
                if (m_dispatcherQueues.ContainsKey(abortMes.SceneID))
                {
                    m_dispatcherQueues[abortMes.SceneID].Delete();
                    m_dispatcherQueues.Remove(abortMes.SceneID);
                }
                clearSceneContainer(abortMes.SceneName);
                m_isMerging = false;
                m_instanceQueue.Clear();
                m_instanceQueue.Delete();
                m_abortQueue.Delete();
                m_log.Info("/RenderingAbortMessage");
                return true;
            }
            return false;
        }

        //creates image and save it to blob
        private void createImage(CloudBlockBlob blob)
        {
            m_log.Info("Creating bitmap");
            Bitmap frameBitmap = m_engine.GetFramebufferBitmap();

            m_log.Info("Saving");
            using (MemoryStream ms = new MemoryStream())
            {
                frameBitmap.Save(ms, ImageFormat.Jpeg);
                Utils.UpploadBlobByteArray(blob, ms.ToArray());
                frameBitmap.Dispose();
            }
        }

        /// <summary>
        /// Clears all data after merging making it ready for new scene
        /// </summary>
        /// <param name="finishMsg">Render finish message from worker</param>
        private void cleanup(RenderingFinishMessage finishMsg)
        {
            clearSceneContainer(finishMsg.SceneName);
            m_engine.Cleanup();
            m_dispatcherQueues.Remove(finishMsg.SceneID);
            m_isMerging = false;
            m_instanceQueue.Delete();
            m_abortQueue.Delete();
        }

        private void waitForThreads()
        {
            while (m_threadsCount > 0)
                Thread.Sleep(3000);//wait while all merging threads finish
        }

        public override bool OnStart()
        {
            try
            {
                ServicePointManager.DefaultConnectionLimit = 12;

                RoleEnvironment.Changing += roleEnvironmentChanging;

                m_log = new RenderLog("mergerlog");

                m_engine = new LuxEngine(m_log);
                m_engine.Init();

                m_log.Info("storageAccount created");
                m_dispatcherQueues = new Dictionary<int, MessageQueue<DispetcherMessage>>();
                initQueue();
                m_log.Info("Queue initialized");
                initBlob();
                m_log.Info("Blob initialized");
                m_oneBatchInPercents = 0;
                m_totalRenderTime = 0;
                m_percentageCompleted = 0;
                m_log.Info("OnStart successfull");
            }
            catch
            {                
                Trace.TraceWarning("Method OnStart() of MergerRole is failed.");
                m_log.Error("Method OnStart() of MergerRole is failed.");
                RoleEnvironment.RequestRecycle(); 
                return false;
            }            
            return base.OnStart();
        }

        private void initQueue()
        {
            try
            {
                m_mergerHandler = new MessageQueue<MergerMessage>();
            }
            catch (Exception ex)
            {
                m_log.Error("InitQueue failed: " + ex.Message);
            }
        }

        private void initBlob()
        {
            renderStorage.Get().CreateContainer(BlobName.IMAGE_BLOB, false);
            renderStorage.Get().CreateContainer(BlobName.FILM_BLOB, false);
            renderStorage.Get().CreateContainer(BlobName.DRIVES_BLOB, false);
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

        private void clearSceneContainer(string sceneName)
        {
            try
            {
                renderStorage.Get().DeleteBlob(BlobName.SCENE_BLOB, sceneName); 
            }
            catch (Exception ex)
            {
                m_log.Error("ClearSceneContainer error: " + ex.Message);
            }
        }

        #endregion

        #region Private fields

        LuxEngine m_engine;
        RenderLog m_log;
        MessageQueue<MergerMessage> m_mergerHandler;
        MessageQueue<MergerMessage> m_instanceQueue = null; //queue to this merger
        MessageQueue<MergerMessage> m_abortQueue = null;
        //scene data(flm location, etc.)
        Utils.SceneBlobReferenceMessage m_scene;
        Dictionary<int, MessageQueue<DispetcherMessage>> m_dispatcherQueues; //dispatchers connected to scenes IDs

        Object m_downloadFlmLock = new Object();
        Object m_makePicLock = new Object();
        Object m_renderFinishLock = new Object();

        float m_oneBatchInPercents;   //which part of whole scene is rendered by one worker in one update
        int m_totalRenderTime;
        float m_percentageCompleted;
        
        Stopwatch m_makePicStopwatch;
        bool m_stopBySpp = false;
        bool m_isMerging = true; //working with particular scene

        MessageQueue<DispetcherMessage> m_dispatcherHandler;
        double m_requiredSpp;

        int m_maxThreadsCount = 0;
        int m_threadsCount = 0;

        #endregion
    }
}
