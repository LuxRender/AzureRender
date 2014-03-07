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
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using RenderUtils.QueueMessage;

namespace RenderUtils
{
    public class Scene
    {
        /* scene file - blob with scene
         * diskLetter - DiskLetter = drive.LocalPath  //local instance storage
         */
        public Scene(string sceneFile, string diskLetter)
        {
            m_inputPath = sceneFile;

            createDirectoryForScene(diskLetter);  //creates local directory

            m_isZip = true;
            extractArchive(sceneFile); //extract zip scene file into local directory(same with scene zip)
            FileInfo[] lxsArray = m_directory.GetFiles("*.lxs", SearchOption.AllDirectories);
            if (lxsArray.Length == 0)
                throw new Exception("Lxs file not found in archive.");
            else if (lxsArray.Length > 1)
                throw new Exception("It has been found a several lxs files.");
            m_lxsName = lxsArray[0].FullName;
            correctIncludePath();
        }

        public string GetUniqueFlmPath()
        {
            int index = m_lxsName.LastIndexOf("\\");
            string flmPath = m_lxsName.Remove(index);

            FileInfo file;
            do
            {
                string guid = Guid.NewGuid().ToString();

                flmPath += guid + ".flm";
                file = new FileInfo(flmPath);
            }
            while (file.Exists);

            return flmPath;
        }

        static public string GetFileName(string filePath)
        {
            int index = filePath.LastIndexOf("\\");
            if (index != -1)
                filePath = filePath.Substring(index + 1);
            else
            {
                index = filePath.LastIndexOf("/");
                if (index != -1)
                    filePath = filePath.Substring(index + 1);
            }

            return filePath;
        }

        public string ScenePath
        {
            get { return m_lxsName; }
        }

        public string SceneDirectory
        {
            get
            {
                int index = m_lxsName.LastIndexOf("\\");
                return m_lxsName.Remove(index);
            }
        }

        public string InputPath
        {
            get { return m_inputPath; }
        }

        private void createDirectoryForScene(string diskLetter)
        {
            do
            {
                string guid = Guid.NewGuid().ToString();
                string folderName = diskLetter + guid + "\\";

                m_directory = new DirectoryInfo(folderName);
            }
            while (m_directory.Exists);

            m_directory.Create();
        }

        //zipArchive - blob with scene zip file
        private void extractArchive(string zipArchive)
        {
            Microsoft.WindowsAzure.Storage.CloudStorageAccount storageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse(DocConstants.Get().ConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer driveContainer = blobClient.GetContainerReference(BlobName.SCENE_BLOB);//sceneBlob = "scenegallery"

            CloudBlockBlob blockBlob = driveContainer.GetBlockBlobReference(zipArchive);
            string zipPath = m_directory.FullName + blockBlob.Name; 
            Utils.DownloadBlobToFile(blockBlob, zipPath); //download scene zip file to zipPath

            try
            {
                FastZip zip = new FastZip();
                zip.ExtractZip(zipPath, m_directory.FullName, null); //extract from zipPath to Directory
            }
            catch (Exception e)
            {
                string msg = "Extract archive error: " + e.Message;
                throw new Exception(msg);
            }
        }

        private void saveFileToDirectory(string lxsFile)
        {
            CloudBlobContainer driveContainer = renderStorage.Get().CreateContainer(BlobName.SCENE_BLOB, false);
            CloudPageBlob pageBlob = driveContainer.GetPageBlobReference(lxsFile);

            string lxsPath = m_directory.FullName + pageBlob.Name;
            Utils.DownloadBlobToFile(pageBlob, lxsPath);
        }

        private void correctIncludePath()
        {
            int index = m_lxsName.LastIndexOf("\\") + 1;
            string directory = m_lxsName.Remove(index);  //path to scene.lxs without the most scene.lxs

            string[] text = File.ReadAllLines(m_lxsName); //content of scene.lxs
            int strLength = text.GetLength(0);

            for (int i = 0; i < strLength; ++i)     //each row in scene.lxs
            {
                if (text[i].Contains("Include"))    //creating string like: Include Include "a:\Directory\Data\luxMaterials.lxm"
                {
                    text[i] = text[i].Replace("/", "\\");

                    index = text[i].IndexOf("\"") + 1;
                    if (text[i][0] == '.')
                        ++index;
                    if (text[i][0] == '\\')
                        ++index;

                    text[i] = text[i].Substring(index);
                    text[i] = "Include \"" + directory + text[i];
                }

            }

            //enviropment
            for (int i = 0; i < strLength; ++i)
            {
                if (text[i].Contains("string mapname"))
                {
                    string temp = "";
                    int index1 = text[i].IndexOf("[");
                    temp = text[i].Substring(0, index1 + 2);

                    int index2 = text[i].IndexOf(".");

                    int dif = index2 - index1 - 2;
                    Console.WriteLine(dif);
                    string temp1 = text[i].Substring(index1 + 2, dif);
                    Console.WriteLine(temp1);
                    temp += directory + temp1 + text[i].Substring(index2, 6);
                    Console.WriteLine(index1);
                    Console.WriteLine(index2);
                    Console.WriteLine(temp);
                    text[i] = temp;

                    CloudBlockBlob SppBlob = renderStorage.Get().CreateBlob("logcontainer", "Env.txt");

                    Utils.UpploadBlobText(SppBlob, Convert.ToString(temp));
                }


            }            

            File.WriteAllLines(m_lxsName, text);

            
            FileInfo[] lxmArray = m_directory.GetFiles("*.lxm", SearchOption.AllDirectories);
            if (lxmArray.Length == 0)
                throw new Exception("Lxm file not found in archive.");
            else if (lxmArray.Length > 1)
                throw new Exception("It has been found a several lxs files.");

            string lxmName = lxmArray[0].FullName;

            string[] lxmText = File.ReadAllLines(lxmName); //content of *.lxm
            int strNumber = lxmText.GetLength(0);
            int lxmIndex = 0;
            for (int i = 0; i < strNumber; ++i)     //each row *.lxm
            {
                if (lxmText[i].Contains("string filename"))
                {
              //      lxmText[i] = lxmText[i].Replace("/", "\\");

                    lxmIndex = lxmText[i].IndexOf("[") + 2;
                    lxmText[i] = lxmText[i].Insert(lxmIndex, directory);
                    lxmText[i] = lxmText[i].Replace("\\", "/");
                  //  lxmText[i] = lxmText[i].Substring(lxmIndex);
                 //   lxmText[i] = "Include \"" + directory + text[i];
                }

            }
            File.WriteAllLines(lxmName, lxmText);
        }

        public void DeleteScene(RenderLog log)
        {
            try
            {
                m_lxsName = string.Empty;
                if (m_directory.Exists)
                    m_directory.Delete(true);
            }
            catch (Exception e)
            {
                log.Error("Error when deleting scene: " + e.Message);
            }
        }

        public bool IsZip
        {
            get { return m_isZip; }
        }

        bool m_isZip;
        DirectoryInfo m_directory;
        string m_lxsName;
        string m_inputPath;
    }
}
