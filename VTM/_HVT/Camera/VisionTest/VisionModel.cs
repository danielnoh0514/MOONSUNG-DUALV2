﻿using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;



namespace Camera
{
    public class VisionModel
    {

        public const int CHARNUMBER = 7;
        public const double FND_WIDTH = 20;
        public const double FND_HEIGHT = 30;
        public const int FND_DIAMETER_POINT = 5;
        public const int FND_THRESH_POINT = 80;



        private List<List<FND>> _FNDs { set; get; } = new List<List<FND>>();

        public List<List<FND>> FNDs
        {
            get { return _FNDs; }
            set
            {
                if (value != null || value != _FNDs)
                {
                    _FNDs = value;
                    for (int index_char = 0; index_char < _FNDs.Count; index_char++)
                    {
                        for (int index_board=0; index_board < 4; index_board++)
                        {
                            FNDs[index_char][index_board].Selected += VisionModel_Selected;
                        }
                    }
                }
            }
        }


        private List<LCD> _LCDs = new List<LCD>();
        public List<LCD> LCDs
        {
            get { return _LCDs; }
            set
            {
                if (value != null || value != _LCDs)
                {
                    _LCDs = value;
                    for (int i = 0; i < 4; i++)
                    {
                        LCDs[i].Selected += VisionModel_Selected;
                    }
                }
            }
        }


        private List<LED> _LED = new List<LED>();
        public List<LED> LED
        {
            get { return _LED; }
            set
            {
                if (value != null || value != _LED) _LED = value;
            }
        }

        private List<GLED> _GLED = new List<GLED>();
        public List<GLED> GLED
        {
            get { return _GLED; }
            set
            {
                if (value != null || value != _GLED) _GLED = value;
            }
        }

        public VisionModelOption Option = new VisionModelOption();



        public VisionModel()
        {
            for (int fndChar_index = 0; fndChar_index < CHARNUMBER; fndChar_index++)
            {
                List<FND> FNDchar = new List<FND>();


                for (int i = 0; i < 4; i++)
                {
                    FND fnd = new FND(fndChar_index);
                    fnd.Selected += VisionModel_Selected;
                    FNDchar.Add(fnd);
                }

                FNDs.Add(FNDchar);
            }
            for (int i = 0; i < 4; i++)
            {
                LCDs.Add(new LCD(i));
                LCDs[i].Selected += VisionModel_Selected;
                GLED.Add(new GLED(new System.Windows.Point(5, 5 + 25 * i)));
                LED.Add(new LED(new System.Windows.Point(5, 100 + 25 * i)));
            }
            Option.DataContext = FNDs.First().First();

        }

        private void VisionModel_Selected(object sender, EventArgs e)
        {
            FND tryCatchFND = (sender as FND);
            if (tryCatchFND != null)
            {
                Option.SetDataContext(tryCatchFND);

            }
            LCD tryCatchLCD = (sender as LCD);
            if (tryCatchLCD != null)
            {
                Option.SetDataContext(tryCatchLCD);
            }
        }

        public void UpdateLayout(int PCB_Count)
        {
            for (int i = 0; i < 4; i++)
            {
                foreach (var fnds_char in  FNDs)
                {
                    fnds_char[i].Visibility = PCB_Count > i ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                }
          

                LCDs[i].Visibility = PCB_Count > i ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                GLED[i].Visibility = PCB_Count > i ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                LED[i].Visibility = PCB_Count > i ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        public void GetFNDSampleImage(Mat mat)
        {

            foreach (var fnds_char in FNDs)
            {
                foreach (var item in fnds_char)
                {
                    if (item.Use)
                    {
                        item.TestImage(mat);
                    }
                    else
                    {
                        item.DetectedString = string.Empty;
                    }
                }
            }
        }

        public void GetLCDSampleImage(Mat mat)
        {
            if (LCDs.Count < 4)
            {
                return;
            }
            foreach (var item in LCDs)
            {
                if (item.Visibility == Visibility.Visible)
                {
                    if (!LCD.Processing)
                        item.TestImage(mat, item.SpectString, true);
                }
            }
        }

        public void GetGLEDSampleImage(Mat mat)
        {
            foreach (var item in GLED)
            {
                item.GetValue(mat);
            }
        }
        public void GetLEDSampleImage(Mat mat)
        {
            foreach (var item in LED)
            {
                item.GetValue(mat);
            }
        }
  
   
    }
}
