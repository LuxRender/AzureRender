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
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Drawing;

namespace RenderUtils
{
    public class LuxEngine
    {
        #region DllImport

        // Engine functions

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern void luxInit();

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern int luxParse(string filename);

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern double luxStatistics(string statName);

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern void luxExit();

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern void luxAbort();

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern void luxCleanup();

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern void luxStart();

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern void luxPause();

        // Framebuffer functions

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern void luxUpdateFramebuffer();

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern IntPtr luxFramebuffer();

        // FLM functions

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern double luxLoadFLMFromStream(IntPtr buffer, uint bufSize, string flmName);

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern IntPtr luxSaveFLMToStream(ref uint size);

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern double luxUpdateFLMFromStream(IntPtr buffer, uint bufSize);

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern void luxDeleteFLMBuffer(IntPtr buffer);

        // Attribute functions

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern int luxGetIntAttribute(string s1, string s2);

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern double luxGetDoubleAttribute(string s1, string s2);

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern void luxSetDoubleAttribute(string s1, string s2, double value);

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern void resetFlm();

        /* Controlling number of threads */
        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern int luxAddThread();

        [DllImport("liblux.dll", CallingConvention = CallingConvention.Cdecl)]
        [SecurityPermission(SecurityAction.Assert, Unrestricted = true)]
        static extern void luxRemoveThread();

        #endregion

        public LuxEngine(RenderLog log)
        {
            m_log = log;
            Samples = 0;
        }

        public void RemoveThread()
        {
            try
            {
                luxRemoveThread();
            }
            catch (Exception e)
            {
                m_log.Error("Error removing new rendering thread: " + e.Message);
            }
        }

        public int AddThread()
        {
            try
            {
                return luxAddThread();
            }
            catch (Exception e)
            {
                m_log.Error("Error adding new rendering thread: " + e.Message);
                return -1;
            }
        }

        public void ResetFlm()
        {
            try
            {
                resetFlm();
            }
            catch (Exception e)
            {
                m_log.Error("Error reseting flm: " + e.Message);
            }
        }

        public bool Init()
        {
            try
            {
                luxInit();
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Init failed - " + ex.Message);
                return false;
            }
            return true;
        }

        public void Exit()
        {
            try
            {
                luxExit();
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Exit failed - " + ex.Message);
            }
        }

        public void Abort()
        {
            try
            {
                luxAbort();
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Abort failed - " + ex.Message);
            }
        }

        public void Cleanup()
        {
            luxCleanup(); //will give unhandled exception otherwhere if set try catch here
            Samples = 0;
        }

        public bool LoadScene(string scenePath)
        {
            try
            {
                int result = luxParse(scenePath.Normalize());   //call thread gives control here to c++ code and is waiting 
                                                                //here for finish render
                if (result == 0)
                    return false;
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Scene parsing failed - " + ex.Message);
                return false;
            }
            return true;
        }

        public bool IsSceneReady()
        {
            double sceneStatus = luxStatistics("sceneIsReady"); 
            return !Utils.IsEqual(sceneStatus, 0.0);
        }

        public void ResumeRendering()
        {
            try
            {
                luxStart();
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Resume rendering failed - " + ex.Message);
            }
        }

        public void PauseRendering()
        {
            try
            {
                luxPause();
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Pause rendering failed - " + ex.Message);
            }
        }

        public void UpdateFramebuffer()
        {
            try
            {
                luxUpdateFramebuffer();
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Update of framebuffer failed - " + ex.Message);
            }
        }

        public Bitmap GetFramebufferBitmap()
        {
            Bitmap image = null;
            try
            {
                IntPtr framePtr = luxFramebuffer();

                int width = GetFilmWidth();
                int height = GetFilmHeight();

                int bufferSize = width * height * 3; // pixel is RGB color
                byte[] bmpBuffer = new byte[bufferSize];
                Marshal.Copy(framePtr, bmpBuffer, 0, bufferSize);

                // TODO: Optimize creation of bitmap
                image = new Bitmap(width, height);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width + x) * 3;
                        image.SetPixel(x, y, Color.FromArgb(bmpBuffer[index], bmpBuffer[index + 1], bmpBuffer[index + 2]));
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Get framebuffer bitmap failed - " + ex.Message);
                return null;
            }
            return image;
        }

        public byte[] SaveFLMToStream()
        {
            byte[] flmBuffer = null;
            try
            {
                uint flmSize = 0;
                IntPtr flmPtr = luxSaveFLMToStream(ref flmSize);

                flmBuffer = new byte[flmSize];
                Marshal.Copy(flmPtr, flmBuffer, 0, (int)flmSize);

                luxDeleteFLMBuffer(flmPtr);
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Save FLM to stream failed - " + ex.Message);
                return null;
            }
            return flmBuffer;
        }

        public double LoadFLMFromStream(IntPtr buffer, int bufferSize, string flmName)
        {
            try
            {
                return luxLoadFLMFromStream(buffer, (uint)bufferSize, flmName);
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Loading of FLM from stream failed - " + ex.Message);
                return -1;
            }
        }

        public void UpdateFLMFromStream(IntPtr buffer, int bufferSize)
        {
            try
            {
                Samples += luxUpdateFLMFromStream(buffer, (uint)bufferSize);
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Updating of FLM from stream failed - " + ex.Message);
            }
        }

        public int GetFilmWidth()
        {
            try
            {
                return luxGetIntAttribute("film", "xResolution");
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Get film x resolution failed - " + ex.Message);
                return 0;
            }
        }

        public int GetFilmHeight()
        {
            try
            {
                return luxGetIntAttribute("film", "yResolution");
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Get film y resolution failed - " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>-1 if error occured</returns>
        public double GetFilmNumberOfSamples()
        {
            try
            {
                // "numberOfSamplesFromNetwork" - Number of samples contributed from network slaves
                // "numberOfResumedSamples" - Number of samples loaded from saved film
                return luxGetDoubleAttribute("film", "numberOfLocalSamples"); //Number of samples contributed to film on the local machine
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Get film number of samples failed - " + ex.Message);
                return -1;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>0 if error occured</returns>
        public double GetSpp()
        {
            try
            {
                if (GetFilmWidth() != 0 && GetFilmHeight() != 0)
                    return GetFilmNumberOfSamples() / (GetFilmWidth() * GetFilmHeight());
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Get film number of samples failed - " + ex.Message);
            }
            return 0;
        }

        public double GetSppOfLoadedFLM()
        {
            try
            {
                if (GetFilmWidth() != 0 && GetFilmHeight() != 0)
                    return Samples / (GetFilmWidth() * GetFilmHeight());
            }
            catch (Exception ex)
            {
                m_log.Error("LuxEngine: Get film number of samples failed - " + ex.Message);
            }
            return 0;
        }

        private RenderLog m_log;
        public double Samples {get; protected set; }   //current number of samples on image (needed for spp on merger);
    }
}
