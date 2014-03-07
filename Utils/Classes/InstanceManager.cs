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
using System.Timers;
using RenderUtils.QueueMessage;

namespace RenderUtils
{
    public class InstanceManager
    {
        #region Methods
        public static InstanceManager Get(RenderLog log)
        {
            if (m_manager == null)
                m_manager = new InstanceManager(log);
            return m_manager;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>true if deleted successfully</returns>
        public bool Clear()
        {
            try
            {
                lock (m_requestLock)
                {
                    string deploymentInfo = AzureRESTMgmtHelper.GetDeploymentInfo();
                    string svcconfig = AzureRESTMgmtHelper.GetServiceConfig(deploymentInfo);
                    string UpdatedSvcConfig;
                    UpdatedSvcConfig = AzureRESTMgmtHelper.ChangeInstanceCount(svcconfig, DocConstants.MERGER_NAME, DocConstants.InititalInstancesCount);
                    UpdatedSvcConfig = AzureRESTMgmtHelper.ChangeInstanceCount(UpdatedSvcConfig, DocConstants.WORKER_NAME, DocConstants.InititalInstancesCount);
                    AzureRESTMgmtHelper.ChangeConfigFile(UpdatedSvcConfig); //deallocating instances            
                    m_scalePerforming = true;
                    m_instancesTimer.Enabled = true;
                }
            }
            catch (Exception e)
            {
                m_log.Error("Error when deleting instances: " + e.Message);
                return false;
            }
            m_mergerCount = 1;
            m_workerCount = 1;
            m_mergerFreeCount = 1;
            m_workerFreeCount = 1;
            return true;
        }

        public void FreeResources(int workerCount)
        {
            lock (m_requestLock)
            {
                m_workerFreeCount += workerCount;
                ++m_mergerFreeCount;
            }
        }


        /// <summary>
        /// Send request to azure to allocate new worker and merger instances if needed
        /// </summary>
        /// <param name="workerRequestCount">Nunber of required worker instances</param>
        /// <returns>true if operation was a success</returns>
        public bool Require(int workerRequestCount)
        {
            if (DocConstants.Get().ConnectionString == DocConstants.DEBUG_CONNECTION_STRING)
            {
                return true;
            }
            int workerBackupCount = m_workerCount;
            int workerFreeBackupCount = m_workerFreeCount;
            int mergerBackupCount = m_mergerCount;
            try
            {
                lock (m_requestLock)
                {
                    bool changeConf = false;
                    m_log.Info("INSTANCE_MANAGER: getting deployment info");
                    string deploymentInfo = AzureRESTMgmtHelper.GetDeploymentInfo();  
                    string svcconfig = AzureRESTMgmtHelper.GetServiceConfig(deploymentInfo);
                    string UpdatedSvcConfig = svcconfig;
                    if (m_mergerFreeCount < 1)  
                    {
                        //adding merger instance to config
                        ++m_mergerCount;
                        m_log.Info("INSTANCE_MANAGER: requesting new merger. Now mergers count: " + m_mergerCount.ToString());
                        changeConf = true;
                        UpdatedSvcConfig = AzureRESTMgmtHelper.ChangeInstanceCount(svcconfig, DocConstants.MERGER_NAME, m_mergerCount.ToString());
                    }
                    else //there is at least one free merger
                    {
                        --m_mergerFreeCount;
                    }
                    if (m_workerFreeCount < workerRequestCount)
                    {
                        //adding new workers to config
                        int toAllocWorkerCount = workerRequestCount - m_workerFreeCount;
                        m_workerCount += toAllocWorkerCount;
                        m_workerFreeCount = 0;
                        m_log.Info("INSTANCE_MANAGER: requesting new workers. Now workers count: " + m_workerCount.ToString());
                        changeConf = true;
                        UpdatedSvcConfig = AzureRESTMgmtHelper.ChangeInstanceCount(UpdatedSvcConfig, DocConstants.WORKER_NAME, m_workerCount.ToString());
                    }
                    else
                    {
                        m_workerFreeCount -= workerRequestCount;
                    }
                        while (m_scalePerforming) //wait while last request is being performed
                        {
                            System.Threading.Thread.Sleep(10000);
                            continue;
                        }
                        if (changeConf)
                        {
                            m_scalePerforming = true;
                            AzureRESTMgmtHelper.ChangeConfigFile(UpdatedSvcConfig); //allocating instances
                            m_instancesTimer.Enabled = true;
                            m_log.Info("INSTANCE_MANAGER: Timer turned on. Waiting for allocation");
                        }
                }
                return true;
            }
            catch (Exception e)
            {
                m_log.Error("INSTANCE_MANAGER: Error when allocating instances: " + e.Message + " Request was reversed.");
                m_workerCount = workerBackupCount;
                m_workerFreeCount = workerFreeBackupCount;
                if (m_mergerCount != mergerBackupCount)
                    --m_mergerCount;
                else
                    ++m_mergerFreeCount;
                return false;
            }            
        }

        private void scaleFinished(object sender, ElapsedEventArgs b)
        {
            m_scalePerforming = false;
            m_instancesTimer.Enabled = false;
            m_log.Info("INSTANCE_MANAGER: Timer turned off. Scale request finished");
        }

        private void timerElapsed(object sender, ElapsedEventArgs e)
        {
            lock (m_RemoveInstancesLock)
            {
                m_log.Info("TIMER: connection count: " + m_connectionsNumber);
                if (m_connectionsNumber == 0)
                {
                    try
                    {
                        //current deployed instances
                        m_log.Info("deleteInstances: " + m_deleteInstances);
                        m_log.Info("InstanceCount: " + GetWorkerCount());
                        if ((GetWorkerCount() == GetWorkerFreeCount()) && (GetWorkerCount() > 1))
                        {
                            if (m_deleteInstances == true)
                            {
                                try   //remove extra instances
                                {
                                    m_log.Info("Removing extra instances");

                                    if (!Clear())
                                        return;  //try to delete instances at next timer event
                                    m_deleteInstances = false;
                                    MessageQueue<RenderMessage> renderHandler = new MessageQueue<RenderMessage>();
                                    MessageQueue<MergerMessage> mergerHandler = new MessageQueue<MergerMessage>();
                                    mergerHandler.Clear();
                                    renderHandler.Clear();
                                    m_log.Info("Instances removed");
                                    renderStorage.Get().ClearContainer(BlobName.IMAGE_BLOB);
                                    m_log.Info("Container 'imagegallery' was cleaned");
                                }
                                catch (System.Exception ex)
                                {
                                    m_log.Error("Error occures while deallocation instances : " + ex.Message);
                                    return;
                                }
                            }
                            else
                            {
                                m_log.Info("Instances market to delete");
                                m_deleteInstances = true;
                            }
                        }
                        else
                        {
                            m_deleteInstances = false;
                            m_log.Info("Instances count is not going to be reduced");
                        }
                        m_log.Info("Leaving timer event");
                    }
                    catch (System.Exception ex)
                    {
                        m_log.Error("Error requesting azure: " + ex.Message);
                    }
                }
                else
                    m_deleteInstances = false;
            }
        }

        public void WaitForMerger(MessageQueue<DispetcherMessage> dispatcherHandler, int sceneID, LoopAction action)
        {
            DispetcherMessage dispMessage = dispatcherHandler.WaitForMessage(typeof(MergerIsReady), null, action);
            MergerIsReady go = dispMessage as MergerIsReady;
            if (go.MergerQueueSuffix != sceneID.ToString())  //check for thread error
            {
                m_log.Error("Unknown error while waiting for 'MergerIsReady' message. ID: " + sceneID.ToString());
                throw (new Exception("Unknown error while waiting for 'MergerIsReady' message"));
            }
        }

        public void WaitForWorkers(int workersCount, MessageQueue<DispetcherMessage> dispatcherHandler,
            string sessionGuid, int sceneID, LoopAction action)
        {
            MessageQueue<RenderMessage> renderHandler = new MessageQueue<RenderMessage>();
            for (int i = 0; i < workersCount; ++i)
            {
                IsFreeMessage isFreeMes = new IsFreeMessage(sessionGuid, sceneID); //send check message to worker                
                renderHandler.AddMessage(isFreeMes);
            }
            int workersReadyCount = 0;
            while (workersReadyCount < workersCount)
            {
                DispetcherMessage dispMessage = dispatcherHandler.WaitForMessage(typeof(WorkerIsReady), null, action);
                ++workersReadyCount;
            }
        }

        InstanceManager(RenderLog log)
        {
            m_log = log;
            m_log.Info("INSTANCE_MANAGER: Initializing instance manager");
            m_requestLock = new Object();
            //timer initialization
            m_instancesTimer = new System.Timers.Timer(DocConstants.WAIT_AFTER_SCALE_REQUEST);
            m_instancesTimer.Elapsed += new ElapsedEventHandler(scaleFinished);
            m_instancesTimer.Enabled = false;
            m_timer = new System.Timers.Timer(RenderUtils.DocConstants.INSTANCES_REMOVING_INTERVAL);
            m_timer.Elapsed += new ElapsedEventHandler(timerElapsed);
            m_timer.Enabled = true;
            if (DocConstants.Get().ConnectionString != DocConstants.DEBUG_CONNECTION_STRING)
            {
                string deploymentInfo = AzureRESTMgmtHelper.GetDeploymentInfo();
                string svcconfig = AzureRESTMgmtHelper.GetServiceConfig(deploymentInfo); //get azure config XML
                Int32.TryParse(AzureRESTMgmtHelper.GetInstanceCount(svcconfig, DocConstants.WORKER_NAME), out m_workerCount); //get number of 
                if (m_workerCount <= 0)
                {
                    m_log.Warning("INSTANCE_MANAGER: Azure returned worker instance number as: " + m_workerCount);
                    m_workerCount = 1;
                }
                else
                    m_log.Info("INSTANCE_MANAGER: initial worker count is: " + m_workerCount);
                Int32.TryParse(AzureRESTMgmtHelper.GetInstanceCount(svcconfig, DocConstants.MERGER_NAME), out m_mergerCount);
                if (m_mergerCount <= 0)
                {
                    m_log.Warning("INSTANCE_MANAGER: Azure returned merger instance number as: " + m_workerCount);
                    m_mergerCount = 1;
                }
                else
                    m_log.Info("INSTANCE_MANAGER: initial merger count is: " + m_mergerCount);

                m_workerFreeCount = m_workerCount;
                m_mergerFreeCount = m_mergerCount;
            }
            m_scalePerforming = false;
        }

        public int GetWorkerCount()
        {
            return m_workerCount;
        }

        public int GetWorkerFreeCount()
        {
            return m_workerFreeCount;
        }

        public void IncreaseFreeMergerCount(int num = 1)
        {
            lock (m_requestLock)
            {
                m_mergerFreeCount += num;
            }
        }

        public void IncreaseFreeWorkerCount(int num)
        {
            lock (m_requestLock)
            {
                m_workerFreeCount += num;
            }
        }

        public void IncreaseConnectionsNumber()
        {
            lock (m_RemoveInstancesLock)
            {
                ++m_connectionsNumber;
            }
            m_log.Info("Connections number: " + m_connectionsNumber);
        }

        public void ReduceConnectionsNumber()
        {
            lock (m_RemoveInstancesLock)
            {
                --m_connectionsNumber;
            }
            m_log.Info("Connections number: " + m_connectionsNumber);
        }

        #endregion

        #region Private fields
        int m_mergerCount;
        int m_workerCount;
        int m_mergerFreeCount;
        int m_workerFreeCount;
        bool m_scalePerforming;   //if false => azure performing scale and new scale request is unable
        bool m_deleteInstances = false;   //flag for instance removing
        int m_connectionsNumber = 0;
        static InstanceManager m_manager = null;
        Object m_requestLock;
        Object m_RemoveInstancesLock = new Object();    //connections count lock
        RenderLog m_log;
        System.Timers.Timer m_instancesTimer;   //show if scale is performing
        System.Timers.Timer m_timer;      //timer for checking if instances should be removed

        #endregion

    }

}
