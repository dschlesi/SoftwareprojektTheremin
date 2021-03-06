﻿using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using NAudio.Wave;

namespace SoftwareprojektTheremin
{

    public class SineWaveProvider32 : WaveProvider32
    {
        int sample;

        public SineWaveProvider32()
        {
            Frequency = 1000;
            Amplitude = 0.25f;
        }

        public float Frequency { get; set; }
        public float Amplitude { get; set; }

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            int sampleRate = WaveFormat.SampleRate;
            for (int n = 0; n < sampleCount; n++)
            {
                buffer[n + offset] = (float)(Amplitude * Math.Sin((2 * Math.PI * sample * Frequency) / sampleRate));
                sample++;
                if (sample >= sampleRate) sample = 0;
            }
            return sampleCount;
        }
    }


    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        enum hand : Int32
        {
            LEFT = 0, RIGHT = 1
        }
        enum cord : Int32
        {
            X = 0, Y = 1
        }
        private PXCMSession session;
        private float[,] blobCoordinates = new float[4, 2] { { -1, -1 }, { -1, -1 }, { -1, -1 }, { -1, -1 } };
        private PXCMSenseManager senseManager;
        private PXCMPersonTrackingModule tracker;
        private Thread update;
        private PXCMBlobModule blobModule;
        private PXCMBlobConfiguration blobConfig;
        private PXCMBlobData blobData;
        private PXCMBlobData.IBlob[] blobList = new PXCMBlobData.IBlob[2];
        private int trackingDistance = 1000;
        private int frequency = 35;
        private float volume = 0.5F;
        //  private Thread ton;
        private SineWaveProvider32 sineWaveProvider = new SineWaveProvider32();

        public MainWindow()
        {
            InitializeComponent();

            // Configure RealSense session and SenseManager interface
            session = PXCMSession.CreateInstance();
            senseManager = session.CreateSenseManager();
            senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 640, 480, 30);
            senseManager.EnableBlob();
            senseManager.Init();

            //Create blobModule from SenseManager
            blobModule = senseManager.QueryBlob();
            blobConfig = blobModule.CreateActiveConfiguration();
            blobConfig.SetMaxBlobs(2);
            blobConfig.SetMaxDistance(trackingDistance);
            blobConfig.SetMaxObjectDepth(100);
            blobConfig.SetMinPixelCount(400);
            blobConfig.EnableColorMapping(true);
            blobConfig.ApplyChanges();
            blobData = blobModule.CreateOutput();

            //Audio
            WaveOut waveOut;
            sineWaveProvider.SetWaveFormat(16000, 1);
            sineWaveProvider.Frequency = 1000;
            sineWaveProvider.Amplitude = 0;
            waveOut = new WaveOut();
            waveOut.Init(sineWaveProvider);
            waveOut.Play();

            // Start Update thread
            update = new Thread(new ThreadStart(Update));
            update.Start();
        }



        private void Update()
        {
            // Start AcquireFrame-ReleaseFrame loop
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                PXCMCapture.Sample sample = senseManager.QuerySample();

                Bitmap colorBitmap;
                PXCMImage.ImageData colorData;
                blobData.Update();

                /*while (blobData.QueryNumberOfBlobs() < 2)
                {
                    trackingDistance += 100;
                    blobConfig.SetMaxDistance(trackingDistance);
                    blobConfig.ApplyChanges();
                    blobData.Update();
                    
                    senseManager.ReleaseFrame();

                   if(trackingDistance > 3000)
                       trackingDistance = 600;
                   }
                   {
                }
                */

                for (int i = 0; i < 2; i++)
                {
                    blobData.QueryBlobByAccessOrder(i, PXCMBlobData.AccessOrderType.ACCESS_ORDER_NEAR_TO_FAR, out blobList[i]);
                }
                if (blobData.QueryNumberOfBlobs() == 2)
                {
                    if (blobCoordinates[3, (int)cord.Y] == -1)
                    {
                        //smoothing
                    }
                    if (blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x > blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x)
                    {
                        blobCoordinates[(int)hand.LEFT, (int)cord.Y] = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        blobCoordinates[(int)hand.RIGHT, (int)cord.Y] = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        blobCoordinates[(int)hand.LEFT, (int)cord.X] = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x;
                        blobCoordinates[(int)hand.RIGHT, (int)cord.X] = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x;
                    }
                    else
                    {
                        blobCoordinates[(int)hand.RIGHT, (int)cord.Y] = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        blobCoordinates[(int)hand.LEFT, (int)cord.Y] = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y;
                        blobCoordinates[(int)hand.RIGHT, (int)cord.X] = blobList[0].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x;
                        blobCoordinates[(int)hand.LEFT, (int)cord.X] = blobList[1].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x;
                    }
                }

                // Tonausgabe: aktuelle Tonausgabe beenden und neue beginnen
                frequency = (int)(blobCoordinates[(int)hand.LEFT, (int)cord.Y] * 1.8);
                volume = (500 - blobCoordinates[(int)hand.RIGHT, (int)cord.Y]) / 500;

                sineWaveProvider.Frequency = frequency;
                sineWaveProvider.Amplitude = volume;

                // Get color image data
                sample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out colorData);
                colorBitmap = colorData.ToBitmap(0, sample.color.info.width, sample.color.info.height);

                // Update UI
                Render(colorBitmap);

                // Release frame
                colorBitmap.Dispose();
                sample.color.ReleaseAccess(colorData);
                senseManager.ReleaseFrame();
            }
        }

        private void Render(Bitmap bitmap)
        {
            BitmapImage bitmapImage = ConvertBitmap(bitmap);

            if (bitmapImage != null)
            {
                // Update the WPF Image control
                this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
                {
                    imgStream.Source = bitmapImage;
                    labelR.Content = blobCoordinates[(int)hand.RIGHT, (int)cord.Y];
                    labelL.Content = blobCoordinates[(int)hand.LEFT, (int)cord.Y];
                }));
            }
        }

        private BitmapImage ConvertBitmap(Bitmap bitmap)
        {
            BitmapImage bitmapImage = null;
            System.Drawing.Pen fancyPen = new System.Drawing.Pen(System.Drawing.Color.Cyan, 5);

            if (bitmap != null)
            {
                if (blobData.QueryNumberOfBlobs() == 2)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            //graphics.DrawRectangle(fancyPen, blobList[i].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).x, blobList[i].QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CENTER).y, 5, 5);
                            graphics.DrawRectangle(fancyPen, blobCoordinates[(int)hand.LEFT, (int)cord.X] - 50, blobCoordinates[(int)hand.LEFT, (int)cord.Y], 5, 5);
                            graphics.DrawRectangle(fancyPen, blobCoordinates[(int)hand.RIGHT, (int)cord.X] - 50, blobCoordinates[(int)hand.RIGHT, (int)cord.Y], 5, 5);

                        }
                    }
                }
                bitmap.RotateFlip(RotateFlipType.Rotate180FlipY);
                MemoryStream memoryStream = new MemoryStream();
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memoryStream.Position = 0;
                bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }

            return bitmapImage;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ShutDown();
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            ShutDown();
            this.Close();
        }

        private void ShutDown()
        {
            // Stop the Update thread
            update.Abort();

            // Dispose RealSense objects
            blobModule.Dispose();
            blobData.Dispose();
            senseManager.Dispose();
            session.Dispose();
        }
    }
}
