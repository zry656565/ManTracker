using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using Microsoft.Kinect;
using SJTU.IOTLab.ManTracking.ImageProcess;
using SJTU.IOTLab.ManTracking.Struct;
using SJTU.IOTLab.ManTracking.Helper;

namespace SJTU.IOTLab.ManTracking
{
    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap bitmap = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null,
                       infoText = null,
                       locationText = null;

        private KinectProcess kinectProcess;
        private Transporter transporter;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            transporter = new Transporter();
            // transporter.Connect();

            kinectProcess = new KinectProcess();
            kinectProcess.Initialize(kinectStatusUpdated, processStatusUpdated, locationUpdated, bitmapInit);
            
            this.DataContext = this;
            this.InitializeComponent();
        }

        private void kinectStatusUpdated(string status)
        {
            this.StatusText = status;
        }

        private void processStatusUpdated(double fps, string otherStatus)
        {
            this.InfoText = "fps: " + fps.ToString("0.00") + ", " + otherStatus;
        }

        private void locationUpdated(Location[] locations)
        {
            string requestStr = "";
            for (int i = 0; i < locations.Length; ++i)
            {
                requestStr += locations[i].depth.ToString("0.00") + ":" + locations[i].offset.ToString("0.00") + ";";
            }

            this.LocationText = requestStr;
            if (transporter.status == Transporter.STATUS.CONNECTED)
                transporter.Send(requestStr);                
        }

        private void bitmapInit(WriteableBitmap bitmap)
        {
            this.bitmap = bitmap;
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.bitmap;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        public string InfoText
        {
            get
            {
                return this.infoText;
            }

            set
            {
                if (this.infoText != value)
                {
                    this.infoText = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("InfoText"));
                    }
                }
            }
        }

        public string LocationText
        {
            get
            {
                return this.locationText;
            }

            set
            {
                if (this.locationText != value)
                {
                    this.locationText = value;

                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("LocationText"));
                    }
                }
            }
        }
        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            this.kinectProcess.Close();
        }
    }
}
