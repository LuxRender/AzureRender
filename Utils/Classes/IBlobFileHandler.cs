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

namespace RenderUtils.StorageManager
{
    /// <summary>
    /// Provides methods to work with blobs
    /// </summary>
    interface IBlobFileHandler
    {
        /// <summary>
        /// Copies an existing blob's contents, properties, and metadata to a new blob.
        /// </summary>
        /// <param name="sourceUri">Absolute Uri of the source blob</param>
        /// <param name="fileName">The name of the target blob</param>
        /// <param name="folderName">The name of the taget blob's container</param>
        /// <param name="setPublic">Set or not target blob as public</param>
        /// <returns>The absolute URI of the target blob</returns>
        string Copy(string sourceUri, string fileName, string folderName, bool setPublic = true);
        /// <summary>
        /// Gets content of the blob
        /// </summary>
        /// <param name="fileName">The name of the blob, or the absolute URI to the blob</param>
        /// <param name="folderName">The name of the blob container</param>
        /// <param name="type">The type of the blob's content</param>
        /// <returns>Streams with blob's content</returns>
        System.IO.Stream Load(string fileName, string folderName, out string type);
        /// <summary>
        /// Gets content of the blob
        /// </summary>
        /// <param name="uri">Absolute Uri of the blob</param>
        /// <param name="type">Type of the blob content</param>
        /// <returns>Streams with blob's content</returns>
        System.IO.Stream Load(string uri, out string type);
        /// <summary>
        /// Saves stream to the blob
        /// </summary>
        /// <param name="stream">Saving stream</param>
        /// <param name="folderName">The name of the blob's container</param>
        /// <param name="type">The content-type value stored for the blob</param>
        /// <param name="isPublic">Set or not blob as public</param>
        /// <returns>The absolute URI of the blob</returns>
        string Save(System.IO.Stream stream, string folderName, string type, bool isPublic = true);
        /// <summary>
        /// Uploads a stream to a block blob
        /// </summary>
        /// <param name="stream">The stream providing the blob content</param>
        /// <param name="targetFolderName"></param>
        /// <param name="type">Content type</param>
        /// <param name="res">The name of the blob, or the absolute URI to the blob</param>
        /// <returns>The absolute URI of the blob</returns>
        string Update(System.IO.Stream stream, string targetFolderName, string type, string res);
    }
}
