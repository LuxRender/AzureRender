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
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace RenderUtils
{
    public class DocConstants
    {
        public static DocConstants Get()
        {
            if (m_docConstants == null)
                m_docConstants = new DocConstants();
            return m_docConstants;
        }

        private DocConstants()
        {
            ConnectionString = getServiceConnectionString();
            DBConnectionString = getDBConnectionString();
            SubscriptionId = getSubscription();
            ServiceName = getServiceName();
            DBTableName = getDBTableName();
        }

        public const string DEBUG_CONNECTION_STRING = "UseDevelopmentStorage=true";

        //   Connection string for cloud Azure
        public string ConnectionString { get; protected set; }
        public string DBTableName { get; protected set; }

        //variables for cloud dynamic allocation
        public string SubscriptionId { get; protected set; } 
        public string ServiceName { get; protected set; }

        private static DocConstants m_docConstants = null;

        //variables for dynamic instance allocation
        public const string WORKER_NAME = "Worker";
        public const string MERGER_NAME = "Merger";
        public const double INSTANCES_REMOVING_INTERVAL = 60000.0;   //time in milliseconds that dispatcher will wait(when no connections are 
                                                                   //established) to free worker instances 
        public const int MINIMUM_RENDER_TIME = 60;
        public const string InititalInstancesCount = "1"; //number of instances of each role
                                      //that should be locatet when no rendering is perfoming
        public const int WAIT_AFTER_SCALE_REQUEST = 180000; //time for timeout after new instances allocating request
        public const int MAKE_PICTURE_HOLDUP = 60000; //timer for merger to wait for each new picture sending to client
        //database
        public string DBConnectionString { get; protected set; }// = "Server=tcp:ch7djpiqeo.database.windows.net,1433;Database=users;User ID=sqldatabase@ch7djpiqeo;Password=qwerTY356;Trusted_Connection=False;Encrypt=True;Connection Timeout=30;";//getDBConnectionString();


        private string getDBTableName()
        {
            return RoleEnvironment.GetConfigurationSettingValue("DataBaseTableName"); 
        }

        private string getDBConnectionString()
        {
            return RoleEnvironment.GetConfigurationSettingValue("DataBaseConnectionString"); 
        }

        private string getServiceConnectionString()
        {
            return RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"); 
        }

        private string getServiceName()
        {
            return RoleEnvironment.GetConfigurationSettingValue("ServiceName");
        }

        private string getSubscription()
        {
            return RoleEnvironment.GetConfigurationSettingValue("SubscriptionID");
        }

    }
}
