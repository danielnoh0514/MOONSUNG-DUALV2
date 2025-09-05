using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Camera
{
    /// <summary>
    /// Interaction logic for VisionTester.xaml
    /// </summary>
    public partial class VisionTester : UserControl
    {
        private VisionModel _Models = new VisionModel();
        public VisionModel Models
        {
            get { return _Models; }
            set
            {
                if (value != null || value != _Models)
                {
                    _Models = value;
                    FuntionsUpdate();
                }

            }
        }

        public VisionTester()
        {
            InitializeComponent();
            functionCanvas.Children.Clear();
        }

        public void FuntionsUpdate()
        {
            functionCanvas.Children.Clear();
            foreach (var item in Models.FNDs)
            {
                foreach (var  item2 in item)
                {
                    item2.SetParentCanvas(functionCanvas);
                    item2.IsReadOnly = true;

                }

            }
            foreach (var item in Models.LCDs)
            {
                item.SetParentCanvas(functionCanvas);
                item.IsReadOnly = true;

            }
            foreach (var item in Models.GLED)
            {
                item.SetParentCanvas(functionCanvas);
                item.IsReadOnly = true;
                foreach (var gled in item.GLEDs)
                {
                    gled.Visibility = gled.Use ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            foreach (var item in Models.LED)
            {
                item.SetParentCanvas(functionCanvas);
                item.IsReadOnly = true;
                foreach (var led in item.LEDs)
                {
                    led.Visibility = led.Use ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        public void FndFunctionUpdate()
        {
            functionCanvas.Children.Clear();

            foreach (var FNDchar in Models.FNDs)
            {
                foreach (var FNDchar_BoardN in FNDchar)
                {
                    FNDchar_BoardN.SetParentCanvas(functionCanvas);
                    FNDchar_BoardN.IsReadOnly = true;

                }
            }
        }

        public void LcdFunctionUpdate()
        {
            functionCanvas.Children.Clear();

            foreach (var item in Models.LCDs)
            {
                item.SetParentCanvas(functionCanvas);
                item.IsReadOnly = true;
            }
        }

        public void LedFunctionUpdate()
        {
            functionCanvas.Children.Clear();

            foreach (var item in Models.LED)
            {
                item.SetParentCanvas(functionCanvas);
                item.IsReadOnly = true;

                foreach (var led in item.LEDs)
                {
                    led.Visibility = led.Use ? Visibility.Visible : Visibility.Collapsed;
                }
            }


        }

        public void UpdateParentCanvas()
        {
            functionCanvas.UpdateLayout();
        }

        private void functionCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            FuntionsUpdate();
        }

     
    }
}
