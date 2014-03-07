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
using System.Net.Sockets;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using RenderUtils;
using NetworkMessage;
using RenderUtils.QueueMessage;
using RenderUtils.StorageManager;
using System.IO;
using System.Text.RegularExpressions;
using System.Security;
using System.Timers;

namespace Dispatcher
{
    public class DispatcherRole : RoleEntryPoint
    {
        #region Methods

        public override void Run()
        {
            Trace.WriteLine("Starting echo server...", "Information");

            TcpListener listener = null;
            try
            {
                listener = new TcpListener(
                    RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["InPort"].IPEndpoint);
                listener.ExclusiveAddressUse = false;
                listener.Start();
            }
            catch (SocketException se)
            {
                Trace.Write("Server could not start.", se.Message);
                return;
            }

            while (true)
            {
                IAsyncResult result = listener.BeginAcceptTcpClient(handleAsyncConnection, listener);
                m_connectionWaitHandle.WaitOne();
            }
        }

        private void handleAsyncConnection(IAsyncResult result)
        {
            // Accept connection
            m_instanceManager.IncreaseConnectionsNumber();
            TcpListener listener = (TcpListener)result.AsyncState;
            using (TcpClient client = listener.EndAcceptTcpClient(result))
            {
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                processConnection(client);
            }
            m_instanceManager.ReduceConnectionsNumber();
        }

        private void processConnection(TcpClient client)
        {
            m_connectionWaitHandle.Set();   //unblock listener thread

            string sessionID = Guid.NewGuid().ToString();
            m_log.Info("Thread GUID: " + sessionID);
            lock (m_IDLock)
            {
                m_threadsID.Add(sessionID, m_scenesId.GetNewSceneID());
            }
            MessageQueue<DispetcherMessage> dispatcherHandler = new MessageQueue<DispetcherMessage>(m_threadsID[sessionID].ToString(), true);
            m_log.Info("Start");

            //get scene info
            NetMessageHandler handler = new NetMessageHandler(client, 30);
            NetMessage message = null;

            m_connectionHandlers.Add(sessionID, handler);

            while (true)
            {
                try
                {
                    message = handler.GetSynchMessage();

                    if (message == null)
                        continue;
                }
                catch (Exception)
                {
                    sendError(handler, "Can't read connection message");
                    return;
                }

                SceneMessage sceneMessage;
                bool userAuthorized = false;

                //User authorization
                if (message is AuthorizationMessage)
                {
                    var loginMessage = message as AuthorizationMessage;

                    if (Utils.DataBaseUtils.CheckLoginCorrect(loginMessage.Login, loginMessage.Pwd) == false)
                    {
                        m_log.Info("Wrong password. ID: " + m_threadsID[sessionID].ToString());
                        sendAuthorizationResultMessage(false, handler);
                        return;
                    }
                    else
                    {
                        m_log.Info("User had been authorized. ID: " + m_threadsID[sessionID].ToString());
                        userAuthorized = true;
                        sendAuthorizationResultMessage(true, handler);
                    }
                }
                else if (message is SceneMessage)
                {
                    if (!userAuthorized)
                        m_log.Warning("User not authorized.");

                    sceneMessage = message as SceneMessage;

                    try
                    {
                        if (sceneMessage.RequiredSpp == 0 && sceneMessage.TotalTime < DocConstants.MINIMUM_RENDER_TIME)
                        {
                            m_log.Warning("Minimum render time is 60 sec");
                            sendError(handler, "Minimum render time is 60 sec");
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Error("Unexpected problem when checking password: " + e.ToString() + ". ID: " + m_threadsID[sessionID].ToString());
                        sendError(handler, "Error requesting database");
                        return;
                    }
                    //get scene file
                    byte[] sceneFile = null;
                    try
                    {
                        sceneFile = new byte[sceneMessage.SceneFileSize];
                        handler.GetFile(sceneFile);
                    }
                    catch (Exception ex)
                    {
                        m_log.Error("Error uploading scene" + ex.Message);
                        sendError(handler, "Error uploading scene");
                        return;
                    }

                    if (!m_instanceManager.Require(sceneMessage.InstanceCnt)) //gives new worker and merger
                    //instances if needed                                                                                       
                    {
                        sendError(handler, "Error while allocating instances. Try again later");
                        return;
                    }
                    try
                    {
                        sendClientMessage(handler, StatusMessage.StatusEnum.OK);
                    }
                    catch (Exception e)
                    {
                        m_log.Error("Error sending sync message" + e.Message);
                    }
                    try
                    {
                        m_log.Info("ProcessingScene. ID: " + m_threadsID[sessionID].ToString());
                        processingScene(handler, sceneMessage, sceneFile, sessionID, dispatcherHandler);
                        m_log.Info("/ProcessingScene. ID: " + m_threadsID[sessionID].ToString());
                        m_log.Info("WaitingProcess. ID: " + m_threadsID[sessionID].ToString());
                        waitingProcess(handler, sessionID, sceneMessage, dispatcherHandler);
                        m_log.Info("/WaitingProcess. ID: " + m_threadsID[sessionID].ToString());
                    }
                    catch (Exception ex)
                    {
                        isDisconnected(handler, sessionID);
                        sendError(handler, ex.Message);
                        m_log.Error("Processing error: " + ex.Message + ". ID: " + m_threadsID[sessionID].ToString());
                    }
                    m_log.Info("Workers had finished. ID: " + m_threadsID[sessionID].ToString());
                    //free resources
                    try
                    {
                        m_threadsID.Remove(sessionID);
                        m_instanceManager.FreeResources(sceneMessage.InstanceCnt);
                    }
                    catch (Exception e)
                    {
                        m_log.Error("Error when freeing resources" + e.Message);
                    }
                    m_log.Info("resources was freed. ");
                    m_log.Info("sceneMessage.InstanceCount : " + sceneMessage.InstanceCnt);
                    m_log.Info("Finish");
                    return;
                }
                else
                {
                    m_log.Info("message type: " + message.ToString());
                    m_log.Warning("SceneInfo: Incorrect or damaged scene file.");
                    sendError(handler, Resources.MEESAGE_INCORRECT_SCENE);
                    return;
                }
            }
        }

        private void sendAuthorizationResultMessage(bool isAuthorized, NetMessageHandler handler)
        {
            try
            {
                handler.SendSynchMessage(new AuthorizationResultMessage(isAuthorized));
            }
            catch (Exception)
            {
                m_log.Error("Error when sending authorization message");
            }
        }

        private void processingScene(NetMessageHandler handler, SceneMessage message, byte[] sceneFile, string sessionGuid,
            MessageQueue<DispetcherMessage> dispatcherHandler)
        {
            string sceneName = null;
            int threadID = m_threadsID[sessionGuid];
            string uniqueBlobName = string.Format("{0}_{1}", BlobName.UNIQUE_BLOB + threadID.ToString(), sessionGuid);
            try
            {
                lock (m_BlobLock)
                {
                    CloudBlockBlob blob = renderStorage.Get().CreateBlob(BlobName.SCENE_BLOB, uniqueBlobName);//sceneContainer.GetBlockBlobReference(uniqueBlobName);
                    Utils.UpploadBlobByteArray(blob, sceneFile);
                    sceneName = blob.Uri.ToString();
                }
            }
            catch (Exception e)
            {
                m_log.Error("Error saving scene to blob: " + e.Message);
                m_instanceManager.IncreaseFreeMergerCount(); //if no blob created than mark requested instances as free
                                                             //and kill thread
                m_instanceManager.IncreaseFreeWorkerCount(message.InstanceCnt);
                Thread.CurrentThread.Abort();
            }

            StartMergeMessage mergerMessage = new StartMergeMessage(message.InstanceCnt, message.TotalTime,
                message.UpdateTime, sessionGuid, threadID, message.RequiredSpp);
            m_mergerHandler.AddMessage(mergerMessage);
            m_log.Info("IsMergerReady message sent");
            WaitForWorkerAction action = new WaitForWorkerAction(sendClientMessage, handler);
            lock (m_waitMergerLock)
            {
                //wait for merger allocating
                InstanceManager.Get(m_log).WaitForMerger(dispatcherHandler, m_threadsID[sessionGuid], action); 
            }
            lock (m_waitWorkerLock)
            {
                //check if workers has been already allocated, if no wait for it
                InstanceManager.Get(m_log).WaitForWorkers(message.InstanceCnt, dispatcherHandler, sessionGuid, threadID, action);
            }
            sendClientMessage(handler, StatusMessage.StatusEnum.Allocated);

            m_log.Info("StartMergeMessage sent to merger. ID: " + threadID);
            m_log.Info("Scene GUID: " + sessionGuid + "Scene ID: " + threadID);
            m_renderAbortHandlers.Add(threadID, new List<MessageQueue<RenderMessage>>()); //each m_renderAbortHandlers contains list of worker queues 
            MessageQueue<RenderMessage> toRenderQueue = new MessageQueue<RenderMessage>(threadID.ToString());
            double sppPerOneWorker = 0; //how much spp is required from each worker
            for (int i = 0; i < message.InstanceCnt; i++)
            {
                //adding new renderqueue for list that correspond to ID of scene which is connected with m_threadsID[sessionGuid]
                try
                {
                    MessageQueue<RenderMessage> newAbortQueue = new MessageQueue<RenderMessage>(threadID.ToString() + m_renderAbortHandlers[threadID].Count.ToString());
                    newAbortQueue.Clear();
                    m_renderAbortHandlers[threadID].Add(newAbortQueue);
                }
                catch (Exception e)
                {
                    m_log.Error("Error creating queue: " + e.Message + ". ID: " + threadID);
                }
                sppPerOneWorker = message.RequiredSpp / message.InstanceCnt;
                ToRenderMessage msg = new ToRenderMessage(sceneName, message.TotalTime,
                    message.UpdateTime, threadID, threadID.ToString() +
                    (m_renderAbortHandlers[threadID].Count - 1).ToString(), //scene ID + number of worker that is rendering this scene
                    sppPerOneWorker);
                m_log.Info("Abort queue name suffix: " + (threadID.ToString() + (m_renderAbortHandlers[threadID].Count - 1)).ToString());
                toRenderQueue.AddMessage(msg);
                m_log.Info("ToRenderMessage sent to worker: " + i);
            }
            m_log.Info("ToRenderMessage sent to workers. ID: " + threadID);
        }



        private void waitingProcess(NetMessageHandler handler, string sessionId, SceneMessage sceneMessage,
            MessageQueue<DispetcherMessage> dispatcherHandler)
        {
            DispatcherExitCondition exitCondition = new DispatcherExitCondition(handler, sessionId, isDisconnected);
            QueueListener<DispetcherMessage> queueListener = new QueueListener<DispetcherMessage>(dispatcherHandler, m_log);
            queueListener.AddResponse("MergerFinishMessage", mergerFinishMessageEvent);
            queueListener.AddResponse("MergerUpdateMessage", mergerUpdateMessageEvent);
            queueListener.AddResponse("MergerUpdateFailedMessage", mergerUpdateFailedMessageEvent);
            queueListener.Run(exitCondition);
        }

        private bool mergerUpdateFailedMessageEvent(DispetcherMessage dispMessage)
        {
            m_log.Info("MergerUpdateFailedMessage. ID: " + m_threadsID[dispMessage.SessionId].ToString());
            MergerUpdateFailedMessage errMessage = dispMessage as MergerUpdateFailedMessage;
            sendError(m_connectionHandlers[dispMessage.SessionId], errMessage.ErrorMessage);
            if (m_renderAbortHandlers.ContainsKey(m_threadsID[dispMessage.SessionId]))
            {
                m_renderAbortHandlers.Remove(m_threadsID[dispMessage.SessionId]);
            }
            m_log.Info("/MergerUpdateFailedMessage");
            return true;
        }

        private bool mergerUpdateMessageEvent(DispetcherMessage dispMessage)
        {
            try
            {
                m_log.Info("MergerUpdateMessage. ID: " + m_threadsID[dispMessage.SessionId].ToString());
                MergerUpdateMessage mergMessage = dispMessage as MergerUpdateMessage;
                CloudBlockBlob imageBlob;
                lock (m_BlobLock)
                {
                    imageBlob = renderStorage.Get().CreateBlob(BlobName.IMAGE_BLOB, mergMessage.ImagePath);
                }

                m_log.Info("Merger completed percent: " + mergMessage.PercentageCompleted);
                float percentageCompleted = Math.Min(mergMessage.PercentageCompleted, 100);
                percentageCompleted = (float)Math.Round(percentageCompleted, 2, MidpointRounding.AwayFromZero);
                m_log.Info("Percentage completed: " + percentageCompleted.ToString());
                m_log.Info("Merger completed spp: " + mergMessage.SPP.ToString());

                m_log.Info("Fetching image. ID: " + m_threadsID[dispMessage.SessionId].ToString());
                byte[] image = Utils.DownloadBlobToArray(imageBlob);
                m_log.Info("Deleting image from blob");

                double spp = 0;
                if ((mergMessage.RequiredSpp != 0) && (mergMessage.SPP > mergMessage.RequiredSpp))
                    spp = mergMessage.RequiredSpp;
                else
                    spp = mergMessage.SPP;

                ImageMessage pngMes = new ImageMessage(image.Length, percentageCompleted, spp);
                m_log.Info("Sending message to client. ID: " + m_threadsID[dispMessage.SessionId].ToString());
                m_connectionHandlers[dispMessage.SessionId].SendSynchMessage(pngMes);
                m_log.Info("Sending picture to client. ID: " + m_threadsID[dispMessage.SessionId].ToString());
                m_connectionHandlers[dispMessage.SessionId].SendFile(image);
                m_log.Info("/MergerUpdateMessage. ID: " + m_threadsID[dispMessage.SessionId].ToString());
            }
            catch (Exception ex)
            {
                m_log.Error("MergerUpdateMessage ex: " + ex.Message);
            }
            return false;
        }

        private bool mergerFinishMessageEvent(DispetcherMessage dispMessage)
        {
            try
            {
                MergerFinishMessage mergMessage = dispMessage as MergerFinishMessage;
                if (!mergMessage.IsError)
                    m_connectionHandlers[dispMessage.SessionId].SendSynchMessage(new RenderFinishMessage(mergMessage.FlmUri, mergMessage.SPP));
                else
                    sendError(m_connectionHandlers[dispMessage.SessionId], "Error occurred on worker. Likely scene is damaged");
                m_log.Info("SPP: " + mergMessage.SPP.ToString());
            }
            catch (Exception e)
            {
                m_log.Warning("Error sending sync message. " + e.Message);
            }
            if (m_renderAbortHandlers.ContainsKey(m_threadsID[dispMessage.SessionId]))
            {
                m_renderAbortHandlers.Remove(m_threadsID[dispMessage.SessionId]);
            }
            MessageQueue<DispetcherMessage> dispatcherHandler = new MessageQueue<DispetcherMessage>(m_threadsID[dispMessage.SessionId].ToString());
            dispatcherHandler.Delete();
            m_log.Info("/MergerFinishMessage.");
            return true;
        }

        private void sendError(NetMessageHandler handler, string message)
        {
            try
            {
                StatusMessage errMes = new StatusMessage(StatusMessage.StatusEnum.Failed, message);
                handler.SendSynchMessage(errMes);
            }
            catch (Exception)
            {
                m_log.Error("Error when sending error message");
            }
        }

        private void sendClientMessage(NetMessageHandler handler, StatusMessage.StatusEnum status)
        {
            try
            {
                StatusMessage msg = new StatusMessage(status);
                handler.SendSynchMessage(msg);      //send message to client to mantain a connection while waiting
            }
            catch (Exception e)
            {
                m_log.Warning("Error sending sync message. " + e.Message);
            }
        }

        public override bool OnStart()
        {
            try
            {
                ServicePointManager.DefaultConnectionLimit = 12;

                // For information on handling configuration changes
                // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
                RoleEnvironment.Changing += roleEnvironmentChanging;
                m_threadsID = new Dictionary<string, int>();
                m_renderAbortHandlers = new Dictionary<int, List<MessageQueue<RenderMessage>>>();
                m_connectionHandlers = new Dictionary<string, NetMessageHandler>();
                m_log = new RenderLog("dispatcherlog");

                m_log.Info("storageAccount created");
                initQueue();
                m_log.Info("Queue initialized");
                initBlob();
                m_log.Info("Blob initialized");
                m_log.Info("RolesCnt initialized");
                m_scenesId = SceneID.Get();
                m_instanceManager = InstanceManager.Get(m_log);
                m_log.Info("OnStart completed");
            }
            catch (Exception ex)
            {
                RoleEnvironment.RequestRecycle();    //request role restart
                System.Diagnostics.Trace.TraceWarning("Method OnStart() of WebRole is failed.");
                m_log.Error("Error when initializing: " + ex.Message);
                return false;
            }

            return base.OnStart();
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

        private void initQueue()
        {
            try
            {
                m_log.Info("Queue initializing");
                m_renderHandler = new MessageQueue<RenderMessage>();
                m_mergerHandler = new MessageQueue<MergerMessage>();
            }
            catch (Exception e)
            {
                m_log.Error("InitQueue failed: " + e.Message);
            }
            m_mergerHandler.Clear();
            m_renderHandler.Clear();
        }

        private void initBlob()
        {
            BlobContainerPermissions permissions = new BlobContainerPermissions();
            permissions.PublicAccess = BlobContainerPublicAccessType.Container;

            CloudBlobContainer sceneContainer = renderStorage.Get().CreateContainer(BlobName.SCENE_BLOB, true, permissions);
            CloudBlobContainer filmContainer = renderStorage.Get().CreateContainer(BlobName.FILM_BLOB, true, permissions);
            CloudBlobContainer imageContainer = renderStorage.Get().CreateContainer(BlobName.IMAGE_BLOB, true, permissions);
            CloudBlobContainer driveContainer = renderStorage.Get().CreateContainer(BlobName.DRIVES_BLOB, true, permissions);
        }

        //check if abort message was send by client
        private bool isDisconnected(NetMessageHandler handler, string sessionId)
        {
            sendClientMessage(handler, StatusMessage.StatusEnum.OK);

            StatusMessage statusMess = handler.GetSynchMessage() as StatusMessage;
            if (((statusMess != null) && (statusMess.Status == StatusMessage.StatusEnum.Aborted)) || (!handler.Connected()))
            {
                try
                {
                    m_log.Info("Got abort message or connection was disconnected. ID: " + m_threadsID[sessionId].ToString());
                    //sending abort messeges to workers
                    for (int i = 0; i < m_renderAbortHandlers[m_threadsID[sessionId]].Count; ++i)
                    {
                        try
                        {
                            m_renderAbortHandlers[m_threadsID[sessionId]][i].AddMessage(new ToAbortRenderMessage());
                        }
                        catch (Exception e)
                        {
                            m_log.Warning("renderQueue was already deleted. Error: " + m_renderAbortHandlers[m_threadsID[sessionId]][i].ToString() + ". ID: " + m_threadsID[sessionId].ToString());
                            m_log.Warning(e.Message);
                        }
                    }
                    MessageQueue<MergerMessage> abortQueue = new MessageQueue<MergerMessage>(m_threadsID[sessionId].ToString() + "abort");
                    AbortMergeMessage mergerMessage = new AbortMergeMessage("uniqueBlob" + m_threadsID[sessionId].ToString() + '_' + sessionId.ToString(), m_threadsID[sessionId]);
                    abortQueue.AddMessage(mergerMessage);
                    m_renderAbortHandlers.Remove(m_threadsID[sessionId]);
                    m_log.Info("Aborted by user. ID: " + m_threadsID[sessionId].ToString());
                    return true;
                }
                catch (Exception e)
                {
                    m_log.Error("Error for ID " + m_threadsID[sessionId] + " when checking connection status:" + e.Message);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public class DispatcherExitCondition : ExitConditions
        {
            public DispatcherExitCondition(NetMessageHandler connectionHandler, string sessionId, DispatcherCondition exitCondition = null)
            {
                m_exitCondition = exitCondition;
                m_connectionHandler = connectionHandler;
                m_sessionId = sessionId;
            }

            public bool IsExit()
            {
                bool isExit = false;
                isExit = (m_exitCondition != null) && m_exitCondition(m_connectionHandler, m_sessionId);
                return isExit;
            }

            public delegate bool DispatcherCondition(NetMessageHandler handler, string sessionId);
            private DispatcherCondition m_exitCondition;
            private NetMessageHandler m_connectionHandler;
            string m_sessionId;
        }

        public class WaitForWorkerAction : LoopAction
        {
            public WaitForWorkerAction(PerformedAction action, NetMessageHandler handler)
            {
                m_action = action;
                m_handler = handler;
            }
            public void Execute()
            {
                m_action(m_handler, StatusMessage.StatusEnum.OK);
            }
            private PerformedAction m_action;
            private NetMessageHandler m_handler;
            public delegate void PerformedAction(NetMessageHandler handler, StatusMessage.StatusEnum status);
        }

        #endregion

        #region Private fields

        private AutoResetEvent m_connectionWaitHandle = new AutoResetEvent(false);

        SceneID m_scenesId;
        Object m_QueueLock = new Object();
        Object m_BlobLock = new Object();
        Object m_IDLock = new Object();
        Object m_waitMergerLock = new Object();
        Object m_waitWorkerLock = new Object();

        RenderLog m_log;

        Dictionary<string, int> m_threadsID;   //connect dispatcher threads GUID with scenes id
        Dictionary<string, NetMessageHandler> m_connectionHandlers;
        Dictionary<int, List<MessageQueue<RenderMessage>>> m_renderAbortHandlers;  //list of queues which are listened by worker for    
        //abort message
        MessageQueue<RenderMessage> m_renderHandler;
        MessageQueue<MergerMessage> m_mergerHandler;

        InstanceManager m_instanceManager;
        #endregion
    }
}
