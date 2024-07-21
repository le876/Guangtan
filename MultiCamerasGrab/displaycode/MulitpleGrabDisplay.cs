using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Yolov5Net.Scorer;
using Yolov5Net.Scorer.Models;
using IKapC.NET;
namespace MultiCamerasGrab
{
    public partial class MulitpleGrabDisplay : Form
    {
        //最多可以同时采集的相机个数，可以自行根据需求修改
        static public int MAX_DEVICE_COUNT = 4;
        //相机控制类
        public CameraManager[] m_Camera = new CameraManager[MAX_DEVICE_COUNT];
        //数据转换类
        public BufferManager[] m_Buffer = new BufferManager[MAX_DEVICE_COUNT];
        //使用相机个数
        public int m_nUseCount = 0;
        //连接相机个数
        public int m_nDeviceCount = 0;

        //UI控制值，用于更新UI界面
        public bool bGrab = false;
        private bool bOpen = false;
        private bool bTrigger = false;
        public bool bContinuous = false;
        public bool[] bShow = new bool[MAX_DEVICE_COUNT+2];

        //线程同步上下文：图片刷新、采集结束
        public SynchronizationContext m_imgUpdateSync;
        public SynchronizationContext m_grabStopSync;

        //回调函数声明
        #region Callback Declare
        public IKapCLib.PITKSTREAMCALLBACK[] cbOnStartOfStreamProc = new IKapCLib.PITKSTREAMCALLBACK[MAX_DEVICE_COUNT];
        public IKapCLib.PITKSTREAMCALLBACK[] cbOnEndOfFrameProc = new IKapCLib.PITKSTREAMCALLBACK[MAX_DEVICE_COUNT];
        public IKapCLib.PITKSTREAMCALLBACK[] cbOnTimeOutProc = new IKapCLib.PITKSTREAMCALLBACK[MAX_DEVICE_COUNT];
        public IKapCLib.PITKSTREAMCALLBACK[] cbOnFrameLostProc = new IKapCLib.PITKSTREAMCALLBACK[MAX_DEVICE_COUNT];
        public IKapCLib.PITKSTREAMCALLBACK[] cbOnEndOfStreamProc = new IKapCLib.PITKSTREAMCALLBACK[MAX_DEVICE_COUNT];
        #endregion

        //回调函数定义
        #region Callback Define
        public void cbOnStartOfStreamFunc(uint eventType, IntPtr pContext)
        {
            System.Diagnostics.Debug.WriteLine("Stream start");
        }
        public void cbOnTimeOutFunc(uint eventType, IntPtr pContext)
        {
            System.Diagnostics.Debug.WriteLine("Timeout");
        }
        public void cbOnFrameLostFunc(uint eventType, IntPtr pContext)
        {
            System.Diagnostics.Debug.WriteLine("Frame lost");
        }
        public void cbOnEndOfStreamFunc(uint eventType, IntPtr pContext)
        {
            System.Diagnostics.Debug.WriteLine("Stream end");
            bGrab = false;
        }
        public void cbOnEndOfFrameFunc(uint eventType, IntPtr pContext)
        {
            int nIndex = pContext.ToInt32();
            lock(m_Buffer[nIndex].m_bufferLock)
            {
                m_Camera[nIndex].ReadData(0, m_Buffer[nIndex].m_pBuffer, m_Camera[nIndex].m_nFrameIndex);
                m_Buffer[nIndex].bUpdateImg = true;
            }
            m_Camera[nIndex].m_nFrameIndex++;
            m_Camera[nIndex].m_nFrameIndex = m_Camera[nIndex].m_nFrameIndex % m_Camera[nIndex].m_nFrameCount;
        }
        #endregion

        //数据转换线程
        static void WorkThread(object obj)
        {
            MulitpleGrabDisplay hDisplay = obj as MulitpleGrabDisplay;
            while(hDisplay.bGrab)
            {
                //单帧或多帧采集时使用WaitGrab()等待采集结束
                if(!hDisplay.bContinuous && !hDisplay.bTrigger)
                {
                    for (int i = 0 ; i < hDisplay.m_nUseCount; i++)
                    {
                        hDisplay.m_Camera[i].WaitGrab();
                    }
                }
                Thread.Sleep(30);
                for(int i = 0; i < hDisplay.m_nUseCount; i++)
                {
                    if(hDisplay.m_Buffer[i].bUpdateImg)
                    {
                        hDisplay.m_Buffer[i].ReadImage();
                        hDisplay.m_imgUpdateSync.Post(hDisplay.ImageUpdateSyncContext, null);
                    }
                }
            }
            hDisplay.m_grabStopSync.Post(hDisplay.GrabStopSyncContext, null);
        }

        //完成数据转换后委托UI进行图片显示
        private void ImageUpdateSyncContext(object obj)
        {
            lock(m_Buffer[0].m_imageLock)
            {
                if (m_Buffer[0].m_Bmp == null)
                {
                    UpdateUI();
                    return;
                }
                if(this.pictureBoxShow1.Image != null)
                {
                    this.pictureBoxShow1.Image.Dispose();
                }
                this.pictureBoxShow1.Image = (Image)(m_Buffer[0].m_Bmp.Clone());
                bShow[0] = true;
            }
            UpdateUI();
            lock (m_Buffer[1].m_imageLock)
            {
                if(m_Buffer[1].m_Bmp == null)
                {
                    UpdateUI();
                    return;
                }
                if (this.pictureBoxShow2.Image != null)
                {
                    this.pictureBoxShow2.Image.Dispose();
                }
                this.pictureBoxShow2.Image = (Image)(m_Buffer[1].m_Bmp.Clone());
                bShow[1] = true;
            }
            UpdateUI();
            lock (m_Buffer[2].m_imageLock)
            {
                if (m_Buffer[2].m_Bmp == null)
                {
                    UpdateUI();
                    return;
                }
                if (this.pictureBoxShow3.Image != null)
                {
                    this.pictureBoxShow3.Image.Dispose();
                }
                this.pictureBoxShow3.Image = (Image)(m_Buffer[2].m_Bmp.Clone());
                bShow[2] = true;
            }
            UpdateUI();
            lock (m_Buffer[3].m_imageLock)
            {
                if (m_Buffer[3].m_Bmp == null)
                {
                    UpdateUI();
                    return;
                }
                if (this.pictureBoxShow4.Image != null)
                {
                    this.pictureBoxShow4.Image.Dispose();
                }
                this.pictureBoxShow4.Image = (Image)(m_Buffer[3].m_Bmp.Clone());
                bShow[3] = true;
            }
            UpdateUI();

           

   

        }

        //完全结束采集后刷新UI
        private void GrabStopSyncContext(object obj)
        {
            bGrab = false;
            UpdateUI();
        }

        //刷新UI
        public void UpdateUI()
        {
            if (bOpen)
            {
                this.buttonOpen.Enabled = false;
                this.buttonClose.Enabled = !bGrab;
                this.buttonGet.Enabled = !bGrab;
                this.buttonSet.Enabled = !bGrab;
                this.buttonStartGrab.Enabled = !bGrab;
                this.buttonStopGrab.Enabled = bGrab;
                this.buttonSoftTrigger.Enabled = bTrigger;
                this.buttonSave.Enabled = !bGrab;
                this.radioButtonContinuous.Enabled = !bGrab;
                this.radioButtonOnce.Enabled = !bGrab;
                this.radioButtonTrigger.Enabled = !bGrab;
                this.buttonProbe.Enabled = false;
            }
            else
            {
                this.buttonOpen.Enabled = true;
                this.buttonClose.Enabled = false;
                this.buttonGet.Enabled = false;
                this.buttonSet.Enabled = false;
                this.buttonStartGrab.Enabled = false;
                this.buttonStopGrab.Enabled = false;
                this.buttonSoftTrigger.Enabled = false;
                this.buttonSave.Enabled = true;
                this.buttonProbe.Enabled = true;
            }
        }

        public MulitpleGrabDisplay()
        {
            InitializeComponent();
            for(int i = 0; i < MAX_DEVICE_COUNT; i++)
            {
                m_Camera[i] = new CameraManager();
                m_Buffer[i] = new BufferManager();
                bShow[i] = false;
            }
            this.radioButtonContinuous.Checked = true;           
            m_imgUpdateSync = SynchronizationContext.Current;
            m_grabStopSync = SynchronizationContext.Current;

            UpdateUI();
        }

        private void buttonProbe_Click(object sender, EventArgs e)
        {
            uint nIndex = 0;
            //获取连接的GV相机个数，这里可以通过修改第二个参数来获取连接的其他类型相机个数
            m_nDeviceCount = m_Camera[0].GetConnectedCameraCount(ref nIndex, "GigEVision");
            if (m_nDeviceCount == -1)
            {
                MessageBox.Show("No cameras!", "Warning", MessageBoxButtons.OK);
                return;
            }
            this.textBoxDeviceCount.Text = m_nDeviceCount.ToString();
            this.textBoxOpenDeviceCount.Text = "1";
            for(uint i = 0; i < m_nDeviceCount; i++)
            {
                var pDI = m_Camera[0].GetDeviceInfo(nIndex);
                string name = pDI.FullName;
                this.comboBoxInfo.Items.Add(name);
                nIndex++;
            }
            this.comboBoxInfo.SelectedIndex = 0;
        }

        private void buttonOpen_Click(object sender, EventArgs e)
        {
            uint nIndex = 0;
            m_Camera[0].GetConnectedCameraCount(ref nIndex, "GigEVision");
            this.textBoxDeviceCount.Text = m_nDeviceCount.ToString();
            m_nUseCount = Convert.ToInt32(this.textBoxOpenDeviceCount.Text);
            if(m_nUseCount > m_nDeviceCount)
            {
                m_nUseCount = m_nDeviceCount;
            }
            else if(m_nUseCount > MAX_DEVICE_COUNT)
            {
                m_nUseCount = MAX_DEVICE_COUNT;
            }
            //根据索引打开相机
            for(uint i = 0; i < m_nUseCount; i++)
            {
                bOpen = m_Camera[i].OpenCamera(nIndex + i);
                if (!bOpen)
                {
                    MessageBox.Show("Open failure", "Warning", MessageBoxButtons.OK);
                    return;
                }
            }
            UpdateUI();
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            for(int i = 0; i < m_nUseCount; i++)
            {
                m_Camera[i].CloseCamera();
            }
            bOpen = false;
            UpdateUI();
        }

        private void buttonStartGrab_Click(object sender, EventArgs e)
        {
            IntPtr hPtr = new IntPtr(-1);
            uint nGrabCount = 1;
            //获取采集模式
            bContinuous = this.radioButtonContinuous.Checked;
            bTrigger = this.radioButtonTrigger.Checked;
            //连续采集和触发模式的采集次数均需设置为ITKSTREAM_CONTINUOUS
            if (bContinuous || bTrigger)
                nGrabCount = (uint)IKapCLib.ITKSTREAM_CONTINUOUS;
            for(int i = 0; i < m_nUseCount; i++)
            {
                m_Camera[i].SetTriggerMode(bTrigger);
                m_Camera[i].CreateStreamAndBuffer(0, 5);
                hPtr = (IntPtr)i;
                //注册采集开始回调
                cbOnStartOfStreamProc[i] = new IKapCLib.PITKSTREAMCALLBACK(cbOnStartOfStreamFunc);
                if (!m_Camera[i].RegisterCallBack((uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_START_OF_STREAM, cbOnStartOfStreamProc[i], hPtr))
                    return;

                //注册采集超时回调
                cbOnTimeOutProc[i] = new IKapCLib.PITKSTREAMCALLBACK(cbOnTimeOutFunc);
                if (!m_Camera[i].RegisterCallBack((uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_TIME_OUT, cbOnTimeOutProc[i], hPtr))
                    return;

                //注册采集丢帧回调
                cbOnFrameLostProc[i] = new IKapCLib.PITKSTREAMCALLBACK(cbOnFrameLostFunc);
                if (!m_Camera[i].RegisterCallBack((uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_FRAME_LOST, cbOnFrameLostProc[i], hPtr))
                    return;

                //注册采集结束回调
                cbOnEndOfStreamProc[i] = new IKapCLib.PITKSTREAMCALLBACK(cbOnEndOfStreamFunc);
                if (!m_Camera[i].RegisterCallBack((uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_END_OF_STREAM, cbOnEndOfStreamProc[i], hPtr))
                    return;

                //注册帧结束回调
                cbOnEndOfFrameProc[i] = new IKapCLib.PITKSTREAMCALLBACK(cbOnEndOfFrameFunc);
                if (!m_Camera[i].RegisterCallBack((uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_END_OF_FRAME, cbOnEndOfFrameProc[i], hPtr))
                    return;

                //创建数据转换缓冲区和BITMAP图像
                m_Buffer[i].CreateDataBufferAndBitmap(m_Camera[i]);
                m_Camera[i].m_nFrameIndex = 0;
                if(!m_Camera[i].StartGrab(nGrabCount))
                {
                    MessageBox.Show("Start grab failure", "Warning", MessageBoxButtons.OK);
                    return;
                }
            }
            bGrab = true;
            //开启数据转换线程
            Thread thread = new Thread(new ParameterizedThreadStart(WorkThread));
            thread.Start(this);
            UpdateUI();
        }

        private void buttonStopGrab_Click(object sender, EventArgs e)
        {
            for(int i = 0; i < m_nUseCount; i++)
            {
                //停止采集
                m_Camera[i].StopGrab();
                //注销回调函数
                m_Camera[i].UnregisterCallBack((uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_START_OF_STREAM);
                m_Camera[i].UnregisterCallBack((uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_END_OF_STREAM);
                m_Camera[i].UnregisterCallBack((uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_FRAME_LOST);
                m_Camera[i].UnregisterCallBack((uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_TIME_OUT);
                m_Camera[i].UnregisterCallBack((uint)ItkStreamEventType.ITKSTREAM_VAL_EVENT_TYPE_END_OF_FRAME);
                //清空申请的采集数据流和数据转换资源
                m_Camera[i].FreeStreamAndBuffer();
                m_Buffer[i].ReleaseBuffer();
            }
            bGrab = false;
        }

        private void buttonSoftTrigger_Click(object sender, EventArgs e)
        {
            for(int i = 0; i < m_nUseCount; i++)
            {
                m_Camera[i].SetFeatureCommand("TriggerSoftware");
            }
        }

        private void buttonGet_Click(object sender, EventArgs e)
        {
            //USB相机ExposureTime参数为Int64类型，这里使用GV相机的参数设置作为范例
            //如需获取USB相机ExposureTime参数，请自行将该参数设置为Int64类型
            //USB相机DigitalGain参数的特征名为Gain，这里使用GV相机的参数设置作为范例
            //如需获取USB相机DigitalGain参数，请自行将参数特征名更改为Gain
            int nIndex = this.comboBoxInfo.SelectedIndex;
            long nHeight = 0;
            double dExp = 0;
            double dGain = 0;
            m_Camera[nIndex].GetFeatureInt64("Height", ref nHeight);
            m_Camera[nIndex].GetFeatureDouble("ExposureTime", ref dExp);
            m_Camera[nIndex].GetFeatureDouble("DigitalGain", ref dGain);

            this.textBoxHeight.Text = nHeight.ToString();
            this.textBoxExp.Text = dExp.ToString();
            this.textBoxGain.Text = dGain.ToString();
        }

        private void buttonSet_Click(object sender, EventArgs e)
        {
            //USB相机ExposureTime参数为Int64类型，这里使用GV相机的参数设置作为范例
            //如需设置USB相机ExposureTime参数，请自行将该参数设置为Int64类型
            //USB相机DigitalGain参数的特征名为Gain，这里使用GV相机的参数设置作为范例
            //如需设置USB相机DigitalGain参数，请自行将参数特征名更改为Gain
            int nIndex = this.comboBoxInfo.SelectedIndex;
            long nHeight = Convert.ToInt64(this.textBoxHeight.Text);
            double dExp = Convert.ToDouble(this.textBoxExp.Text);
            double dGain = Convert.ToDouble(this.textBoxGain.Text);

            if(m_Camera[nIndex].SetFeatureInt64("Height", nHeight)     && 
               m_Camera[nIndex].SetFeatureDouble("ExposureTime", dExp) &&
               m_Camera[nIndex].SetFeatureDouble("DigitalGain", dGain)   )
            {
                MessageBox.Show("Set features success", "Result", MessageBoxButtons.OK);
            }
            else
            {
                MessageBox.Show("Set features failure", "Result", MessageBoxButtons.OK);
            }
        }
        private Bitmap CropImage(Bitmap source, int x, int y, int width, int height)
        {
            Rectangle cropArea = new Rectangle(x, y, width, height);
            Bitmap croppedImage = source.Clone(cropArea, source.PixelFormat);
            return croppedImage;
        }

        private void pingyi(object sender, EventArgs e)
        {
            //图像平移
            Bitmap I1 = (Bitmap)(this.pictureBoxShow1.Image.Clone());
            Bitmap I1f = (Bitmap)(this.pictureBoxShow1.Image.Clone());
            BitmapData bmpData1 = I1.LockBits(new Rectangle(0, 0, I1.Width, I1.Height), ImageLockMode.ReadWrite, I1.PixelFormat);
            BitmapData bmpData1f = I1f.LockBits(new Rectangle(0, 0, I1f.Width, I1f.Height), ImageLockMode.ReadWrite, I1f.PixelFormat);
            int width = I1.Width; int height = I1.Height;
            int size = width * height;
            byte[] I1_ = new byte[size];
            byte[] I1_f = new byte[size];
            IntPtr ptr1 = bmpData1.Scan0;
            IntPtr ptr1f = bmpData1f.Scan0;
            Marshal.Copy(ptr1, I1_, 0, size);
            Marshal.Copy(ptr1f, I1_f, 0, size);
            int t1 = 158;//纵向
            int z1 = 142;//水平-
            for (int i = 0; i < height - t1; i++)//
            {
                for (int j = z1; j < width; j++)
                {
                    I1_[(i + t1) * width + j - z1] = I1_f[i * width + j];
                }
            }
            for (int i = height - t1; i < height; i++)
            {
                for (int j = 0; j < z1; j++)
                {
                    I1_[(i + t1 - height) * width + j + width - z1] = I1_f[i * width + j];
                }
            }

            for (int i = height - t1; i < height; i++)
            {
                for (int j = z1; j < width; j++)
                {
                    I1_[(i + t1 - height) * width + j - z1] = I1_f[i * width + j];
                }
            }
            for (int i = 0; i < height - t1; i++)
            {
                for (int j = 0; j < z1; j++)
                {
                    I1_[(i + t1) * width + j + width - z1] = I1_f[i * width + j];
                }
            }
            
            I1.UnlockBits(bmpData1);
            I1f.UnlockBits(bmpData1f);
            Marshal.Copy(I1_, 0, ptr1, size);
            // 确定裁剪区域，例如裁剪平移后图像的中心区域
            int cropX = 50; // 裁剪区域的起始 X 坐标
            int cropY = 50; // 裁剪区域的起始 Y 坐标
            int cropWidth = I1.Width - 100; // 裁剪区域的宽度
            int cropHeight = I1.Height - 100; // 裁剪区域的高度

            // 执行裁剪操作
            Bitmap croppedImage = CropImage(I1, cropX, cropY, cropWidth, cropHeight);

            // 更新pictureBoxShow1的图像为裁剪后的图像
            this.pictureBoxShow1.Image = croppedImage;
            //this.pictureBoxShow1.Image = (Image)(I1.Clone());
            bShow[0] = true;
            UpdateUI();

            Bitmap I2 = (Bitmap)(this.pictureBoxShow2.Image.Clone());
            Bitmap I2f = (Bitmap)(this.pictureBoxShow2.Image.Clone());
            BitmapData bmpData2 = I2.LockBits(new Rectangle(0, 0, I2.Width, I2.Height), ImageLockMode.ReadWrite, I2.PixelFormat);
            BitmapData bmpData2f = I2f.LockBits(new Rectangle(0, 0, I2f.Width, I2f.Height), ImageLockMode.ReadWrite, I2f.PixelFormat);

            byte[] I2_ = new byte[size];
            byte[] I2_f = new byte[size];
            IntPtr ptr2 = bmpData2.Scan0;
            IntPtr ptr2f = bmpData2f.Scan0;
            Marshal.Copy(ptr2, I2_, 0, size);
            Marshal.Copy(ptr2f, I2_f, 0, size);
            int t2 = 57;//纵向
            int z2 = 0;//水平..157
            for (int i = 0; i < height - t2; i++)
            {
                for (int j = z2; j < width; j++)
                {
                    I2_[(i + t2) * width + j - z2] = I2_f[i * width + j];
                }
            }
            for (int i = height - t2; i < height; i++)
            {
                for (int j = 0; j < z2; j++)
                {
                    I2_[(i + t2 - height) * width + j + width - z2] = I2_f[i * width + j];
                }
            }

            for (int i = height - t2; i < height; i++)
            {
                for (int j = z2; j < width; j++)
                {
                    I2_[(i + t2 - height) * width + j - z2] = I2_f[i * width + j];
                }
            }
            for (int i = 0; i < height - t2; i++)
            {
                for (int j = 0; j < z2; j++)
                {
                    I2_[(i + t2) * width + j + width - z2] = I2_f[i * width + j];
                }
            }
            Marshal.Copy(I2_, 0, ptr2, size);
            I2.UnlockBits(bmpData2);
            I2f.UnlockBits(bmpData2f);
            this.pictureBoxShow2.Image = (Image)(I2.Clone());
            bShow[1] = true;
            UpdateUI();

            Bitmap I3 = (Bitmap)(this.pictureBoxShow3.Image.Clone());
            Bitmap I3f = (Bitmap)(this.pictureBoxShow3.Image.Clone());
            BitmapData bmpData3 = I3.LockBits(new Rectangle(0, 0, I3.Width, I3.Height), ImageLockMode.ReadWrite, I3.PixelFormat);
            BitmapData bmpData3f = I3f.LockBits(new Rectangle(0, 0, I3f.Width, I3f.Height), ImageLockMode.ReadWrite, I3f.PixelFormat);

            byte[] I3_ = new byte[size];
            byte[] I3_f = new byte[size];
            IntPtr ptr3 = bmpData3.Scan0;
            IntPtr ptr3f = bmpData3f.Scan0;
            Marshal.Copy(ptr3, I3_, 0, size);
            Marshal.Copy(ptr3f, I3_f, 0, size);
            int t3 = 100;//纵向
            int z3 = 1168;//水平..1133--

            for (int i = 0; i < height - t3; i++)
            {
                for (int j = z3; j < width; j++)
                {
                    I3_[(i + t3) * width + j - z3] = I3_f[i * width + j];
                }
            }
            for (int i = height - t3; i < height; i++)
            {
                for (int j = 0; j < z3; j++)
                {
                    I3_[(i + t3 - height) * width + j + width - z3] = I3_f[i * width + j];
                }
            }

            for (int i = height - t3; i < height; i++)
            {
                for (int j = z3; j < width; j++)
                {
                    I3_[(i + t3 - height) * width + j - z3] = I3_f[i * width + j];
                }
            }
            for (int i = 0; i < height - t3; i++)
            {
                for (int j = 0; j < z3; j++)
                {
                    I3_[(i + t3) * width + j + width - z3] = I3_f[i * width + j];
                }
            }
            Marshal.Copy(I3_, 0, ptr3, size);
            I3.UnlockBits(bmpData3);
            I3f.UnlockBits(bmpData3f);
            this.pictureBoxShow3.Image = (Image)(I3.Clone());
            bShow[2] = true;
            UpdateUI();
            //
            Bitmap I4 = (Bitmap)(this.pictureBoxShow4.Image.Clone());
            Bitmap I4f = (Bitmap)(this.pictureBoxShow4.Image.Clone());
            BitmapData bmpData4 = I4.LockBits(new Rectangle(0, 0, I4.Width, I4.Height), ImageLockMode.ReadWrite, I4.PixelFormat);
            BitmapData bmpData4f = I4f.LockBits(new Rectangle(0, 0, I4f.Width, I4f.Height), ImageLockMode.ReadWrite, I4f.PixelFormat);

            byte[] I4_ = new byte[size];
            byte[] I4_f = new byte[size];
            IntPtr ptr4 = bmpData4.Scan0;
            IntPtr ptr4f = bmpData4f.Scan0;
            Marshal.Copy(ptr4, I4_, 0, size);
            Marshal.Copy(ptr4f, I4_f, 0, size);
            int t4 = 0;//纵向
            int z4 = 1125;//水平..1133--

            for (int i = 0; i < height - t4; i++)
            {
                for (int j = z4; j < width; j++)
                {
                    I4_[(i + t4) * width + j - z4] = I4_f[i * width + j];
                }
            }
            for (int i = height - t4; i < height; i++)
            {
                for (int j = 0; j < z4; j++)
                {
                    I4_[(i + t4 - height) * width + j + width - z4] = I4_f[i * width + j];
                }
            }

            for (int i = height - t4; i < height; i++)
            {
                for (int j = z4; j < width; j++)
                {
                    I4_[(i + t4 - height) * width + j - z4] = I4_f[i * width + j];
                }
            }
            for (int i = 0; i < height - t4; i++)
            {
                for (int j = 0; j < z4; j++)
                {
                    I4_[(i + t4) * width + j + width - z4] = I4_f[i * width + j];
                }
            }
            Marshal.Copy(I4_, 0, ptr4, size);
            I4.UnlockBits(bmpData4);
            I4f.UnlockBits(bmpData4f);
            this.pictureBoxShow4.Image = (Image)(I4.Clone());
            bShow[3] = true;
            UpdateUI();

        }

        private void AOP(object sender, EventArgs e)
        {
            //图像拼接I0 + I90 = Ip1
            Bitmap I0 = (Bitmap)(this.pictureBoxShow2.Image.Clone());
            Bitmap I90 = (Bitmap)(this.pictureBoxShow4.Image.Clone());
            Bitmap Ip1 = (Bitmap)(this.pictureBoxShow4.Image.Clone());
            BitmapData bmpDataI90 = I90.LockBits(new Rectangle(0, 0, I90.Width, I90.Height), ImageLockMode.ReadWrite, I90.PixelFormat);
            BitmapData bmpDataI0 = I0.LockBits(new Rectangle(0, 0, I0.Width, I0.Height), ImageLockMode.ReadWrite, I0.PixelFormat);
            BitmapData bmpDataIp1 = Ip1.LockBits(new Rectangle(0, 0, Ip1.Width, Ip1.Height), ImageLockMode.ReadWrite, Ip1.PixelFormat);
            int width = I0.Width; int height = I0.Height;
            int size = width * height;
            byte[] bg = new byte[size];
            byte[] I0ff = new byte[size];
            byte[] I90ff = new byte[size];
            byte[] Ip1ff = new byte[size];
            IntPtr ptrI0 = bmpDataI0.Scan0;
            IntPtr ptrI90 = bmpDataI90.Scan0;
            IntPtr ptrIp1 = bmpDataIp1.Scan0;
            Marshal.Copy(ptrI0, I0ff, 0, size);
            Marshal.Copy(ptrI90, I90ff, 0, size);
            double dI0 = 0.0, dI90 = 0.0;
            for (int i = 0; i < size; i = i + 2)
            {
                dI0 += (double)I0ff[i];
                dI90 += (double)I90ff[i];
            }
            dI0 = dI0 / size * 1.0;
            dI90 = dI90 / size * 1.0;

            for (int i = 0; i < size; i = i + 2)
            {
                if ((double)I0ff[i] / dI0 + (double)I90ff[i] / dI90 == 0) Ip1ff[i] = 0;
                else
                {
                    Ip1ff[i] = (byte)(255.0 * ((double)I0ff[i] / dI0 - (double)I90ff[i] / dI90) / ((double)I0ff[i] / dI0 + (double)I90ff[i] / dI90));
                }
            }
            Marshal.Copy(Ip1ff, 0, ptrIp1, size);
            I0.UnlockBits(bmpDataI0);
            I90.UnlockBits(bmpDataI90);
            Ip1.UnlockBits(bmpDataIp1);

            //图像拼接I45 + I135 = Ip2
            Bitmap I45 = (Bitmap)(this.pictureBoxShow1.Image.Clone());
            Bitmap I135 = (Bitmap)(this.pictureBoxShow3.Image.Clone());
            Bitmap Ip2 = (Bitmap)(this.pictureBoxShow3.Image.Clone());
            BitmapData bmpDataI135 = I135.LockBits(new Rectangle(0, 0, I135.Width, I135.Height), ImageLockMode.ReadWrite, I135.PixelFormat);
            BitmapData bmpDataI45 = I45.LockBits(new Rectangle(0, 0, I45.Width, I45.Height), ImageLockMode.ReadWrite, I45.PixelFormat);
            BitmapData bmpDataIp2 = Ip2.LockBits(new Rectangle(0, 0, Ip2.Width, Ip2.Height), ImageLockMode.ReadWrite, Ip2.PixelFormat);
            byte[] I45ff = new byte[size];
            byte[] I135ff = new byte[size];
            byte[] Ip2ff = new byte[size];
            IntPtr ptrI45 = bmpDataI45.Scan0;
            IntPtr ptrI135 = bmpDataI135.Scan0;
            IntPtr ptrIp2 = bmpDataIp2.Scan0;
            Marshal.Copy(ptrI45, I45ff, 0, size);
            Marshal.Copy(ptrI135, I135ff, 0, size);
            double dI45 = 0.0, dI135 = 0.0;
            for (int i = 0; i < size; i = i + 2)
            {
                dI45 += (double)I45ff[i];
                dI135 += (double)I135ff[i];
            }
            dI45 = dI45 / size * 1.0;
            dI135 = dI135 / size * 1.0;

            for (int i = 0; i < size; i = i + 2)
            {
                if ((double)I45ff[i] / dI45 + (double)I135ff[i] / dI135 == 0) Ip2ff[i] = 0;
                else
                {
                    Ip2ff[i] = (byte)(255.0 * ((double)I45ff[i] / dI45 - (double)I135ff[i] / dI135) / ((double)I45ff[i] / dI45 + (double)I135ff[i] / dI135));
                }
            }
            Marshal.Copy(Ip2ff, 0, ptrIp2, size);
            I45.UnlockBits(bmpDataI45);
            I135.UnlockBits(bmpDataI135);
            Ip2.UnlockBits(bmpDataIp2);
            /*
            //AOP计算
            Bitmap AOP = (Bitmap)(this.pictureBoxShow1.Image.Clone()); ;
            BitmapData bmpDataAOP = AOP.LockBits(new Rectangle(0, 0, AOP.Width, AOP.Height), ImageLockMode.ReadWrite, AOP.PixelFormat);
            byte[] AOPff = new byte[size];
            IntPtr ptrAOP = bmpDataAOP.Scan0;
            for (int i = 0; i < size; i = i + 2)
            {
                if ((double)Ip1ff[i] == 0 && (double)Ip2ff[i] == 0) AOPff[i] = 0;
                else
                {
                    if (0.5 * Math.Atan2((double)Ip2ff[i], (double)Ip1ff[i]) + 0.25 * Math.PI > 0.5 * Math.PI)
                    {
                        AOPff[i] = (byte)(255.0 * (0.5 * Math.Atan2((double)Ip1ff[i], (double)Ip2ff[i]) + 0.25 * Math.PI - Math.PI));
                    }
                    else
                    {
                        AOPff[i] = (byte)(255.0 * (0.5 * Math.Atan2((double)Ip1ff[i], (double)Ip2ff[i]) + 0.25 * Math.PI));
                    }
                }
            }
            Marshal.Copy(AOPff, 0, ptrAOP, size);
            AOP.UnlockBits(bmpDataAOP);
            this.pictureBoxShow6.Image = (Image)(AOP.Clone());//第六幅图的赋值，灰度图像
            bShow[4] = true;
            UpdateUI();
        }*/
            //DOLP计算
            Bitmap DOLP = (Bitmap)(this.pictureBoxShow1.Image.Clone()); ;
            BitmapData bmpDataDOLP = DOLP.LockBits(new Rectangle(0, 0, DOLP.Width, DOLP.Height), ImageLockMode.ReadWrite, DOLP.PixelFormat);
            byte[] DOLPff = new byte[size];
            IntPtr ptrDOLP = bmpDataDOLP.Scan0;
            for (int i = 0; i < size; i++)
            {
                int j = i % Width;
                DOLPff[i] = (byte)(Math.Sqrt(((double)Ip1ff[i] * (double)Ip1ff[i] + (double)Ip2ff[i] * (double)Ip2ff[i]) / 2.0));
                byte dolpvalue = (byte)(DOLPff[i]);
                dolpvalue = (byte)(dolpvalue - bg[j]);
                DOLPff[i]= (byte)(dolpvalue);
            }
            Marshal.Copy(DOLPff, 0, ptrDOLP, size);
            DOLP.UnlockBits(bmpDataDOLP);

            this.pictureBoxShow6.Image = (Image)(DOLP.Clone());//第五幅图的赋值
            bShow[4] = true;
            UpdateUI();
        }
      
        private void DOLP(object sender, EventArgs e)
        {
            //图像拼接I0 + I90 = Ip1
            Bitmap I0 = (Bitmap)(this.pictureBoxShow2.Image.Clone());
            Bitmap I90 = (Bitmap)(this.pictureBoxShow4.Image.Clone());
            Bitmap Ip1 = (Bitmap)(this.pictureBoxShow4.Image.Clone());
            BitmapData bmpDataI90 = I90.LockBits(new Rectangle(0, 0, I90.Width, I90.Height), ImageLockMode.ReadWrite, I90.PixelFormat);
            BitmapData bmpDataI0 = I0.LockBits(new Rectangle(0, 0, I0.Width, I0.Height), ImageLockMode.ReadWrite, I0.PixelFormat);
            BitmapData bmpDataIp1 = Ip1.LockBits(new Rectangle(0, 0, Ip1.Width, Ip1.Height), ImageLockMode.ReadWrite, Ip1.PixelFormat);
            int width = I0.Width; int height = I0.Height;
            int size = width * height;
            byte[] bg= new byte[size];
            byte[] I0ff = new byte[size];
            byte[] I90ff = new byte[size];
            byte[] Ip1ff = new byte[size];
            IntPtr ptrI0 = bmpDataI0.Scan0;
            IntPtr ptrI90 = bmpDataI90.Scan0;
            IntPtr ptrIp1 = bmpDataIp1.Scan0;
            Marshal.Copy(ptrI0, I0ff, 0, size);
            Marshal.Copy(ptrI90, I90ff, 0, size);
            double dI0 = 0.0, dI90 = 0.0;
            for (int i = 0; i < size; i++)
            {
                dI0 += (double)I0ff[i];
                dI90 += (double)I90ff[i];
            }
            dI0 = dI0 / size * 1.0;
            dI90 = dI90 / size * 1.0;

            for (int i = 0; i < size; i++)
            {
                if ((double)I0ff[i] / dI0 + (double)I90ff[i] / dI90 == 0) Ip1ff[i] = 0;
                else
                {
                    Ip1ff[i] = (byte)(255.0 * ((double)I0ff[i] / dI0 - (double)I90ff[i] / dI90) / ((double)I0ff[i] / dI0 + (double)I90ff[i] / dI90));
                }
            }
            Marshal.Copy(Ip1ff, 0, ptrIp1, size);
            I0.UnlockBits(bmpDataI0);
            I90.UnlockBits(bmpDataI90);
            Ip1.UnlockBits(bmpDataIp1);

            //图像拼接I45 + I135 = Ip2
            Bitmap I45 = (Bitmap)(this.pictureBoxShow1.Image.Clone());
            Bitmap I135 = (Bitmap)(this.pictureBoxShow3.Image.Clone());
            Bitmap Ip2 = (Bitmap)(this.pictureBoxShow3.Image.Clone());
            BitmapData bmpDataI135 = I135.LockBits(new Rectangle(0, 0, I135.Width, I135.Height), ImageLockMode.ReadWrite, I135.PixelFormat);
            BitmapData bmpDataI45 = I45.LockBits(new Rectangle(0, 0, I45.Width, I45.Height), ImageLockMode.ReadWrite, I45.PixelFormat);
            BitmapData bmpDataIp2 = Ip2.LockBits(new Rectangle(0, 0, Ip2.Width, Ip2.Height), ImageLockMode.ReadWrite, Ip2.PixelFormat);
            byte[] I45ff = new byte[size];
            byte[] I135ff = new byte[size];
            byte[] Ip2ff = new byte[size];
            IntPtr ptrI45 = bmpDataI45.Scan0;
            IntPtr ptrI135 = bmpDataI135.Scan0;
            IntPtr ptrIp2 = bmpDataIp2.Scan0;
            Marshal.Copy(ptrI45, I45ff, 0, size);
            Marshal.Copy(ptrI135, I135ff, 0, size);
            double dI45 = 0.0, dI135 = 0.0;
            for (int i = 0; i < size; i++)
            {
                dI45 += (double)I45ff[i];
                dI135 += (double)I135ff[i];
            }
            dI45 = dI45 / size * 1.0;
            dI135 = dI135 / size * 1.0;

            for (int i = 0; i < size; i++)
            {
                if ((double)I45ff[i] / dI45 + (double)I135ff[i] / dI135 == 0) Ip2ff[i] = 0;
                else
                {
                    Ip2ff[i] = (byte)(255.0 * ((double)I45ff[i] / dI45 - (double)I135ff[i] / dI135) / ((double)I45ff[i] / dI45 + (double)I135ff[i] / dI135));
                }
            }

            Marshal.Copy(Ip2ff, 0, ptrIp2, size);
            I45.UnlockBits(bmpDataI45);
            I135.UnlockBits(bmpDataI135);
            Ip2.UnlockBits(bmpDataIp2);
         

        // DOLP计算
Bitmap DOLP = (Bitmap)(this.pictureBoxShow1.Image.Clone());
Bitmap DOLPcolor = new Bitmap(DOLP.Width, DOLP.Height, PixelFormat.Format24bppRgb);
BitmapData bmpDataDOLP = DOLP.LockBits(new Rectangle(0, 0, DOLP.Width, DOLP.Height), ImageLockMode.ReadWrite, DOLP.PixelFormat);
BitmapData bmpDataDOLPcolor = DOLPcolor.LockBits(new Rectangle(0, 0, DOLPcolor.Width, DOLPcolor.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

int sizecolor = DOLPcolor.Width * DOLPcolor.Height * 3;
byte[] DOLPffcolor = new byte[sizecolor];
byte[] DOLPff = new byte[size];

IntPtr ptrDOLP = bmpDataDOLP.Scan0;

// 计算灰度值
for (int i = 0; i < size; i++)
{
    DOLPff[i] = (byte)(Math.Sqrt(((double)Ip1ff[i] * (double)Ip1ff[i] + (double)Ip2ff[i] * (double)Ip2ff[i]) / 2.0));
}

//
for (int i = 0; i < Width; i++)
{
    byte dolpvalue = DOLPff[i];
    bg[i]= dolpvalue;
}
for (int i = 0; i < DOLPff.Length; i++)
{
    int j = i % Width;
    byte dolpvalue = DOLPff[i];
                // dolpvalue = (byte)(dolpvalue - bg[j]);
    Color mappedColor = GetColorFromGrayValue(dolpvalue);
    int colorindex = i * 3;
    DOLPffcolor[colorindex] = mappedColor.R;
    DOLPffcolor[colorindex + 1] = mappedColor.G;
    DOLPffcolor[colorindex + 2] = mappedColor.B;
}

Marshal.Copy(DOLPffcolor, 0, bmpDataDOLPcolor.Scan0, DOLPffcolor.Length);
DOLPcolor.UnlockBits(bmpDataDOLPcolor);

this.pictureBoxShow5.Image = DOLPcolor;
bShow[4] = true;
UpdateUI();
 }
        public static Color GetColorFromGrayValue(byte grayValue)
        {
            // 将灰度值映射到HSV色彩空间
            double hue = (grayValue / 255.0) * 360.0;
            double saturation = 1.0; //
            double value = 1.0;

            // 将HSV转换为RGB
            double chroma = value * saturation;
            double hPrime = hue / 60.0;
            double x = chroma * (1 - Math.Abs(hPrime % 2 - 1));

            double r1, g1, b1;
            if (0 <= hPrime && hPrime < 1)
            {
                r1 = chroma;
                g1 = x;
                b1 = 0;
            }
            else if (1 <= hPrime && hPrime < 2)
            {
                r1 = x;
                g1 = chroma;
                b1 = 0;
            }
            else if (2 <= hPrime && hPrime < 3)
            {
                r1 = 0;
                g1 = chroma;
                b1 = x;
            }
            else if (3 <= hPrime && hPrime < 4)
            {
                r1 = 0;
                g1 = x;
                b1 = chroma;
            }
            else if (4 <= hPrime && hPrime < 5)
            {
                r1 = x;
                g1 = 0;
                b1 = chroma;
            }
            else
            {
                r1 = chroma;
                g1 = 0;
                b1 = x;
            }

            double m = value - chroma;
            int r = (int)((r1 + m) * 255);
            int g = (int)((g1 + m) * 255);
            int b = (int)((b1 + m) * 255);

            return Color.FromArgb(r, g, b);
        }
        private Bitmap AdjustContrast(Bitmap originalImage, float contrast) // contrast: -100 <= contrast <= 100
        {
            contrast = (100.0f + contrast) / 100.0f;
            contrast *= contrast;
            Bitmap adjustedImage = new Bitmap(originalImage.Width, originalImage.Height);

            // Lock the bitmap's bits.  
            Rectangle rect = new Rectangle(0, 0, adjustedImage.Width, adjustedImage.Height);
            BitmapData bmpData = adjustedImage.LockBits(rect, ImageLockMode.ReadWrite, adjustedImage.PixelFormat);

            // Get the address of the first line.
            IntPtr ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bmpData.Stride) * adjustedImage.Height;
            byte[] rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, bytes);

            for (int i = 0; i < rgbValues.Length; i += 4)
            {
                // Apply contrast formula for each channel
                float red = rgbValues[i] / 255.0f;
                red -= 0.5f;
                red *= contrast;
                red += 0.5f;
                red *= 255;
                if (red > 255) red = 255;
                if (red < 0) red = 0;
                rgbValues[i] = (byte)red;

                float green = rgbValues[i + 1] / 255.0f;
                green -= 0.5f;
                green *= contrast;
                green += 0.5f;
                green *= 255;
                if (green > 255) green = 255;
                if (green < 0) green = 0;
                rgbValues[i + 1] = (byte)green;

                float blue = rgbValues[i + 2] / 255.0f;
                blue -= 0.5f;
                blue *= contrast;
                blue += 0.5f;
                blue *= 255;
                if (blue > 255) blue = 255;
                if (blue < 0) blue = 0;
                rgbValues[i + 2] = (byte)blue;
            }

            // Copy the RGB values back to the bitmap
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

            // Unlock the bits.
            adjustedImage.UnlockBits(bmpData);

            return adjustedImage;
        }


        private void shibie(object sender, EventArgs e)
        {
            //图像识别
            var image1 = this.pictureBoxShow5.Image;
            Bitmap image = new Bitmap(image1.Width, image1.Height, PixelFormat.Format24bppRgb);
            Graphics g = Graphics.FromImage(image);
            g.DrawImage(image1, 0, 0, image1.Width, image1.Height);
            var scorer = new YoloScorer<YoloCocoP6Model>("weights/best.onnx");

            List<YoloPrediction> predictions = scorer.Predict(image);

            var graphics = Graphics.FromImage(image);

            foreach (var prediction in predictions) // iterate predictions to draw results
            {
                double score = Math.Round(prediction.Score, 2);

                graphics.DrawRectangles(new Pen(prediction.Label.Color, 1), new[] { prediction.Rectangle });


                string result = String.Format("{0} {1}{2}{3}", prediction.Label.Name, "{", score, "}");

                graphics.DrawString(result, new Font("Consolas", 16, GraphicsUnit.Pixel), new SolidBrush(prediction.Label.Color), new PointF(prediction.Rectangle.X - 3, prediction.Rectangle.Y - 23));
            }
            this.pictureBoxShow6.Image = (Image)(image.Clone());
            bShow[5] = true;
            UpdateUI();
        }
        private void SaveImage(Image save_Image)
        {
            SaveFileDialog saveImg = new SaveFileDialog();
            saveImg.Title = "图片保存";
            saveImg.Filter = @"PNG(*.png)|*.png";
            if (saveImg.ShowDialog() == DialogResult.OK)
            {
                string fileName = saveImg.FileName.ToString();
                if (fileName != "" && fileName != null)
                {
                    System.Drawing.Imaging.ImageFormat imgFormat = System.Drawing.Imaging.ImageFormat.Png;
                    try
                    {
                        save_Image.Save(fileName, imgFormat);
                    }
                    catch
                    {
                        MessageBox.Show("Save image failure", "Warning", MessageBoxButtons.OK);
                    }
                }
            }
        }
        
       

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if(!bShow[0])
            {
                MessageBox.Show("No images", "Warning", MessageBoxButtons.OK);
                return;
            }
            //SaveImage(this.pictureBoxShow1.Image);

            if(!bShow[1])
            {
                return;
            }
            //SaveImage(this.pictureBoxShow2.Image);
            if (!bShow[2])
            {
                return;
            }
            //SaveImage(this.pictureBoxShow3.Image);
            if (!bShow[3])
            {
                return;
            }
            //SaveImage(this.pictureBoxShow4.Image);
            if (!bShow[4])
            {
                return;
            }
            SaveImage(this.pictureBoxShow5.Image);
            if (!bShow[5])
            {
                return;
            }
            //SaveImage(this.pictureBoxShow6.Image);
        }

        private void MulitpleGrabDisplay_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (bGrab)
            {
                MessageBox.Show("Please stop grab before closing the form", "Warning", MessageBoxButtons.OK);
                e.Cancel = true;
                return;
            }
            if(bOpen)
            {
                for (int i = 0; i < m_nUseCount; i++)
                {
                    m_Camera[i].CloseCamera();
                }
                bOpen = false;
            }
        }


        //双击图片时进入全屏或者还原
        bool a1 = true;
        bool a2 = true;
        bool a3 = true;
        bool a4 = true;
        bool a5 = true;
        bool a6 = true;
        private void pictureBoxShow1_DoubleClick(object sender, EventArgs e)
        {
            
            if (a1 == true)
            {
                pictureBoxShow1.Width = Convert.ToInt32(pictureBoxShow1.Width * 2);
                pictureBoxShow1.Height = Convert.ToInt32(pictureBoxShow1.Height * 2);
                a1 = false;
            }
            else
            {
                pictureBoxShow1.Width = Convert.ToInt32(pictureBoxShow1.Width * 0.5);
                pictureBoxShow1.Height = Convert.ToInt32(pictureBoxShow1.Height * 0.5);
                a1 = true;
            }
        }
        private void pictureBoxShow2_DoubleClick(object sender, EventArgs e)
        {
            if (a2 == true)
            {
                pictureBoxShow2.Width = Convert.ToInt32(pictureBoxShow2.Width * 2);
                pictureBoxShow2.Height = Convert.ToInt32(pictureBoxShow2.Height * 2);
                a2 = false;
            }
            else
            {
                pictureBoxShow2.Width = Convert.ToInt32(pictureBoxShow2.Width * 0.5);
                pictureBoxShow2.Height = Convert.ToInt32(pictureBoxShow2.Height * 0.5);
                a2 = true;
            }
        }
        private void pictureBoxShow3_DoubleClick(object sender, EventArgs e)
        {
            if (a3 == true)
            {
                pictureBoxShow3.Width = Convert.ToInt32(pictureBoxShow3.Width * 2);
                pictureBoxShow3.Height = Convert.ToInt32(pictureBoxShow3.Height * 2);
                a3 = false;
            }
            else
            {
                pictureBoxShow3.Width = Convert.ToInt32(pictureBoxShow3.Width * 0.5);
                pictureBoxShow3.Height = Convert.ToInt32(pictureBoxShow3.Height * 0.5);
                a3 = true;
            }
        }

        private void pictureBoxShow4_DoubleClick(object sender, EventArgs e)
        {
            if (a4 == true)
            {
                pictureBoxShow4.Width = Convert.ToInt32(pictureBoxShow4.Width * 2);
                pictureBoxShow4.Height = Convert.ToInt32(pictureBoxShow4.Height * 2);
                a4 = false;
            }
            else
            {
                pictureBoxShow4.Width = Convert.ToInt32(pictureBoxShow4.Width * 0.5);
                pictureBoxShow4.Height = Convert.ToInt32(pictureBoxShow4.Height * 0.5);
                a4 = true;
            }
        }

        private void pictureBoxShow5_DoubleClick(object sender, EventArgs e)
        {
            if (a5 == true)
            {
                pictureBoxShow5.Width = Convert.ToInt32(pictureBoxShow5.Width * 2);
                pictureBoxShow5.Height = Convert.ToInt32(pictureBoxShow5.Height * 2);
                a5 = false;
            }
            else
            {
                pictureBoxShow5.Width = Convert.ToInt32(pictureBoxShow5.Width * 0.5);
                pictureBoxShow5.Height = Convert.ToInt32(pictureBoxShow5.Height * 0.5);
                a5 = true;
            }
        }

        private void pictureBoxShow6_DoubleClick(object sender, EventArgs e)
        {
            if (a6 == true)
            {
                pictureBoxShow6.Width = Convert.ToInt32(pictureBoxShow6.Width * 2);
                pictureBoxShow6.Height = Convert.ToInt32(pictureBoxShow6.Height * 2);
                a6 = false;
            }
            else
            {
                pictureBoxShow6.Width = Convert.ToInt32(pictureBoxShow6.Width * 0.5);
                pictureBoxShow6.Height = Convert.ToInt32(pictureBoxShow6.Height * 0.5);
                a6 = true;
            }
        }

        int xPos;
        int yPos;
        bool MoveFlag;
        private void pictureBoxShow1_MouseWheel(object sender, MouseEventArgs e)
        {
            var t = pictureBoxShow1.Size;
            t.Width += e.Delta;
            t.Height += e.Delta;
            pictureBoxShow1.Size = t;
            //图片按中心比例放大缩小
            //pictureBoxShow1.Location = new Point((this.Width - pictureBoxShow1.Width) / 2, (this.Height - pictureBoxShow1.Height) / 2);

        }

        private void pictureBoxShow1_MouseMove(object sender, MouseEventArgs e)
        {
            if (MoveFlag)
            {
                pictureBoxShow1.Left += Convert.ToInt16(e.X - xPos);//设置x坐标.
                pictureBoxShow1.Top += Convert.ToInt16(e.Y - yPos);//设置y坐标.
                pictureBoxShow1.Location = new Point(pictureBoxShow1.Left, pictureBoxShow1.Top);
            }

        }

        private void pictureBoxShow1_MouseDown(object sender, MouseEventArgs e)
        {

            MoveFlag = true;//已经按下.
            xPos = e.X;//当前x坐标.
            yPos = e.Y;//当前y坐标.
        }
        private void pictureBoxShow1_MouseUp(object sender, MouseEventArgs e)
        {

            MoveFlag = false;
        }


        int xPos2;
        int yPos2;
        bool MoveFlag2;
        private void pictureBoxShow2_MouseWheel(object sender, MouseEventArgs e)
        {
            var t = pictureBoxShow2.Size;
            t.Width += e.Delta;
            t.Height += e.Delta;
            pictureBoxShow2.Size = t;
            //图片按中心比例放大缩小
            //pictureBoxShow2.Location = new Point((this.Width - pictureBoxShow2.Width) / 2, (this.Height - pictureBoxShow2.Height) / 2);

        }

        private void pictureBoxShow2_MouseMove(object sender, MouseEventArgs e)
        {
            if (MoveFlag2)
            {
                pictureBoxShow2.Left += Convert.ToInt16(e.X - xPos2);//设置x坐标.
                pictureBoxShow2.Top += Convert.ToInt16(e.Y - yPos2);//设置y坐标.
                pictureBoxShow2.Location = new Point(pictureBoxShow2.Left, pictureBoxShow2.Top);
            }

        }

        private void pictureBoxShow2_MouseDown(object sender, MouseEventArgs e)
        {

            MoveFlag2 = true;//已经按下.
            xPos2 = e.X;//当前x坐标.
            yPos2 = e.Y;//当前y坐标.
        }
        private void pictureBoxShow2_MouseUp(object sender, MouseEventArgs e)
        {

            MoveFlag2 = false;
        }

        int xPos3;
        int yPos3;
        bool MoveFlag3;
        private void pictureBoxShow3_MouseWheel(object sender, MouseEventArgs e)
        {
            var t = pictureBoxShow3.Size;
            t.Width += e.Delta;
            t.Height += e.Delta;
            pictureBoxShow3.Size = t;
            //图片按中心比例放大缩小
            //pictureBoxShow3.Location = new Point((this.Width - pictureBoxShow3.Width) / 2, (this.Height - pictureBoxShow3.Height) / 2);

        }

        private void pictureBoxShow3_MouseMove(object sender, MouseEventArgs e)
        {
            if (MoveFlag3)
            {
                pictureBoxShow3.Left += Convert.ToInt16(e.X - xPos3);//设置x坐标.
                pictureBoxShow3.Top += Convert.ToInt16(e.Y - yPos3);//设置y坐标.
                pictureBoxShow3.Location = new Point(pictureBoxShow3.Left, pictureBoxShow3.Top);
            }

        }

        private void pictureBoxShow3_MouseDown(object sender, MouseEventArgs e)
        {

            MoveFlag3 = true;//已经按下.
            xPos3 = e.X;//当前x坐标.
            yPos3 = e.Y;//当前y坐标.
        }
        private void pictureBoxShow3_MouseUp(object sender, MouseEventArgs e)
        {

            MoveFlag3 = false;
        }

        int xPos4;
        int yPos4;
        bool MoveFlag4;
        private void pictureBoxShow4_MouseWheel(object sender, MouseEventArgs e)
        {
            var t = pictureBoxShow4.Size;
            t.Width += e.Delta;
            t.Height += e.Delta;
            pictureBoxShow4.Size = t;
            //图片按中心比例放大缩小
            //pictureBoxShow4.Location = new Point((this.Width - pictureBoxShow4.Width) / 2, (this.Height - pictureBoxShow4.Height) / 2);

        }

        private void pictureBoxShow4_MouseMove(object sender, MouseEventArgs e)
        {
            if (MoveFlag4)
            {
                pictureBoxShow4.Left += Convert.ToInt16(e.X - xPos4);//设置x坐标.
                pictureBoxShow4.Top += Convert.ToInt16(e.Y - yPos4);//设置y坐标.
                pictureBoxShow4.Location = new Point(pictureBoxShow4.Left, pictureBoxShow4.Top);
            }

        }

        private void pictureBoxShow4_MouseDown(object sender, MouseEventArgs e)
        {

            MoveFlag4 = true;//已经按下.
            xPos4 = e.X;//当前x坐标.
            yPos4 = e.Y;//当前y坐标.
        }
        private void pictureBoxShow4_MouseUp(object sender, MouseEventArgs e)
        {

            MoveFlag4 = false;
        }

        int xPos5;
        int yPos5;
        bool MoveFlag5;
        private void pictureBoxShow5_MouseWheel(object sender, MouseEventArgs e)
        {
            var t = pictureBoxShow5.Size;
            t.Width += e.Delta;
            t.Height += e.Delta;
            pictureBoxShow5.Size = t;
            //图片按中心比例放大缩小
            //pictureBoxShow5.Location = new Point((this.Width - pictureBoxShow5.Width) / 2, (this.Height - pictureBoxShow5.Height) / 2);

        }

        private void pictureBoxShow5_MouseMove(object sender, MouseEventArgs e)
        {
            if (MoveFlag5)
            {
                pictureBoxShow5.Left += Convert.ToInt16(e.X - xPos5);//设置x坐标.
                pictureBoxShow5.Top += Convert.ToInt16(e.Y - yPos5);//设置y坐标.
                pictureBoxShow5.Location = new Point(pictureBoxShow5.Left, pictureBoxShow5.Top);
            }

        }

        private void pictureBoxShow5_MouseDown(object sender, MouseEventArgs e)
        {

            MoveFlag5 = true;//已经按下.
            xPos5 = e.X;//当前x坐标.
            yPos5 = e.Y;//当前y坐标.
        }
        private void pictureBoxShow5_MouseUp(object sender, MouseEventArgs e)
        {

            MoveFlag5 = false;
        }
        int xPos6;
        int yPos6;
        bool MoveFlag6;
        private void pictureBoxShow6_MouseWheel(object sender, MouseEventArgs e)
        {
            var t = pictureBoxShow6.Size;
            t.Width += e.Delta;
            t.Height += e.Delta;
            pictureBoxShow6.Size = t;
            //图片按中心比例放大缩小
            //pictureBoxShow6.Location = new Point((this.Width - pictureBoxShow6.Width) / 2, (this.Height - pictureBoxShow6.Height) / 2);

        }

        private void pictureBoxShow6_MouseMove(object sender, MouseEventArgs e)
        {
            if (MoveFlag6)
            {
                pictureBoxShow6.Left += Convert.ToInt16(e.X - xPos6);//设置x坐标.
                pictureBoxShow6.Top += Convert.ToInt16(e.Y - yPos6);//设置y坐标.
                pictureBoxShow6.Location = new Point(pictureBoxShow6.Left, pictureBoxShow6.Top);
            }

        }

        private void pictureBoxShow6_MouseDown(object sender, MouseEventArgs e)
        {

            MoveFlag6 = true;//已经按下.
            xPos6 = e.X;//当前x坐标.
            yPos6 = e.Y;//当前y坐标.
        }
        private void pictureBoxShow6_MouseUp(object sender, MouseEventArgs e)
        {

            MoveFlag6 = false;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            SaveImage(this.pictureBoxShow6.Image);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            SaveImage(this.pictureBoxShow5.Image);
        }
    }
}
