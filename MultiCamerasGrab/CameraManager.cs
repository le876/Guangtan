using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using IKapC.NET;

//使用相机时需要进行相机类型的设置，在Probe和Open按钮的点击行为函数中修改GetConnectedCameraCount()函数的第二个参数
//GV相机参数为GigEVision
//USB相机参数为USB3Vision
namespace MultiCamerasGrab
{
    public class CameraManager
    {
        //相机句柄
        private IntPtr m_hDev = new IntPtr(-1);
        //采集数据流句柄
        private IntPtr m_hStream = new IntPtr(-1);
        //采集缓冲区句柄列表
        private List<IntPtr> m_hBufferList = new List<IntPtr>();
        //采集帧索引
        public int m_nFrameIndex = 0;
        //缓冲区数目（采集帧索引不得大于缓冲区数目）
        public int m_nFrameCount = 5;
        //相机类型
        private string m_sCameraType = "";

        public CameraManager()
        {
            Init();
        }

        static bool Check(uint err)
        {
            if (err != (uint)ItkStatusErrorId.ITKSTATUS_OK)
            {
                System.Diagnostics.Debug.WriteLine("Error code: {0}.\n", err.ToString("x8"));
                return false;
            }
            return true;
        }

        public static void Init()
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            //初始化IKapC库
            res = IKapCLib.ItkManInitialize();
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Initialize IKapCLib failed");
                return;
            }
        }

        /*
         *@brief:根据索引开启相机
         *@param [in] nIndex:相机索引
         *@return:是否开启成功
         */
        public bool OpenCamera(uint nIndex)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            //以控制模式开启相机
            int accessMode = (int)ItkDeviceAccessMode.ITKDEV_VAL_ACCESS_MODE_CONTROL;
            res = IKapCLib.ItkDevOpen(nIndex, accessMode, ref m_hDev);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Camera error:Open camera failed");
                return false;
            }
            return true;
        }

        /*
         *@brief:关闭相机
         *@param [in]:
         *@return:是否关闭成功
         */
        public bool CloseCamera()
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            res = IKapCLib.ItkDevClose(m_hDev);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Camera error:Close camera failed");
                return false;
            }
            return true;
        }

        /*
         *@brief:获取已连接设备信息
         *@param [in] nIndex:设备索引
         *@return:已连接设备信息结构体
         */
        public IKapCLib.ITKDEV_INFO GetDeviceInfo(uint nIndex)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            IKapCLib.ITKDEV_INFO pDI = new IKapCLib.ITKDEV_INFO();
            res = IKapCLib.ItkManGetDeviceInfo(nIndex, ref pDI);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Camera error:Get camera's Information failed");
                return pDI;
            }
            return pDI;
        }

        /*
         *@brief:获取已连接设备个数
         *@param [in] nIndex,sType:第一个符合相机类型要求的设备索引，相机类型
         *@return:已连接设备个数
         */
        public int GetConnectedCameraCount(ref uint nIndex,string sType)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            uint nCount = 0;
            m_sCameraType = sType;
            res = IKapCLib.ItkManGetDeviceCount(ref nCount);
            if (!Check(res) || nCount < 0)
            {
                System.Diagnostics.Debug.WriteLine("Camera error:Get camera count failed");
                return -1;
            }
            for (uint i = 0; i < nCount; i++)
            {
                var pDI = GetDeviceInfo(i);
                if(pDI.DeviceClass.ToString() == sType)
                {
                    nIndex = i;
                    break;
                }
            }
            return (int)(nCount - nIndex);
        }

        /*
         *@brief:获取相机long类型特征值
         *@param [in] sFeatureName,pFeatureValue:特征名，特征值引用
         *@return:是否获取成功
         */
        public bool GetFeatureInt64(string sFeatureName, ref long pFeatureValue)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            res = IKapCLib.ItkDevGetInt64(m_hDev, sFeatureName, ref pFeatureValue);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Get feature error:Get int64 feature failed");
                return false;
            }
            return true;
        }

        /*
         *@brief:获取相机double类型特征值
         *@param [in] sFeatureName,pFeatureValue:特征名，特征值引用
         *@return:是否获取成功
         */
        public bool GetFeatureDouble(string sFeatureName, ref double pFeatureValue)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            res = IKapCLib.ItkDevGetDouble(m_hDev, sFeatureName, ref pFeatureValue);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Get feature error:Get double feature failed");
                return false;
            }
            return true;
        }

        /*
         *@brief:获取相机图片格式
         *@param [in]:
         *@return:相机图片格式
         */
        public uint GetPixelFormat()
        {
            uint nPixelFormat = 0;
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            StringBuilder sPixelFormat = new StringBuilder(64);
            uint nFormatLen = 64;
            res = IKapCLib.ItkDevToString(m_hDev, "PixelFormat", sPixelFormat, ref nFormatLen);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Pixel format error:Get pixel format failed");
                return 0;
            }
            if (sPixelFormat.ToString() == "Mono8")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO8;
            }
            else if (sPixelFormat.ToString() == "Mono10")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO10;
            }
            else if (sPixelFormat.ToString() == "Mono12")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO10;
            }
            else if (sPixelFormat.ToString() == "BayerGR8")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GR8;
            }
            else if (sPixelFormat.ToString() == "BayerRG8")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_RG8;
            }
            else if (sPixelFormat.ToString() == "BayerGB8")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GB8;
            }
            else if (sPixelFormat.ToString() == "BayerBG8")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_BG8;
            }
            else if (sPixelFormat.ToString() == "BayerGR10")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GR10;
            }
            else if (sPixelFormat.ToString() == "BayerRG10")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_RG10;
            }
            else if (sPixelFormat.ToString() == "BayerGB10")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GB10;
            }
            else if (sPixelFormat.ToString() == "BayerBG10")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_BG10;
            }
            else if (sPixelFormat.ToString() == "BayerGR12")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GR12;
            }
            else if (sPixelFormat.ToString() == "BayerRG12")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_RG12;
            }
            else if (sPixelFormat.ToString() == "BayerGB12")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GB12;
            }
            else if (sPixelFormat.ToString() == "BayerBG12")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_BG12;
            }
            else if (sPixelFormat.ToString() == "RGB8")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB888;
            }
            else if (sPixelFormat.ToString() == "RGB10")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB101010;
            }
            else if (sPixelFormat.ToString() == "RGB12")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB121212;
            }
            else if (sPixelFormat.ToString() == "BGR8")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR888;
            }
            else if (sPixelFormat.ToString() == "BGR10")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR101010;
            }
            else if (sPixelFormat.ToString() == "BGR12")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR121212;
            }
            else if (sPixelFormat.ToString() == "YUV422_8")
            {
                nPixelFormat = (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_YUV422_8_UYUV;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Pixel format error:Undefined format type");
                return 0;
            }
            return nPixelFormat;
        }

        /*
         *@brief:获取采集缓冲区大小
         *@param [in]:
         *@return:采集缓冲区大小
         */
        public uint GetBufferSize()
        {
            uint nBuffersz = 0;
            IntPtr pBuffersz = Marshal.AllocHGlobal(8);
            uint nPixelFormat = GetPixelFormat();
            uint prm = (uint)ItkBufferPrm.ITKBUFFER_PRM_SIZE;
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            if (m_hBufferList.Count != 0)
            {
                res = IKapCLib.ItkBufferGetPrm(m_hBufferList[0], prm, pBuffersz);
                if (!Check(res))
                {
                    System.Diagnostics.Debug.WriteLine("Buffer size error:Get buffer size failed");
                    return 0;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Buffer size error:No Buffer");
                return 0;
            }
            nBuffersz = (uint)Marshal.ReadInt64(pBuffersz);
            Marshal.FreeHGlobal(pBuffersz);
            return nBuffersz;
        }

        /*
         *@brief:设置相机long类型特征值
         *@param [in] sFeatureName,nFeatureValue:特征名，特征值
         *@return:是否设置成功
         */
        public bool SetFeatureInt64(string sFeatureName, long nFeatureValue)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            res = IKapCLib.ItkDevSetInt64(m_hDev, sFeatureName, nFeatureValue);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Set feature error:Set int64 feature failed");
                return false;
            }
            return true;
        }

        /*
         *@brief:设置相机double类型特征值
         *@param [in] sFeatureName,fFeatureValue:特征名，特征值
         *@return:是否设置成功
         */
        public bool SetFeatureDouble(string sFeatureName, double fFeatureValue)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            res = IKapCLib.ItkDevSetDouble(m_hDev, sFeatureName, fFeatureValue);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Set feature error:Set double feature failed");
                return false;
            }
            return true;
        }

        /*
         *@brief:设置相机string类型特征值
         *@param [in] sFeatureName,sFeatureValue:特征名，特征值
         *@return:是否设置成功
         */
        public bool SetFeatureString(string sFeatureName, string sFeatureValue)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            IntPtr itkFeature = new IntPtr(-1);
            res = IKapCLib.ItkDevAllocFeature(m_hDev, sFeatureName, ref itkFeature);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Set feature error:Allocate string feature failed");
                return false;
            }
            res = IKapCLib.ItkFeatureFromString(itkFeature, sFeatureValue);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Set feature error:Set string feature failed");
                return false;
            }
            res = IKapCLib.ItkDevFreeFeature(itkFeature);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Set feature error:Free string feature failed");
                return false;
            }
            return true;
        }

        /*
         *@brief:设置相机command类型特征值
         *@param [in] sFeatureName:特征名
         *@return:是否设置成功
         */
        public bool SetFeatureCommand(string sFeatureName)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            IntPtr itkFeature = new IntPtr(-1);
            res = IKapCLib.ItkDevAllocFeature(m_hDev, sFeatureName, ref itkFeature);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Set feature error:Allocate command feature failed");
                return false;
            }
            res = IKapCLib.ItkFeatureExecuteCommand(itkFeature);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Set feature error:Set command feature failed");
                return false;
            }
            res = IKapCLib.ItkDevFreeFeature(itkFeature);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Set feature error:Free command feature failed");
                return false;
            }
            return true;
        }

        /*
         *@brief:设置相机触发模式
         *@param [in] bTrigger:触发状态布尔值
         *@return:是否设置成功
         */
        public bool SetTriggerMode(bool bTrigger)
        {
            //USB相机设置触发模式的指令和GV相机不同
            if (bTrigger)
            {
                if(m_sCameraType == "USB3Vision")
                {
                    if (!SetFeatureString("ExposureMode", "TriggerPulse"))
                    {
                        System.Diagnostics.Debug.WriteLine("Enable trigger mode error:Set trigger mode failed");
                        return false;
                    }
                    return true;
                }
                if (!SetFeatureString("TriggerSelector", "FrameStart"))
                {
                    System.Diagnostics.Debug.WriteLine("Enable trigger mode error:Set trigger selector failed");
                    return false;
                }
                if (!SetFeatureString("TriggerMode", "On"))
                {
                    System.Diagnostics.Debug.WriteLine("Enable trigger mode error:Enable trigger mode failed");
                    return false;
                }
            }
            else
            {
                if (m_sCameraType == "USB3Vision")
                {
                    if (!SetFeatureString("ExposureMode", "Timed"))
                    {
                        System.Diagnostics.Debug.WriteLine("Enable trigger mode error:Set trigger mode failed");
                        return false;
                    }
                    return true;
                }
                if (!SetFeatureString("TriggerSelector", "FrameStart"))
                {
                    System.Diagnostics.Debug.WriteLine("Disable trigger mode error:Set trigger selector failed");
                    return false;
                }
                if (!SetFeatureString("TriggerMode", "Off"))
                {
                    System.Diagnostics.Debug.WriteLine("Disable trigger mode error:Disable trigger mode failed");
                    return false;
                }
            }
            return true;
        }

        /*
         *@brief:创建采集数据流和缓冲区
         *@param [in] nStreamIndex,nBufferCount:数据流索引，缓冲区个数
         *@return:是否创建成功
         */
        public bool CreateStreamAndBuffer(uint nStreamIndex, int nBufferCount)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            uint nPixelForamt = GetPixelFormat();
            Int64 nWidth = 0;
            Int64 nHeight = 0;
            GetFeatureInt64("Width", ref nWidth);
            GetFeatureInt64("Height", ref nHeight);
            m_nFrameCount = nBufferCount;
            IntPtr hBuffer = new IntPtr();
            //创建第一个缓冲区，设置为数据流默认缓冲区
            res = IKapCLib.ItkBufferNew(nWidth, nHeight, nPixelForamt, ref hBuffer);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Init stream error:Create first buffer failed");
                return false;
            }
            m_hBufferList.Add(hBuffer);
            res = IKapCLib.ItkDevAllocStream(m_hDev, nStreamIndex, hBuffer, ref m_hStream);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Init stream error:Allocate buffer failed");
                return false;
            }
            //根据缓冲区个数创建剩余缓冲区并添加进数据流中
            for (int i = 1; i < m_nFrameCount; i++)
            {
                res = IKapCLib.ItkBufferNew(nWidth, nHeight, nPixelForamt, ref hBuffer);
                if (!Check(res))
                {
                    System.Diagnostics.Debug.WriteLine("Init stream error:Create new buffer failed");
                    return false;
                }
                res = IKapCLib.ItkStreamAddBuffer(m_hStream, hBuffer);
                if (!Check(res))
                {
                    System.Diagnostics.Debug.WriteLine("Init stream error:Add new buffer to stream failed");
                    return false;
                }
                m_hBufferList.Add(hBuffer);
            }
            return true;
        }

        /*
         *@brief:设置数据流
         *@param [in]:
         *@return:是否设置成功
         */
        private bool ConfigureStream()
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            //相机传输模式
            IntPtr xferMode = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(xferMode, 0, (int)ItkStreamTransferMode.ITKSTREAM_VAL_TRANSFER_MODE_SYNCHRONOUS_WITH_PROTECT);
            //采集流采集模式
            IntPtr startMode = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(startMode, 0, (int)ItkStreamStartMode.ITKSTREAM_VAL_START_MODE_NON_BLOCK);
            //采集超时时间
            IntPtr timeOut = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(timeOut, 0, (int)IKapCLib.ITKSTREAM_CONTINUOUS);

            res = IKapCLib.ItkStreamSetPrm(m_hStream, (uint)ItkStreamPrm.ITKSTREAM_PRM_START_MODE, startMode);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Configure stream error:Set start mode failed");
                Marshal.FreeHGlobal(startMode);
                return false;
            }
            res = IKapCLib.ItkStreamSetPrm(m_hStream, (uint)ItkStreamPrm.ITKSTREAM_PRM_TIME_OUT, timeOut);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Configure stream error:Set time out failed");
                Marshal.FreeHGlobal(timeOut);
                return false;
            }
            res = IKapCLib.ItkStreamSetPrm(m_hStream, (uint)ItkStreamPrm.ITKSTREAM_PRM_TRANSFER_MODE, xferMode);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Configure stream error:Set transfer mode failed");
                Marshal.FreeHGlobal(xferMode);
                return false;
            }
            Marshal.FreeHGlobal(xferMode);
            Marshal.FreeHGlobal(startMode);
            Marshal.FreeHGlobal(timeOut);
            return true;
        }

        /*
         *@brief:开始采集
         *@param [in] nGrabCount:采集次数
         *@return:是否成功开始采集
         */
        public bool StartGrab(uint nGrabCount)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            if (ConfigureStream())
            {
                res = IKapCLib.ItkStreamStart(m_hStream, nGrabCount);
                if (!Check(res))
                {
                    System.Diagnostics.Debug.WriteLine("Start grab error:Start stream failed");
                    return false;
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        /*
         *@brief:停止采集
         *@param [in]:
         *@return:是否成功停止采集
         */
        public bool StopGrab()
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            res = IKapCLib.ItkStreamStop(m_hStream);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Stop grab error:Stop stream failed");
                return false;
            }
            return true;
        }

        /*
         *@brief:释放申请的采集数据流和缓冲区
         *@param [in]:
         *@return:是否释放成功
         */
        public bool FreeStreamAndBuffer()
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            for (int i = 0; i < m_nFrameCount; i++)
            {
                res = IKapCLib.ItkStreamRemoveBuffer(m_hStream, m_hBufferList[i]);
                if (!Check(res))
                {
                    System.Diagnostics.Debug.WriteLine("Stop grab error:Remove buffer from stream failed");
                    return false;
                }
                res = IKapCLib.ItkBufferFree(m_hBufferList[i]);
                if (!Check(res))
                {
                    System.Diagnostics.Debug.WriteLine("Stop grab error:Free buffer failed");
                    return false;
                }
            }
            res = IKapCLib.ItkDevFreeStream(m_hStream);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Stop grab error:Free stream failed");
                return false;
            }
            m_hBufferList.Clear();
            return true;
        }

        /*
         *@brief:等待当前数据流采集完成
         *@param [in]:
         *@return:是否成功
         */
        public bool WaitGrab()
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            res = IKapCLib.ItkStreamWait(m_hStream);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Stop grab error:Wait stream end failed");
                return false;
            }
            FreeStreamAndBuffer();
            return true;
        }

        /*
         *@brief:注册回调函数
         *@param [in] nEventType,callback,context:回调事件类型，回调函数指针，上下文指针
         *@return:是否注册成功
         */
        public bool RegisterCallBack(uint nEventType, IKapCLib.PITKSTREAMCALLBACK callback, IntPtr context)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            res = IKapCLib.ItkStreamRegisterCallback(m_hStream, nEventType, callback, context);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Call back function error:Register call back function failed");
                return false;
            }
            return true;
        }

        /*
         *@brief:注销回调函数
         *@param [in] nEvenType:回调事件类型
         *@return:是否注销成功
         */
        public bool UnregisterCallBack(uint nEvenType)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            res = IKapCLib.ItkStreamUnregisterCallback(m_hStream, nEvenType);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Call back function error:Unregister call back function failed");
                return false;
            }
            return true;
        }

        /*
         *@brief:读取图片数据到指定内存
         *@param [in] offset,pData,nBufferIndex:偏置，目标内存指针，采集帧索引
         *@return:是否读取成功
         */
        public bool ReadData(uint offset, IntPtr pData, int nBufferIndex)
        {
            uint res = (uint)ItkStatusErrorId.ITKSTATUS_OK;
            uint nBufferSize = GetBufferSize();
            uint status = 0;
            IntPtr bufferStatus = Marshal.AllocHGlobal(4);
            res = IKapCLib.ItkBufferGetPrm(m_hBufferList[nBufferIndex], (uint)ItkBufferPrm.ITKBUFFER_PRM_STATE, bufferStatus);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Read data error:Get buffer status failed");
                return false;
            }
            //获取缓冲区状态，缓冲区状态为full时进行读取
            status = (uint)Marshal.ReadInt32(bufferStatus);
            if (status != (uint)ItkBufferState.ITKBUFFER_VAL_STATE_FULL)
            {
                System.Diagnostics.Debug.WriteLine("Read data error:Buffer is not full");
                return false;
            }
            Marshal.FreeHGlobal(bufferStatus);
            res = IKapCLib.ItkBufferRead(m_hBufferList[nBufferIndex], offset, pData, nBufferSize);
            if (!Check(res))
            {
                System.Diagnostics.Debug.WriteLine("Read data error:Read data from buffer failed");
                return false;
            }
            return true;
        }
    }
}
