using HVT.Controls.DevicesControl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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

namespace HVT.Controls
{

    public class LEDModel :INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public int Index { get;set; }

        public int State { get; set; } = -1;
        public Brush ColorState { get; set; } = Brushes.LightGray;


        public void UpdateStateColor()
        {
            ColorState = State > 0 ? Brushes.Lime : Brushes.Red;
            ColorState = State == -1 ? Brushes.LightGray : ColorState;


        }

    }

    /// <summary>
    /// Interaction logic for CDSViewer.xaml
    /// </summary>
    public partial class CDSViewer : UserControl
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<LEDModel> LEDs { get; set; } = new ObservableCollection<LEDModel>() ;
        public CDSViewer()
        {
            InitializeComponent();
            DataContext = this;

            for (int i = 0; i < 19; i++) {

                LEDs.Add(new LEDModel() { Index = i+1});
            }
        }


    }
}
