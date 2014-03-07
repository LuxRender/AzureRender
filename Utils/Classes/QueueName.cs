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

namespace RenderUtils.QueueMessage
{
    public static class QueueName
    {
        public const string LOG_QUEUE = "loginfo";
        public const string WEB_QUEUE = "webjobs";
        public const string RENDER_QUEUE = "renderjobs";
        public const string MERGER_QUEUE = "mergerjobs";
        public const string DISPATCHER_QUEUE = "dispjobs";
    }

    public static class BlobName
    {
        public const string DRIVES_BLOB = "drives";
        public const string FILM_BLOB = "filmgallery";
        public const string SCENE_BLOB = "scenegallery";
        public const string IMAGE_BLOB = "imagegallery";
        public const string SPP_FILE = "Spp.txt";
        public const string UNIQUE_BLOB = "uniqueBlob";
    }
}