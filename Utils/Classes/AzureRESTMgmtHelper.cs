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

using RenderUtils;
using System.Xml;
using System.Net;
using System.IO;
using System.Xml.Linq;
using System.Collections;
using System.Collections.Specialized;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace RenderUtils
{
    public class AzureRESTMgmtHelper
    {

        static public X509Certificate2 LookupCertificate()
        {
            return new X509Certificate2("myCert.pfx", "amcPass20014");
        }

        static public string GetDeploymentInfo()
        {
            string x_ms_version = "2009-10-01";

            string deploymentSlot = "Production";

            string requestUri = "https://management.core.windows.net/" + DocConstants.Get().SubscriptionId + "/services/hostedservices/"
                                + DocConstants.Get().ServiceName + "/deploymentslots/" + deploymentSlot;
            HttpWebRequest restRequest = (HttpWebRequest)HttpWebRequest.Create(requestUri);

            NameValueCollection requestHeaders = new NameValueCollection();
            requestHeaders.Add("x-ms-version", x_ms_version);

            X509Certificate cert = LookupCertificate();
            restRequest.ClientCertificates.Add(cert);

            restRequest.Method = "GET";
            WebResponse restResponse = default(WebResponse);
            restRequest.ContentType = "text/xml";

            if (requestHeaders != null)
            {
                restRequest.Headers.Add(requestHeaders);
            }

            restResponse = restRequest.GetResponse();

            string responseBody = string.Empty;

            if (restResponse != null)
            {
                using (StreamReader restResponseStream = new StreamReader(restResponse.GetResponseStream(), true))
                {
                    // Deployment DeploymentConfiguration = (Deployment)xmls.Deserialize(RestResponseStream);
                    responseBody = restResponseStream.ReadToEnd();
                    restResponseStream.Close();
                }
            }
            return responseBody;
        }

        static public string GetServiceConfig(string deploymentInfoXML)
        {
            //get the service configuration out of the deployment configuration
            XElement deploymentInfo = XElement.Parse(deploymentInfoXML);
            string encodedServiceConfig = (from element in deploymentInfo.Elements()
                                           where element.Name.LocalName.Trim().ToLower() == "configuration"
                                           select (string)element.Value).Single();

            string currentServiceConfigText = System.Text.ASCIIEncoding.ASCII.GetString(System.Convert.FromBase64String(encodedServiceConfig));

            return currentServiceConfigText;
        }

        static public string GetInstanceCount(string serviceConfigXML, string roleName)
        {
            //make the service config queryable
            XElement xServiceConfig = XElement.Parse(serviceConfigXML);

            XElement webRoleElement = (from element in xServiceConfig.Elements()
                                       where element.Attribute("name").Value == roleName
                                       select element).Single();

            string currentInstanceCount = (from childelement in webRoleElement.Elements()
                                           where childelement.Name.LocalName.Trim().ToLower() == "instances"
                                           select (string)childelement.Attribute("count").Value).FirstOrDefault();
            return currentInstanceCount;

        }

        public static string ChangeInstanceCount(string serviceConfigXML, string roleName, string newCount)
        {
            string returnConfig = default(string);
            XElement XServiceConfig = XElement.Parse(serviceConfigXML);

            XElement WebRoleElement = (from element in XServiceConfig.Elements()
                                       where element.Attribute("name").Value == roleName
                                       select element).Single();

            XElement InstancesElement = (from childelement in WebRoleElement.Elements()
                                         where childelement.Name.LocalName.Trim().ToLower() == "instances"
                                         select childelement).Single();

            InstancesElement.SetAttributeValue("count", newCount.ToString());

            StringBuilder xml = new StringBuilder();
            XServiceConfig.Save(new StringWriter(xml));
            returnConfig = xml.ToString();

            return returnConfig;
        }

        public static string ChangeInstanceCount(string serviceConfigXML, string roleOneName, string newCountOne, 
            string roleTwoName, string newCountTwo)
        {
            string returnConfig = default(string);
            XElement xServiceConfig = XElement.Parse(serviceConfigXML);

            XElement webRoleOneElement = (from element in xServiceConfig.Elements()
                                       where element.Attribute("name").Value == roleOneName
                                       select element).Single();

            XElement webRoleTwoElement = (from element in xServiceConfig.Elements()
                                          where element.Attribute("name").Value == roleTwoName
                                          select element).Single();


            XElement instancesOneElement = (from childelement in webRoleOneElement.Elements()
                                         where childelement.Name.LocalName.Trim().ToLower() == "instances"
                                         select childelement).Single();

            instancesOneElement.SetAttributeValue("count", newCountOne.ToString());

            XElement instancesTwoElement = (from childelement in webRoleTwoElement.Elements()
                                         where childelement.Name.LocalName.Trim().ToLower() == "instances"
                                         select childelement).Single();

            instancesTwoElement.SetAttributeValue("count", newCountTwo.ToString());

            StringBuilder xml = new StringBuilder();
            xServiceConfig.Save(new StringWriter(xml));
            returnConfig = xml.ToString();

            return returnConfig;
        }

        static public void ChangeConfigFile(string configXML)
        {

            ChangeConfigFile(DocConstants.Get().SubscriptionId,
                            DocConstants.Get().ServiceName, "Production",
                            configXML);
        }

        static public void ChangeConfigFile(String subscriptionID, String svcName, String deploymentSlots, String configXML)
        {
            string changeConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
                                <ChangeConfiguration xmlns=""http://schemas.microsoft.com/windowsazure"">
                                    <Configuration>{0}</Configuration>
                                </ChangeConfiguration>";

            string requestUrl = "https://management.core.windows.net/" + subscriptionID +
                                "/services/hostedservices/" + svcName + "/deploymentslots/" +
                                deploymentSlots + "/?comp=config";

            string configData = System.Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(configXML));
            String requestBody = string.Format(changeConfig, configData);

            string xMsVersion = "2009-10-01";
            string returnBody = string.Empty;


            WebResponse resp = null;
            NameValueCollection requestHeaders = new NameValueCollection();
            requestHeaders.Add("x-ms-version", xMsVersion);

            X509Certificate cert = LookupCertificate();

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(requestUrl);
            request.Method = "POST";
            request.ClientCertificates.Add(cert);
            request.ContentType = "application/xml";
            request.ContentLength = Encoding.UTF8.GetBytes(requestBody).Length;
            if (requestHeaders != null)
            {
                request.Headers.Add(requestHeaders);
            }

            using (StreamWriter sw = new StreamWriter(request.GetRequestStream()))
            {
                sw.Write(requestBody);
                sw.Close();
            }
            resp = request.GetResponse();


        }

        static public void ChangeLocalInstanceCount(String aConfigFilePath, string aAzureCSRunFilePath, string aRoleName, string aNewCount, string aDeploymentId)
        {
            changeLocalConfigFile(aConfigFilePath, aRoleName, aNewCount);
            refreshEmulator(aConfigFilePath, aAzureCSRunFilePath, aDeploymentId);
        }

        static private void refreshEmulator(string aConfigFilePath, string aAzureCSRunFilePath, string aDeploymentId)
        {
            var patternt = Regex.Escape("(") + @"\d+" + Regex.Escape(")");
            var input = aDeploymentId;
            var m = Regex.Match(input, patternt);
            var deploymentId = m.ToString().Replace("(", string.Empty).Replace(")", string.Empty);

            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = aAzureCSRunFilePath;
            p.StartInfo.Arguments = string.Format("/update:{0};{1}", deploymentId, aConfigFilePath);
            p.Start();
            var error = p.StandardError.ReadToEnd();
            p.WaitForExit();
        }

        private static void changeLocalConfigFile(string aConfigFilePath, string aRoleName, string aNewCount)
        {
            XDocument xmlDoc = XDocument.Load(aConfigFilePath);

            XElement roleElement = (from c in xmlDoc.Elements().Elements()
                                    where c.FirstAttribute.Value.Equals(aRoleName)
                                    select c).Single();
            XElement instanceElement = (from childelement in roleElement.Elements()
                                        where childelement.Name.LocalName.Trim().ToLower().Equals("instances")
                                        select childelement).Single();

            instanceElement.Attribute("count").SetValue(aNewCount);

            xmlDoc.Save(aConfigFilePath);
        }

        
    }



}

