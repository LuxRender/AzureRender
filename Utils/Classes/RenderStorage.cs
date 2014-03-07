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
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;

namespace RenderUtils
{
    public class renderStorage
    {
        public static renderStorage Get()
        {
            if (m_renderStorageInstance == null)
                m_renderStorageInstance = new renderStorage(DocConstants.Get().ConnectionString);
            return m_renderStorageInstance;
        }

        private static renderStorage m_renderStorageInstance = null;

        private renderStorage(string connectionString)
        {
            try
            {
                m_storageAccount = CloudStorageAccount.Parse(connectionString);
                m_blobClient = m_storageAccount.CreateCloudBlobClient();
            }
            catch(Exception e)
            {
                System.Diagnostics.Trace.TraceWarning("Cannot get access to Azure storage:\n" + e.Message);
                m_storageAccount = null;
            }
        }

        public CloudBlobContainer CreateContainer(string containerName, bool clear, BlobContainerPermissions permissions = null)
        {
            if (m_storageAccount == null || m_blobClient == null)
                return null;
            lock (m_queueLock)
            {
                try
                {
                    CloudBlobContainer container = m_blobClient.GetContainerReference(containerName);
                    BlobRequestOptions options = new BlobRequestOptions();
                    bool isCreate = container.CreateIfNotExists();
                    if (false == isCreate && clear)
                    {
                        foreach (ICloudBlob blobItem in container.ListBlobs(null, false, BlobListingDetails.None, options))
                        {
                            blobItem.DeleteIfExists();
                        }
                    }
                    if (permissions != null)
                        container.SetPermissions(permissions);
                    return container;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.TraceWarning("Cannot create conatiner:\n" + e.Message);
                    return null;
                }
            }
        }

        public CloudBlockBlob CreateBlob(string containerName, string blobName, bool textBlob = false)
        {
            if (m_storageAccount == null || m_blobClient == null)
                return null;
            lock (m_queueLock)
            {
                CloudBlockBlob blob = null;
                try
                {
                    CloudBlobContainer blobContainer = m_blobClient.GetContainerReference(containerName);
                    blobContainer.CreateIfNotExists();
                    blob = blobContainer.GetBlockBlobReference(blobName);
                    if (textBlob)
                        Utils.UpploadBlobText(blob, "");
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.TraceWarning("Cannot create blob:\n" + e.Message);
                    return null;
                }
                return blob;
            }
        }

        public void DeleteBlob(string containerName, string partOfBlobName)
        {
            if (m_storageAccount == null || m_blobClient == null)
                return;
            lock (m_queueLock)
            {
                try
                {
                    CloudBlobContainer blobContainer = m_blobClient.GetContainerReference(containerName);
                    BlobRequestOptions options = new BlobRequestOptions();
                    bool isCreate = blobContainer.CreateIfNotExists();
                    if (false == isCreate)
                    {
                        foreach (CloudBlockBlob blobItem in blobContainer.ListBlobs(null, false, BlobListingDetails.None, options))
                        {
                            string blobFullName = blobItem.Uri.ToString();
                            if (true == blobFullName.Contains(partOfBlobName))
                            {
                                blobItem.DeleteIfExists();
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.TraceWarning("Cannot create blob:\n" + e.Message);
                }
            }
        }

        public void ClearContainer(string containerName)
        {
            if (m_storageAccount == null || m_blobClient == null)
                return;
            lock (m_queueLock)
            {
                CloudBlobContainer container = m_blobClient.GetContainerReference(containerName);
                BlobRequestOptions options = new BlobRequestOptions();
                bool isCreate = container.CreateIfNotExists();
                if (false == isCreate)
                {
                    foreach (ICloudBlob blobItem in container.ListBlobs(null, false, BlobListingDetails.None, options))
                    {
                        blobItem.DeleteIfExists();
                    }
                }
            }
        }


        public void SetPermissions(string containerName, BlobContainerPermissions permissions)
        {
            lock (m_queueLock)
            {
                CreateContainer(containerName, false, permissions);
            }
        }

        public BlobContainerPermissions GetPermissions(string containerName)
        {       
            lock (m_queueLock)
            {
                CloudBlobContainer container = CreateContainer(containerName, false);
                return container.GetPermissions();
            }
        }

        public CloudQueue GetOrCreateQueue(string queueName)
        {
            if (m_storageAccount == null || m_blobClient == null)
                return null;

            CloudQueue queue = null;
            try
            {
                lock (m_queueLock)
                {
                    CloudQueueClient queueClient = m_storageAccount.CreateCloudQueueClient();
                    queue = queueClient.GetQueueReference(queueName.ToLower()); // Queue name should be lower case
                    queue.CreateIfNotExists();
                }
            }
            catch(Exception e)
            {
                System.Diagnostics.Trace.TraceWarning("Cannot create queue:\n" + e.Message);
                return null;
            }

            return queue;
        }

        private CloudStorageAccount m_storageAccount = null;
        private CloudBlobClient m_blobClient = null;
        private Object m_queueLock = new Object();
    }
}
