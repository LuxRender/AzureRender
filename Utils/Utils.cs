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
using System.Text.RegularExpressions;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.DirectoryServices.AccountManagement;
using RenderUtils.QueueMessage;
using Microsoft.WindowsAzure.Storage;

namespace RenderUtils
{
    public static class Utils
    {
        static double accuracy = 0.0000001;

        public static int MaxLoginLength = 30;
        public static int MinLoginLength = 3;
        public static int MaxPwdLength = MaxLoginLength;
        public static int MinPwdLength = 8;
        public static int MaxEmailLength = MaxLoginLength;
        public static int MinEmailLength = 6;
        public static int CardLength = 16;
        public static int CardVerificationLength = 4;

        public static bool IsEqual(double left, double right)
        {
            bool retval = false;
            if (Math.Abs(right - left) < accuracy)
                retval = true;

            return retval;
        }

        public static bool IsLess(double left, double right)
        {
            bool retval = false;
            if (right - left > accuracy)
                retval = true;

            return retval;
        }

        public static bool IsBigger(double left, double right)
        {
            bool retval = false;
            if (left - right > accuracy)
                retval = true;

            return retval;
        }

        //data about image including location, sceneid, number of instances that made one iteration etc.
        public class SceneBlobReferenceMessage    
        {
            public SceneBlobReferenceMessage(StartMergeMessage startMessage, bool sceneIsSet, int sceneID)
            {
                StartMessage = startMessage;
                SceneID = sceneID;
            }
            public void IncrementFinishedRolesNumber()
            {
                ++InstancesFinished;
            }

            public int SceneID { get; protected set; }
            public StartMergeMessage StartMessage { get; set; }  //message from user(dispatcher)
            public int InstancesFinished { get; private set; }         //number of instances that finished rendering
        }

        public class ThreadLocalSceneBlob
        {
            public CloudBlockBlob FlmBlob { get; set; }
            public CloudBlockBlob OutputBlob { get; set; }
            public string SceneName { get; set; }
        }

        public static int RoleID()
        {
            string str = RoleEnvironment.CurrentRoleInstance.Id;
            Match match = Regex.Match(str, "[0-9]*", RegexOptions.RightToLeft);

            return int.Parse(str.Substring(match.Index, match.Length));
        }

        public static int GetCountWorkerInstances()
        {
            return getCountInstances("Worker");
        }

        public static int GetCountMergerInstances()
        {
            return getCountInstances("Merger");
        }

        private static int getCountInstances(string nameRole)
        {
            int count = 0;

            bool isAvailable = RoleEnvironment.IsAvailable;
            if (true == isAvailable)
            {
                Role serviceRole = RoleEnvironment.Roles[nameRole];
                count = serviceRole.Instances.Count();
            }

            return count;
        }

        public static byte[] DownloadBlobToArray(ICloudBlob blob)
        {
            using (var ms = new MemoryStream())
            {
                blob.DownloadToStream(ms);
                ms.Position = 0;
                return ms.ToArray();
            }
        }

        public static string DownloadBlobText(ICloudBlob blob)
        {
            using (var ms = new MemoryStream())
            {
                blob.DownloadToStream(ms);
                ms.Position = 0;

                using (var reader = new StreamReader(ms, true))
                {
                    ms.Position = 0;
                    return reader.ReadToEnd();
                }
            }

        }

        public static void UpploadBlobText(ICloudBlob blob, string text)
        {
            byte[] byteArray = Encoding.ASCII.GetBytes(text);
            using (var ms = new MemoryStream(byteArray, writable: false))
            {
                blob.UploadFromStream(ms);
            }
        }

        public static void UpploadBlobByteArray(ICloudBlob blob, byte[] byteArray)
        {
            lock (m_locker)
            {
                using (var ms = new MemoryStream(byteArray, writable: false))
                {
                    ms.Seek(0, SeekOrigin.Begin);

                    blob.UploadFromStream(ms);
                }
            }
        }

        private static Object m_locker = new Object();

        public static void DownloadBlobToFile(ICloudBlob blob, string fileName)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Create))
            {
                blob.DownloadToStream(fs);
            }
        }


        public static class DataBaseUtils
        {
            //For server
            const string SERVER = "Server=tcp:t57qi685zb.database.windows.net;";
            const string OTHER_PARAMS = "Trusted_Connection=False;Encrypt=True;";

            ////For Local
            //const string Server = "Server=.\\SQLEXPRESS;";
            //const string OtherParams = "";

            //General
            const string DATABASE = "Database=AzureDB;";
            const string USER = "User ID=azure;Password=Qweasd12;";

            const string CONNECTION_STRING = SERVER + DATABASE + USER + OTHER_PARAMS;

            public static int ConvertLoginToID(string login)
            {
                int userId = -1;

                using (SqlConnection connection = new SqlConnection(CONNECTION_STRING))
                {
                    string comStr = "select UserId from Users where AzureUser='" + login + "'";
                    SqlCommand comm = new SqlCommand(comStr, connection);
                    connection.Open();

                    object obj = comm.ExecuteScalar();
                    if (null != obj)
                    {
                        userId = (int)obj;
                    }
                }

                return userId;
            }

            public static List<string> GetImageUrisForUser(string userName)
            {
                List<string> uris = new List<string>();

                int userId = ConvertLoginToID(userName);
                if (-1 != userId)
                {
                    using (SqlConnection connection = new SqlConnection(CONNECTION_STRING))
                    {
                        string comStr = "select Image from Images where UserId=" + userId.ToString();
                        SqlCommand comm = new SqlCommand(comStr, connection);
                        connection.Open();

                        using (SqlDataReader reader = comm.ExecuteReader())
                        {
                            while (true == reader.Read())
                            {
                                string uriImage = reader.GetString(0);
                                uris.Add(uriImage);
                            }
                        }
                    }
                }

                return uris;
            }

            public static void DeleteImageUri(string uri)
            {
                int pos = uri.LastIndexOf('/');
                if (pos >= 0)
                    uri = uri.Remove(0, pos + 1);

                using (SqlConnection connection = new SqlConnection(CONNECTION_STRING))
                {
                    string comStr = "select UserId from Images where Image='" + uri + "'";
                    SqlCommand comm = new SqlCommand(comStr, connection);
                    connection.Open();

                    object obj = comm.ExecuteScalar();
                    if (null != obj)
                    {
                        comStr = "delete from Images where Image='" + uri + "'";
                        comm = new SqlCommand(comStr, connection);

                        int delete = comm.ExecuteNonQuery();
                    }
                }
            }

            public static bool CheckLoginCorrect(string login, string password)
            {
                bool isCorrect = false;
                using (SqlConnection connection = new SqlConnection(DocConstants.Get().DBConnectionString))
                {
                    //name of db table is taking from config file, made by deployer
                    string comStr = "select * from " + DocConstants.Get().DBTableName + " where name = @login and password = @password";
                    SqlCommand comm = new SqlCommand(comStr, connection);

                    comm.Parameters.AddWithValue("@login", login);         //adding parameters to query
                    comm.Parameters.AddWithValue("@password", password);

                    connection.Open();
                    object obj = comm.ExecuteScalar();
                    if (null != obj)
                    {
                        isCorrect = true;
                    }
                }

                return isCorrect;
            }

            public static void InsertImageUri(int userId, string uri)
            {
                using (SqlConnection connection = new SqlConnection(CONNECTION_STRING))
                {
                    string comStr = "select UserId from Images where Image='" + uri + "' and UserId=" + userId.ToString();
                    SqlCommand comm = new SqlCommand(comStr, connection);
                    connection.Open();

                    object obj = comm.ExecuteScalar();
                    if (null == obj)
                    {
                        comStr = "insert into Images values (" + userId.ToString() + ", '" + uri + "')";
                        comm = new SqlCommand(comStr, connection);

                        int insert = comm.ExecuteNonQuery();
                    }
                }
            }

            public static void InsertNewUser(string user, string pwd, string email)
            {
                using (SqlConnection connection = new SqlConnection(CONNECTION_STRING))
                {
                    string comStr = "select MAX(UserId) from Users";
                    SqlCommand comm = new SqlCommand(comStr, connection);
                    connection.Open();

                    object obj = comm.ExecuteScalar();
                    if (null != obj)
                    {
                        int id = 0;
                        if (obj.GetType() == id.GetType())
                        {
                            id = (int)obj;
                        }
                        ++id;

                        comStr = "insert into Users values (" + id.ToString() + ", '" + user + "', '" + pwd + "', '" + email + "')";
                        comm = new SqlCommand(comStr, connection);

                        int insert = comm.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}


