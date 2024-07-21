using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using IKapC.NET;
namespace MultiCamerasGrab
{
    public class BufferManager
    {
        [DllImport("kernel32.dll")]
        public static extern void CopyMemory(IntPtr Destination, IntPtr Source, int Length);

        //创建的缓冲区大小
        public uint m_nBuffersz = 0;
        //创建的缓冲区指针
        public IntPtr m_pBuffer = new IntPtr(-1);

        private byte[] m_pByteReader = null;
        private byte[] m_pDes = null;
        private short[] m_pShortReader = null;
        //缓冲区锁
        public object m_bufferLock = new object();
        //创建的Bitmap句柄
        public Bitmap m_Bmp = null;
        //图片锁
        public object m_imageLock = new object();
        //图片数据类型，8bit为8,10bit为10,12bit为12
        public uint m_nDataFormat = 8;
        //图片类型
        public uint m_nImageType = 8;
        public bool bUpdateImg = false;

        public bool CreateDataBufferAndBitmap(CameraManager camera)
        {
            Int64 nWidth = 0;
            Int64 nHeight = 0;
            uint nBuffersz = 0;
            nBuffersz = camera.GetBufferSize();

            //根据相机采集图片格式创建Bitmap
            camera.GetFeatureInt64("Width", ref nWidth);
            camera.GetFeatureInt64("Height", ref nHeight);
            m_nImageType = camera.GetPixelFormat();
            PixelFormat pixelFormat = PixelFormat.Undefined;
            if (m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO8 ||
                m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GR8 ||
                m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_RG8 ||
                m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GB8 ||
                m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_BG8)
            {
                m_nDataFormat = 8;
                pixelFormat = PixelFormat.Format8bppIndexed;
            }
            else if (m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO10 ||
                     m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GR10 ||
                     m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_RG10 ||
                     m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GB10 ||
                     m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_BG10)
            {
                m_nDataFormat = 10;
                pixelFormat = PixelFormat.Format8bppIndexed;
            }
            else if (m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_MONO12 ||
                     m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GR12 ||
                     m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_RG12 ||
                     m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_GB12 ||
                     m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BAYER_BG12)
            {
                m_nDataFormat = 12;
                pixelFormat = PixelFormat.Format8bppIndexed;
            }
            else if (m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB888 ||
                     m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR888)
            {
                m_nDataFormat = 8;
                pixelFormat = PixelFormat.Format24bppRgb;
            }
            else if (m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB101010 ||
                     m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR101010)
            {
                m_nDataFormat = 10;
                pixelFormat = PixelFormat.Format24bppRgb;
            }
            else if (m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB121212 ||
                     m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_BGR121212)
            {
                m_nDataFormat = 12;
                pixelFormat = PixelFormat.Format24bppRgb;
            }
            lock (m_imageLock)
            {
                m_Bmp = new Bitmap((int)nWidth, (int)nHeight, pixelFormat);
                //灰度图需要自行初始化调色板
                if (pixelFormat == System.Drawing.Imaging.PixelFormat.Format8bppIndexed)
                {
                    ColorPalette cp = m_Bmp.Palette;
                    for (int i = 0; i < 256; i++)
                        cp.Entries[i] = System.Drawing.Color.FromArgb(i, i, i);
                    m_Bmp.Palette = cp;
                }
            }
            lock (m_bufferLock)
            {
                m_pBuffer = Marshal.AllocHGlobal((int)nBuffersz);
                if (m_pBuffer == null)
                {
                    return false;
                }
                m_nBuffersz = nBuffersz;

                if (m_nDataFormat == 8)
                {
                    m_pDes = new byte[m_nBuffersz];
                    m_pByteReader = new byte[m_nBuffersz];
                }
                else
                {
                    m_pDes = new byte[m_nBuffersz / 2];
                    m_pShortReader = new short[m_nBuffersz / 2];
                }
            }
            return true;
        }
       // int flag = 0;
        public bool ReadImage()
        {
            lock (m_imageLock)
            {
                if (m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB888 ||
                    m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB101010 ||
                    m_nImageType == (uint)ItkBufferFormat.ITKBUFFER_VAL_FORMAT_RGB121212)
                {
                    ReadRGB();
                    return true;
                }
                lock (m_bufferLock)
                {
                    if (m_Bmp == null || m_pBuffer == null)
                    {
                        return false;
                    }
                    bUpdateImg = false;
                    Rectangle rect = new Rectangle(0, 0, m_Bmp.Width, m_Bmp.Height);
                    BitmapData bmpData = m_Bmp.LockBits(rect, ImageLockMode.ReadWrite, m_Bmp.PixelFormat);
                    int nShift = (int)m_nDataFormat - 8;
                    int nStride = (int)m_nBuffersz / m_Bmp.Height;
                    if (m_nDataFormat == 8)
                    {
                        for (int i = 0; i < m_Bmp.Height; i++)
                        {
                            IntPtr pSrc = m_pBuffer + nStride * i;
                            IntPtr pDes = bmpData.Scan0 + bmpData.Stride * i;
                            CopyMemory(pDes, pSrc, nStride);
                        }
                        m_Bmp.UnlockBits(bmpData);
                        return true;
                    }
                    short[] pData = new short[m_nBuffersz / 2];
                    byte[] pDestData = new byte[m_nBuffersz];
                    nStride = (int)m_nBuffersz / (2 * m_Bmp.Height);
                    Marshal.Copy(m_pBuffer, pData, 0, (int)m_nBuffersz / 2);
                    for (int i = 0; i < m_Bmp.Height; i++)
                    {
                        for (int j = 0; j < nStride; j++)
                        {
                            pDestData[i * nStride + j] = (byte)(pData[i * nStride + j] >> nShift);
                        }
                    }
                    Marshal.Copy(pDestData, 0, bmpData.Scan0, (int)m_nBuffersz / 2);
                    m_Bmp.UnlockBits(bmpData);
                }
            }
            return true;
        }

        private void ReadRGB()
        {
            lock (m_bufferLock)
            {
                if (m_Bmp == null || m_pBuffer == null)
                {
                    return;
                }
                bUpdateImg = false;
                Rectangle rect = new Rectangle(0, 0, m_Bmp.Width, m_Bmp.Height);
                BitmapData bmpData = m_Bmp.LockBits(rect, ImageLockMode.ReadWrite, m_Bmp.PixelFormat);
                int nShift = (int)m_nDataFormat - 8;
                int nStride = (int)m_nBuffersz / m_Bmp.Height;
                if (m_nDataFormat == 8)
                {
                    Marshal.Copy(m_pBuffer, m_pByteReader, 0, (int)m_nBuffersz);
                    for (int i = 0; i < m_nBuffersz; i += 3)
                    {
                        m_pDes[i] = m_pByteReader[i + 2];
                        m_pDes[i + 1] = m_pByteReader[i + 1];
                        m_pDes[i + 2] = m_pByteReader[i];
                    }
                    Marshal.Copy(m_pDes, 0, bmpData.Scan0, (int)m_nBuffersz);
                    m_Bmp.UnlockBits(bmpData);
                    return;
                }
                nStride = (int)m_nBuffersz / (2 * m_Bmp.Height);
                Marshal.Copy(m_pBuffer, m_pShortReader, 0, (int)m_nBuffersz / 2);
                for (int i = 0; i < m_Bmp.Height; i++)
                {
                    for (int j = 0; j < nStride; j += 3)
                    {
                        m_pDes[i * nStride + j] = (byte)(m_pShortReader[i * nStride + j + 2] >> nShift);
                        m_pDes[i * nStride + j + 1] = (byte)(m_pShortReader[i * nStride + j + 1] >> nShift);
                        m_pDes[i * nStride + j + 2] = (byte)(m_pShortReader[i * nStride + j] >> nShift);
                    }
                }
                Marshal.Copy(m_pDes, 0, bmpData.Scan0, (int)m_nBuffersz / 2);
                m_Bmp.UnlockBits(bmpData);
            }
        }

        public void ReleaseBuffer()
        {
            lock (m_bufferLock)
            {
                if (m_pBuffer != null)
                {
                    Marshal.FreeHGlobal(m_pBuffer);
                }
                m_nBuffersz = 0;
            }
            lock (m_imageLock)
            {
                if (m_Bmp != null)
                {
                    m_Bmp.Dispose();
                    m_Bmp = null;
                }
            }
            m_pShortReader = null;
            m_pDes = null;
            m_pByteReader = null;
        }
    }
}
