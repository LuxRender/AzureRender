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
using System.IO;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;

namespace RenderUtils.StorageManager
{

    /// <summary>
    /// Provides methods to work with Azure blobs
    /// </summary>
    public class BlobFileHandler : StorageManager.IBlobFileHandler
    {
        #region Constants

        /// <summary>
        /// Connection string setting name.
        /// </summary>
        private static readonly string PATH = string.Empty;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of BlobFileHandler class
        /// </summary>
        static BlobFileHandler()
        {
            try
            {
                PATH = RoleEnvironment.GetConfigurationSettingValue("PATH");
            }
            catch
            { }
        }

        #endregion

        #region Public logic

        /// <summary>
        /// Saves stream to the blob
        /// </summary>
        /// <param name="stream">Saving stream</param>
        /// <param name="folderName">The name of the blob's container</param>
        /// <param name="type">The content-type value stored for the blob</param>
        /// <param name="isPublic">Set or not blob as public</param>
        /// <returns>The absolute URI of the blob</returns>
        public string Save(Stream stream, string folderName, string type, bool isPublic = true)
        {
            CloudBlobContainer blobContainer = getBlobContainer(folderName);

            string name = Guid.NewGuid().ToString();
            var blob = blobContainer.GetBlockBlobReference(name);
            blob.Properties.ContentType = type;
            blob.UploadFromStream(stream);
            blob.SetProperties();

            return blob.Uri.AbsoluteUri;
        }

        /// <summary>
        /// Gets content of the blob
        /// </summary>
        /// <param name="fileName">The name of the blob, or the absolute URI to the blob</param>
        /// <param name="folderName">The name of the blob container</param>
        /// <param name="type">The type of the blob's content</param>
        /// <returns>Streams with blob's content</returns>
        public Stream Load(string fileName, string folderName, out string type)
        {
            var storageAccount = CloudStorageAccount.Parse(DocConstants.Get().ConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference(folderName);

            Stream file = null;
            type = string.Empty;

            string res = string.Format(PATH, folderName, fileName);

            var blob = blobContainer.GetBlockBlobReference(res);

            if (blob.Properties.Length > 0)
            {
                blob.FetchAttributes();
                type = blob.Properties.ContentType;
                file = blob.OpenRead();
            }

            return file;
        }

        /// <summary>
        /// Gets content of the blob
        /// </summary>
        /// <param name="uri">Absolute Uri of the blob</param>
        /// <param name="type">Type of the blob content</param>
        /// <returns>Streams with blob's content</returns>
        public Stream Load(string uriString, out string type)
        {
            Uri uri = new Uri(uriString);
            ICloudBlob blob = getBlob(uri);

            Stream file = null;
            type = string.Empty;

            if (blob.Properties.Length > 0)
            {
                blob.FetchAttributes();
                type = blob.Properties.ContentType;
                file = blob.OpenRead();
            }

            return file;
        }

        /// <summary>
        /// Copies an existing blob's contents, properties, and metadata to a new blob.
        /// </summary>
        /// <param name="sourceUri">Absolute Uri of the source blob</param>
        /// <param name="fileName">The name of the target blob</param>
        /// <param name="folderName">The name of the taget blob's container</param>
        /// <param name="setPublic">Set or not target blob as public</param>
        /// <returns>The absolute URI of the target blob</returns>
        public string Copy(string sourceUriString, string fileName, string folderName, bool setPublic = true)
        {
            Uri sourceUri = new Uri(sourceUriString);
            ICloudBlob sourceBlob = getBlob(sourceUri);

            CloudBlobContainer blobContainer = getBlobContainer(folderName, true, setPublic);
            var targetBlob = blobContainer.GetBlockBlobReference(fileName);

            targetBlob.StartCopyFromBlob(sourceBlob as CloudBlockBlob);
            return targetBlob.Uri.AbsoluteUri;
        }

        /// <summary>
        /// Uploads a stream to a block blob
        /// </summary>
        /// <param name="stream">The stream providing the blob content</param>
        /// <param name="targetFolderName"></param>
        /// <param name="type">Content type</param>
        /// <param name="res">The name of the blob, or the absolute URI to the blob</param>
        /// <returns>The absolute URI of the blob</returns>
        public string Update(Stream stream, string targetFolderName, string type, string res)
        {
            CloudBlobContainer blobContainer = getBlobContainer(targetFolderName, true, false);
            var blob = blobContainer.GetBlockBlobReference(res);

            if (blob.Properties.Length > 0)
            {
                blob.Properties.ContentType = type;
                blob.UploadFromStream(stream);
                blob.SetProperties();
            }

            return blob.Uri.AbsoluteUri;
        }

        #endregion

        #region Private logic

        private CloudBlobContainer getBlobContainer(string folderName, bool createIfNotExists = true, bool setPublic = true)
        {
            var storageAccount = CloudStorageAccount.Parse(DocConstants.Get().ConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobContainer = blobClient.GetContainerReference(folderName);

            if (createIfNotExists)
                blobContainer.CreateIfNotExists();

            if (setPublic)
                setPublicAccess(blobContainer);

            return blobContainer;
        }

        private void setPublicAccess(CloudBlobContainer blobContainer)
        {
            BlobContainerPermissions permissions = blobContainer.GetPermissions();
            permissions.PublicAccess = BlobContainerPublicAccessType.Container;
            blobContainer.SetPermissions(permissions);
        }

        private CloudBlockBlob getBlob(Uri uri)
        {
            var storageAccount = CloudStorageAccount.Parse(DocConstants.Get().ConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            ICloudBlob blob = blobClient.GetBlobReferenceFromServer(uri);

            return blob as CloudBlockBlob;
        }

        #endregion
    }
}
