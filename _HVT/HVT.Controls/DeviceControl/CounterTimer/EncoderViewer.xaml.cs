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
using LiveCharts;
using LiveCharts.Wpf;


namespace HVT.Controls
{
    /// <summary>
    /// Interaction logic for EncoderViewer.xaml
    /// </summary>
    public partial class EncoderViewer : UserControl
    {
        public ChartValues<double> LineValues { get; set; }
        public double AxisMin { get; set; }
        public double AxisMax { get; set; }

        public int WindowSize = 200; // Size of sliding window

        public EncoderViewer()
        {
            InitializeComponent();
            this.DataContext = this;

            LineValues = new ChartValues<double>();
            AxisMin = 0;
            AxisMax = WindowSize;
        }
    }
}
