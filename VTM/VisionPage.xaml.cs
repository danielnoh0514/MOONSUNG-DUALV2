using Camera;
using HVT.Utility;
using HVT.VTM.Base;
using HVT.VTM.Program;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VTM
{
    /// <summary>
    /// Interaction logic for VisionPage.xaml
    /// </summary>
    public partial class VisionPage : Page
    {        //Variable
        private Step buf;
        private Step currentStep;

        private Model editModel = new Model();

        public Model EditModel
        {
            get { return editModel; }
            set
            {
                if (value != editModel)
                {
                    Program.EditModel = value;
                    editModel = value;

                    this.DataContext = EditModel;
                    if (this._contentLoaded)
                    {
                        if (EditModel.VisionModels != null)
                        {
                            VisionBuider.Models = EditModel.VisionModels;
                            componentOptionHolder.Child = EditModel.VisionModels.Option;
                        }
                        else
                        {
                            // row 바뀌면 바뀐 cmd 갖고오고 그게 LCD면 LCD 위치 해당 row lcd roi 갖고오기.
                            EditModel.VisionModels = new Camera.VisionModel();
                            VisionBuider.Models = EditModel.VisionModels;
                            componentOptionHolder.Child = VisionBuider.Models.Option;
                        }
                        cameraSetting.GetcameraSettingValue();

                        DataGrid_FND_0.ItemsSource = VisionBuider.Models.FNDs[0][0].PointSegments.LEDs;
                        DataGrid_FND_1.ItemsSource = VisionBuider.Models.FNDs[1][0].PointSegments.LEDs;
                        DataGrid_FND_2.ItemsSource = VisionBuider.Models.FNDs[2][0].PointSegments.LEDs;
                        DataGrid_FND_3.ItemsSource = VisionBuider.Models.FNDs[3][0].PointSegments.LEDs;
                        DataGrid_FND_4.ItemsSource = VisionBuider.Models.FNDs[4][0].PointSegments.LEDs;
                        DataGrid_FND_5.ItemsSource = VisionBuider.Models.FNDs[5][0].PointSegments.LEDs;
                        DataGrid_FND_6.ItemsSource = VisionBuider.Models.FNDs[6][0].PointSegments.LEDs;

                        GLEDsData.ItemsSource = VisionBuider.Models.GLED[0].GLEDs;
                        LEDsData.ItemsSource = VisionBuider.Models.LED[0].LEDs;
                        componentOptionHolder.Child = VisionBuider.Models.Option;
                        EditModel.VisionModels.UpdateLayout(EditModel.Layout.PCB_Count);
                        EditModel.Layout.PCB_COUNT_CHANGE += Layout_PCB_COUNT_CHANGE;
                        UpdateLayout(EditModel.Layout.PCB_Count);
                        cbbTxnaming_Manual.ItemsSource = editModel.Naming.TxDatas.Select(x => x.Name).ToList();
                        FND_lookupTable.Update(editModel.ModelSegmentLookup);
                    }
                }
            }
        }

        private Program program = new Program();

        public Program Program
        {
            get { return program; }
            set
            {
                program = value;
                Program.EditModel = editModel;
                SolenoidControl.SerialPort.Port = program.Solenoid.SerialPort.Port;
                RelayControl.SerialPort.Port = program.Relay.SerialPort.Port;
                SystemControl.System_Board = program.System.System_Board;
                UUT1Com.Content = Program.UUTs[0].LogBoxVision;
                UUT2Com.Content = Program.UUTs[1].LogBoxVision;
                UUT3Com.Content = Program.UUTs[2].LogBoxVision;
                UUT4Com.Content = Program.UUTs[3].LogBoxVision;
            }
        }

        private Timer GetFNDImageSampleTimer = new Timer
        {
            Interval = 100,
        };

        private Timer GetLCDImageSampleTimer = new Timer
        {
            Interval = 500,
        };

        public VisionPage()
        {
            InitializeComponent();




            // Timer get image for test
            GetFNDImageSampleTimer.Elapsed += GetImageSampleTimer_Elapsed;

            GetLCDImageSampleTimer.Elapsed += GetLCDImageSampleTimer_Elapsed;

            EditModel.VisionModels = VisionBuider.Models;

            componentOptionHolder.Child = VisionBuider.Models.Option;

            TransformGroup transformGroup = (TransformGroup)this.FindResource("sharedTransform");
            scaleTransform = (ScaleTransform)transformGroup.Children[0];
            translateTransform = (TranslateTransform)transformGroup.Children[1];
        }

        public void EnableLive()
        {
            GetFNDImageSampleTimer.Start();
            GetLCDImageSampleTimer.Start();
        }

        public void DisableLive()
        {
            GetFNDImageSampleTimer.Stop();
            GetLCDImageSampleTimer.Stop();
        }

        private void GetLCDImageSampleTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                Program.EditModel.VisionModels.GetLCDSampleImage(Program.Capture?.LastMatFrame);
            }));
        }

        private string ReplaceCharAtIndex(string s, int index, char newChar)
        {
            if (index < 0 || index >= s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
            }

            char[] chars = s.ToCharArray();
            chars[index] = newChar;
            return new string(chars);
        }


        private void GetImageSampleTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                Program.EditModel.VisionModels.GetFNDSampleImage(Program.Capture?.LastMatFrame);
                Program.EditModel.VisionModels.GetLEDSampleImage(Program.Capture?.LastMatFrame);
                Program.EditModel.VisionModels.GetGLEDSampleImage(Program.Capture?.LastMatFrame);

                if (tgbSelectA.IsChecked == true)
                {
                    lbGLEDvalue.Content = Program.EditModel.VisionModels.GLED[0].CalculatorOutputString;

                    string tempOutput = Program.EditModel.VisionModels.LED[0].CalculatorBinaryOutputString;
                    int indexLED = 0;
                    foreach (var item in Program.EditModel.VisionModels.LED[0].LEDs)
                    {
                        if (!item.Use)
                        {
                            tempOutput= ReplaceCharAtIndex(tempOutput, indexLED, 'X');
                        }
                        indexLED++;
                    }

                    
                    string hexString = Convert.ToInt32(new string(tempOutput.Replace("X", "").Reverse().ToArray()), 2).ToString("X");

                    lbLEDvalue.Content = hexString;
                }
                if (tgbSelectB.IsChecked == true)
                {
                    lbGLEDvalue.Content = Program.EditModel.VisionModels.GLED[1].CalculatorOutputString;

                    string tempOutput = Program.EditModel.VisionModels.LED[1].CalculatorBinaryOutputString;
                    int indexLED = 0;
                    foreach (var item in Program.EditModel.VisionModels.LED[1].LEDs)
                    {
                        if (!item.Use)
                        {
                            tempOutput = ReplaceCharAtIndex(tempOutput, indexLED, 'X');
                        }
                        indexLED++;
                    }

                    string hexString = Convert.ToInt32(new string(tempOutput.Replace("X", "").Reverse().ToArray()), 2).ToString("X");

                    lbLEDvalue.Content = hexString;
                }
                if (tgbSelectC.IsChecked == true)
                {
                    lbGLEDvalue.Content = Program.EditModel.VisionModels.GLED[2].CalculatorOutputString;
                    string tempOutput = Program.EditModel.VisionModels.LED[2].CalculatorBinaryOutputString;
                    int indexLED = 0;
                    foreach (var item in Program.EditModel.VisionModels.LED[2].LEDs)
                    {
                        if (!item.Use)
                        {
                            tempOutput = ReplaceCharAtIndex(tempOutput, indexLED, 'X');
                        }
                        indexLED++;
                    }

                    string hexString = Convert.ToInt32(new string(tempOutput.Replace("X", "").Reverse().ToArray()), 2).ToString("X");

                    lbLEDvalue.Content = hexString;
                }
                if (tgbSelectD.IsChecked == true)
                {
                    lbGLEDvalue.Content = Program.EditModel.VisionModels.GLED[3].CalculatorOutputString;
                    string tempOutput = Program.EditModel.VisionModels.LED[3].CalculatorBinaryOutputString;
                    int indexLED = 0;
                    foreach (var item in Program.EditModel.VisionModels.LED[3].LEDs)
                    {
                        if (!item.Use)
                        {
                            tempOutput = ReplaceCharAtIndex(tempOutput, indexLED, 'X');
                        }
                        indexLED++;
                    }

                    string hexString = Convert.ToInt32(new string(tempOutput.Replace("X", "").Reverse().ToArray()), 2).ToString("X");

                    lbLEDvalue.Content = hexString;
                }
            }));
        }

        private void btOpenModel_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog
            {
                DefaultExt = ".model",
                Title = "Open model",
            };
            openFile.Filter = "VTM model files (*.vmdl)|*.vmdl";
            openFile.RestoreDirectory = true;
            if (openFile.ShowDialog() == true)
            {
                HVT.Utility.Debug.Write("Load model:" + openFile.FileName, HVT.Utility.Debug.ContentType.Notify);
                //var fileInfor = new FileInfo(openFile.FileName);
                string modelStr = System.IO.File.ReadAllText(openFile.FileName);
                try
                {
                    string modelString = HVT.Utility.Extensions.Decoder(modelStr, System.Text.Encoding.UTF7);
                    //TestModel = HVT.Utility.Extensions.ConvertFromJson<Model>(modelString);
                    EditModel = HVT.Utility.Extensions.ConvertFromJson<Model>(modelString);
                    EditModel.Path = openFile.FileName;
                    foreach (var item in EditModel.Steps)
                    {
                        item.ValueGet1 = "";
                        item.ValueGet2 = "";
                        item.ValueGet3 = "";
                        item.ValueGet4 = "";
                    }
                    Program.OnEditModelLoaded();
                }
                catch (Exception)
                {
                    HVT.Utility.Debug.Write("Load model fail, file not correct format. \n" +
                        "Model folder: " + openFile.FileName, HVT.Utility.Debug.ContentType.Error);
                }
            }
        }

        private async void btSaveModel_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep != null)
            {
                if (currentStep.cmd == CMDs.LED)
                {
                    currentStep.LedList.Clear();

                    foreach (var item in LEDsData.Items)
                    {
                        var led = (item as Camera.SingleLED);
                        //var ledClone = led.Clone();
                        if (led.Use)
                        {
                            currentStep.LedList.Add(led.Clone());
                            // parentCanvas == null it cannot clone
                        }
                    }
                }
                if (currentStep.cmd == CMDs.LCD)
                {
                    var lcdList = VisionBuider.Models.LCDs;

                    currentStep.LCDRoiValue0 = lcdList[0].Rect;
                    currentStep.LCDRoiValue1 = lcdList[1].Rect;
                    currentStep.LCDRoiValue2 = lcdList[2].Rect;
                    currentStep.LCDRoiValue3 = lcdList[3].Rect;
                }

                if (currentStep.cmd == CMDs.FND)
                {
                    int index_char = 0;
                    foreach (var FNDchar in VisionBuider.Models.FNDs)
                    {
                        if (EditModel.Layout.PCB_Count >= 1)

                        {
                            for (int index_led = 0; index_led < 7; index_led++)
                            {
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].X = FNDchar[0].PointSegments.LEDs[index_led].X;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Y = FNDchar[0].PointSegments.LEDs[index_led].Y;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Dir = FNDchar[0].PointSegments.LEDs[index_led].Dir;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].ON = FNDchar[0].PointSegments.LEDs[index_led].ON;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].OFF = FNDchar[0].PointSegments.LEDs[index_led].OFF;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Thresh = FNDchar[0].PointSegments.LEDs[index_led].Thresh;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Use = FNDchar[0].PointSegments.LEDs[index_led].Use;
                            }

                            currentStep.RectFNDsBoard0[index_char] = FNDchar[0].rect;
                            //currentStep.FNDsBoard0[index_char].rect = FNDchar[0].rect;
                            currentStep.UseFNDsBoard0[index_char] = FNDchar[0].Use;
                            //currentStep.FNDsBoard0[index_char].Use = FNDchar[0].Use;
                        }

                        index_char++;
                    }
                }
            }

            EditModel.CameraSetting = cameraSetting.GetParammeter();
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.Brightness).ToString(), Debug.ContentType.Notify);
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.BackLight).ToString(), Debug.ContentType.Notify);
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.Contrast).ToString(), Debug.ContentType.Notify);
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.Exposure).ToString(), Debug.ContentType.Notify);
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.Focus).ToString(), Debug.ContentType.Notify);
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.Saturation).ToString(), Debug.ContentType.Notify);
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.Sharpness).ToString(), Debug.ContentType.Notify);
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.Zoom).ToString(), Debug.ContentType.Notify);
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.WhiteBalanceBlueU).ToString(), Debug.ContentType.Notify);
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.WhiteBalanceRedV).ToString(), Debug.ContentType.Notify);
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.Pan).ToString(), Debug.ContentType.Notify);
            //Debug.Write(Program.Capture.videoCapture.Get(OpenCvSharp.VideoCaptureProperties.Gain).ToString(), Debug.ContentType.Notify);

            EditModel.ModelSegmentLookup = Camera.FND.SEG_LOOKUP.Clone();
            if (File.Exists(EditModel.Path))
            {
                saveLabel.Visibility = Visibility.Visible;
                await Task.Delay(100);
                HVT.Utility.Extensions.SaveToFile(EditModel, EditModel.Path);
                //Program.OnEditModelSave();
                await Task.Delay(100);
                saveLabel.Visibility = Visibility.Hidden;
            }
            else
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.InitialDirectory = HVT.VTM.Program.FolderMap.RootFolder;
                saveFileDialog.AddExtension = true;
                saveFileDialog.DefaultExt = FolderMap.DefaultModelFileExt;
                if ((bool)saveFileDialog.ShowDialog())
                {
                    saveLabel.Visibility = Visibility.Visible;
                    await Task.Delay(100);
                    EditModel.Name = saveFileDialog.SafeFileName;
                    EditModel.Path = saveFileDialog.FileName;
                    HVT.Utility.Extensions.SaveToFile(EditModel, saveFileDialog.FileName);
                    await Task.Delay(100);
                    saveLabel.Visibility = Visibility.Hidden;
                }
            }
            Program.OnEditModelSave();
        }

        private async void btSaveAsModel_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep != null)
            {
                if (currentStep.cmd == CMDs.LED)
                {
                    if (currentStep.LedList.Count > 0)
                    {
                        currentStep.LedList.Clear();

                        foreach (var item in LEDsData.Items)
                        {
                            var led = (item as Camera.SingleLED);
                            //var ledClone = led.Clone();
                            if (led.Use)
                            {
                                currentStep.LedList.Add(led.Clone());
                                // parentCanvas == null it canot clone
                            }
                        }
                    }
                }

                if (currentStep.cmd == CMDs.LCD)
                {
                    var lcdList = VisionBuider.Models.LCDs;

                    currentStep.LCDRoiValue0 = lcdList[0].Rect;
                    currentStep.LCDRoiValue1 = lcdList[1].Rect;
                    currentStep.LCDRoiValue2 = lcdList[2].Rect;
                    currentStep.LCDRoiValue3 = lcdList[3].Rect;
                }

                if (currentStep.cmd == CMDs.FND)
                {
                    int index_char = 0;

                    foreach (var FNDchar in VisionBuider.Models.FNDs)
                    {
                        if (EditModel.Layout.PCB_Count >= 1)
                        {
                            for (int index_led = 0; index_led < 7; index_led++)
                            {
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].X = FNDchar[0].PointSegments.LEDs[index_led].X;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Y = FNDchar[0].PointSegments.LEDs[index_led].Y;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Dir = FNDchar[0].PointSegments.LEDs[index_led].Dir;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].ON = FNDchar[0].PointSegments.LEDs[index_led].ON;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].OFF = FNDchar[0].PointSegments.LEDs[index_led].OFF;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Thresh = FNDchar[0].PointSegments.LEDs[index_led].Thresh;
                                currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Use = FNDchar[0].PointSegments.LEDs[index_led].Use;
                            }

                            currentStep.RectFNDsBoard0[index_char] = FNDchar[0].rect;
                            //currentStep.FNDsBoard0[index_char].rect = FNDchar[0].rect;
                            currentStep.UseFNDsBoard0[index_char] = FNDchar[0].Use;
                            //currentStep.FNDsBoard0[index_char].Use = FNDchar[0].Use;
                        }

                        index_char++;
                    }
                }
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = HVT.VTM.Program.FolderMap.RootFolder;
            saveFileDialog.AddExtension = true;
            saveFileDialog.DefaultExt = FolderMap.DefaultModelFileExt;
            if ((bool)saveFileDialog.ShowDialog())
            {
                saveLabel.Visibility = Visibility.Visible;
                await Task.Delay(100);
                EditModel.Name = saveFileDialog.SafeFileName;
                EditModel.Path = saveFileDialog.FileName;
                EditModel.CameraSetting = cameraSetting.GetParammeter();
                EditModel.ModelSegmentLookup = Camera.FND.SEG_LOOKUP.Clone();
                HVT.Utility.Extensions.SaveToFile(EditModel, saveFileDialog.FileName);
                saveLabel.Visibility = Visibility.Hidden;
                await Task.Delay(100);
            }
            Program.OnEditModelSave();
        }

        private void LEDsData_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (LEDsData.SelectedItem != null)
            {
                LEDsData.ScrollIntoView(LEDsData.SelectedItem);
            }
        }

        private void waitCheckboxLED_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var item in LEDsData.Items)
            {
                (item as Camera.SingleLED).Use = false;
            }
        }

        private void waitCheckboxLED_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var item in LEDsData.Items)
            {
                (item as Camera.SingleLED).Use = true;
            }
        }

        private void waitCheckboxGLED_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var item in GLEDsData.Items)
            {
                (item as Camera.SingleGLED).Use = false;
            }
        }

        private void waitCheckboxGLED_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var item in GLEDsData.Items)
            {
                (item as Camera.SingleGLED).Use = true;
            }
        }

        private void UpdateLayout(int PCB_Count)
        {
            tgbSelectA.Visibility = PCB_Count >= 1 ? Visibility.Visible : Visibility.Collapsed;
            tgbSelectB.Visibility = PCB_Count >= 2 ? Visibility.Visible : Visibility.Collapsed;
            tgbSelectC.Visibility = PCB_Count >= 3 ? Visibility.Visible : Visibility.Collapsed;
            tgbSelectD.Visibility = PCB_Count >= 4 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BoardSelect_Click(object sender, RoutedEventArgs e)
        {
            var bt = (sender as ToggleButton);
            tgbSelectA.IsChecked = bt == tgbSelectA;
            tgbSelectB.IsChecked = bt == tgbSelectB;
            tgbSelectC.IsChecked = bt == tgbSelectC;
            tgbSelectD.IsChecked = bt == tgbSelectD;
            switch (bt.Name)
            {
                case "tgbSelectA":
                    DataGrid_FND_0.ItemsSource = VisionBuider.Models.FNDs[0][0].PointSegments.LEDs;
                    DataGrid_FND_1.ItemsSource = VisionBuider.Models.FNDs[1][0].PointSegments.LEDs;
                    DataGrid_FND_2.ItemsSource = VisionBuider.Models.FNDs[2][0].PointSegments.LEDs;
                    DataGrid_FND_3.ItemsSource = VisionBuider.Models.FNDs[3][0].PointSegments.LEDs;
                    DataGrid_FND_4.ItemsSource = VisionBuider.Models.FNDs[4][0].PointSegments.LEDs;
                    DataGrid_FND_5.ItemsSource = VisionBuider.Models.FNDs[5][0].PointSegments.LEDs;
                    DataGrid_FND_6.ItemsSource = VisionBuider.Models.FNDs[6][0].PointSegments.LEDs;

                    GLEDsData.ItemsSource = VisionBuider.Models.GLED[0].GLEDs;
                    LEDsData.ItemsSource = VisionBuider.Models.LED[0].LEDs;
                    break;

                case "tgbSelectB":
                    DataGrid_FND_0.ItemsSource = VisionBuider.Models.FNDs[0][1].PointSegments.LEDs;
                    DataGrid_FND_1.ItemsSource = VisionBuider.Models.FNDs[1][1].PointSegments.LEDs;
                    DataGrid_FND_2.ItemsSource = VisionBuider.Models.FNDs[2][1].PointSegments.LEDs;
                    DataGrid_FND_3.ItemsSource = VisionBuider.Models.FNDs[3][1].PointSegments.LEDs;
                    DataGrid_FND_4.ItemsSource = VisionBuider.Models.FNDs[4][1].PointSegments.LEDs;
                    DataGrid_FND_5.ItemsSource = VisionBuider.Models.FNDs[5][1].PointSegments.LEDs;
                    DataGrid_FND_6.ItemsSource = VisionBuider.Models.FNDs[6][1].PointSegments.LEDs;

                    GLEDsData.ItemsSource = VisionBuider.Models.GLED[1].GLEDs;
                    LEDsData.ItemsSource = VisionBuider.Models.LED[1].LEDs;
                    break;

                case "tgbSelectC":
                    DataGrid_FND_0.ItemsSource = VisionBuider.Models.FNDs[0][2].PointSegments.LEDs;
                    DataGrid_FND_1.ItemsSource = VisionBuider.Models.FNDs[1][2].PointSegments.LEDs;
                    DataGrid_FND_2.ItemsSource = VisionBuider.Models.FNDs[2][2].PointSegments.LEDs;
                    DataGrid_FND_3.ItemsSource = VisionBuider.Models.FNDs[3][2].PointSegments.LEDs;
                    DataGrid_FND_4.ItemsSource = VisionBuider.Models.FNDs[4][2].PointSegments.LEDs;
                    DataGrid_FND_5.ItemsSource = VisionBuider.Models.FNDs[5][2].PointSegments.LEDs;
                    DataGrid_FND_6.ItemsSource = VisionBuider.Models.FNDs[6][2].PointSegments.LEDs;

                    GLEDsData.ItemsSource = VisionBuider.Models.GLED[2].GLEDs;
                    LEDsData.ItemsSource = VisionBuider.Models.LED[2].LEDs;
                    break;

                case "tgbSelectD":
                    DataGrid_FND_0.ItemsSource = VisionBuider.Models.FNDs[0][3].PointSegments.LEDs;
                    DataGrid_FND_1.ItemsSource = VisionBuider.Models.FNDs[1][3].PointSegments.LEDs;
                    DataGrid_FND_2.ItemsSource = VisionBuider.Models.FNDs[2][3].PointSegments.LEDs;
                    DataGrid_FND_3.ItemsSource = VisionBuider.Models.FNDs[3][3].PointSegments.LEDs;
                    DataGrid_FND_4.ItemsSource = VisionBuider.Models.FNDs[4][3].PointSegments.LEDs;
                    DataGrid_FND_5.ItemsSource = VisionBuider.Models.FNDs[5][3].PointSegments.LEDs;
                    DataGrid_FND_6.ItemsSource = VisionBuider.Models.FNDs[6][3].PointSegments.LEDs;

                    GLEDsData.ItemsSource = VisionBuider.Models.GLED[3].GLEDs;
                    LEDsData.ItemsSource = VisionBuider.Models.LED[3].LEDs;
                    break;

                default:
                    break;
            }
        }

        private void Layout_PCB_COUNT_CHANGE(object sender, EventArgs e)
        {
            UpdateLayout(EditModel.Layout.PCB_Count);
        }

        private void btGetValue_Click(object sender, RoutedEventArgs e)
        {
            Program.EditModel.VisionModels.GetLEDSampleImage(Program.Capture?.LastMatFrame);
            if (tgbSelectA.IsChecked == true)
            {
                lbLEDvalue.Content = Program.EditModel.VisionModels.LED[0].CalculatorOutputString;
            }
            if (tgbSelectB.IsChecked == true)
            {
                lbLEDvalue.Content = Program.EditModel.VisionModels.LED[1].CalculatorOutputString;
            }
            if (tgbSelectC.IsChecked == true)
            {
                lbLEDvalue.Content = Program.EditModel.VisionModels.LED[2].CalculatorOutputString;
            }
            if (tgbSelectD.IsChecked == true)
            {
                lbLEDvalue.Content = Program.EditModel.VisionModels.LED[3].CalculatorOutputString;
            }
        }

        private void btGetGLEDValue_Click(object sender, RoutedEventArgs e)
        {
            Program.EditModel.VisionModels.GetGLEDSampleImage(Program.Capture?.LastMatFrame);
            if (tgbSelectA.IsChecked == true)
            {
                lbGLEDvalue.Content = Program.EditModel.VisionModels.GLED[0].CalculatorOutputString;
            }
            if (tgbSelectB.IsChecked == true)
            {
                lbGLEDvalue.Content = Program.EditModel.VisionModels.GLED[1].CalculatorOutputString;
            }
            if (tgbSelectC.IsChecked == true)
            {
                lbGLEDvalue.Content = Program.EditModel.VisionModels.GLED[2].CalculatorOutputString;
            }
            if (tgbSelectD.IsChecked == true)
            {
                lbGLEDvalue.Content = Program.EditModel.VisionModels.GLED[3].CalculatorOutputString;
            }
        }

        private void btThresholdCalculate_Click(object sender, RoutedEventArgs e)
        {
            if (tgbSelectA.IsChecked == true)
            {
                Program.EditModel.VisionModels.LED[0].CALC_THRESH();
            }
            if (tgbSelectB.IsChecked == true)
            {
                Program.EditModel.VisionModels.LED[1].CALC_THRESH();
            }
            if (tgbSelectC.IsChecked == true)
            {
                Program.EditModel.VisionModels.LED[2].CALC_THRESH();
            }
            if (tgbSelectD.IsChecked == true)
            {
                Program.EditModel.VisionModels.LED[3].CALC_THRESH();
            }
        }

        private void btGLEDThresholdCalculate_Click(object sender, RoutedEventArgs e)
        {
            if (tgbSelectA.IsChecked == true)
            {
                Program.EditModel.VisionModels.GLED[0].CALC_THRESH();
            }
            if (tgbSelectB.IsChecked == true)
            {
                Program.EditModel.VisionModels.GLED[1].CALC_THRESH();
            }
            if (tgbSelectC.IsChecked == true)
            {
                Program.EditModel.VisionModels.GLED[2].CALC_THRESH();
            }
            if (tgbSelectD.IsChecked == true)
            {
                Program.EditModel.VisionModels.GLED[3].CALC_THRESH();
            }
        }

        private void cbbTxnaming_Mainual_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbbTxnaming_Manual.SelectedItem != null)
            {
                var txData = EditModel.Naming.TxDatas.Where(o => o.Name == (string)cbbTxnaming_Manual.SelectedItem).First();
                if (txData != null)
                {
                    var data = cbbUUTconfig_Manual.Text == "P1" ? EditModel.P1_Config.GetFrame(txData.Data) : EditModel.P2_Config.GetFrame(txData.Data);
                    string dataStr = "";
                    foreach (var item in data)
                    {
                        dataStr += item.ToString("X2") + " ";
                    }
                    lbTxData.Content = dataStr;
                }
            }
        }

        private void ButtonSendUUT_Click(object sender, RoutedEventArgs e)
        {
            if (cbbTxnaming_Manual.SelectedItem != null)
            {
                var txData = EditModel.Naming.TxDatas.Where(o => o.Name == (string)cbbTxnaming_Manual.SelectedItem).First();
                foreach (var item in Program.UUTs)
                {
                    if (cbbUUTconfig_Manual.Text == "P1" && item.Config != EditModel.P1_Config)
                    {
                        item.Config = EditModel.P1_Config;
                    }
                    else if (cbbUUTconfig_Manual.Text == "P2")
                    {
                        item.Config = EditModel.P1_Config;
                    }
                }
                if (txData != null)
                {
                    if (EditModel.Layout.PCB_Count >= 1) Program.UUTs[0].Send(txData);
                    if (EditModel.Layout.PCB_Count >= 2) Program.UUTs[1].Send(txData);
                    if (EditModel.Layout.PCB_Count >= 3) Program.UUTs[2].Send(txData);
                    if (EditModel.Layout.PCB_Count >= 4) Program.UUTs[3].Send(txData);
                }
            }
        }

        private void VisionStepsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // when user change step
            // then buf has the checkbox value (checked or not checked)
            //waitCheckbox1.Checked = true;
            // previous, current
            // previous save roi
            // if previous = current

            // --- LED ---
            // step's led.isUse[] (which is the index of the led so i can both turn on/off (visually) as well as retrieve data properly)
            // so that i can turn the using led's for each step and get their values

            // 스탭 당 led 만 보여주기

            currentStep = (sender as DataGrid).SelectedItem as Step;

            // 새로운 row 선택할때
            if (buf != null)
            {
                buf.LedList.Clear();

                foreach (var item in LEDsData.Items)
                {
                    var led = (item as Camera.SingleLED);
                    //var ledClone = led.Clone();
                    if (led.Use)
                    {
                    buf.LedList.Add(led.Clone());
                        // parentCanvas == null it canot clone
                }
                }

                var lcdList = VisionBuider.Models.LCDs;

                buf.LCDRoiValue0 = lcdList[0].Rect;
                buf.LCDRoiValue1 = lcdList[1].Rect;
                buf.LCDRoiValue2 = lcdList[2].Rect;
                buf.LCDRoiValue3 = lcdList[3].Rect;



                int index_char = 0;

                foreach (var FNDchar in VisionBuider.Models.FNDs)
                {
                    if (EditModel.Layout.PCB_Count >= 1)
                    {

                        for (int index_led = 0; index_led < 7; index_led++)
                        {
                            buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].X = FNDchar[0].PointSegments.LEDs[index_led].X;
                            buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Y = FNDchar[0].PointSegments.LEDs[index_led].Y;
                            buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Dir = FNDchar[0].PointSegments.LEDs[index_led].Dir;
                            buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].ON = FNDchar[0].PointSegments.LEDs[index_led].ON;
                            buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].OFF = FNDchar[0].PointSegments.LEDs[index_led].OFF;
                            buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Thresh = FNDchar[0].PointSegments.LEDs[index_led].Thresh;
                            buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Use = FNDchar[0].PointSegments.LEDs[index_led].Use;
                        }

                        buf.UseFNDsBoard0[index_char] = buf.FNDsBoard0[index_char].PointSegments.LEDs.Any(led => led.Use);

                    }

                    index_char++;
                }
            }

            // buf 없을때
            if (buf != currentStep && currentStep != null)
            {
                buf = currentStep;

                foreach (var item in LEDsData.Items)
                {
                    (item as Camera.SingleLED).Use = false;
                }

                // LEDs = 32
                if (buf.LedList.Count > 0)
                {
                    foreach (var item in buf.LedList)
                    {
                        var led = (LEDsData.Items[item.Index] as Camera.SingleLED);
                        led.X = item.X;
                        led.Y = item.Y;
                        led.Dir = item.Dir;
                        led.ON = item.ON;
                        led.OFF = item.OFF;
                        led.Thresh = item.Thresh;
                        led.Use = item.Use;
                    }
                }

                var lcdList = VisionBuider.Models.LCDs;

                lcdList[0].Rect = buf.LCDRoiValue0;
                lcdList[1].Rect = buf.LCDRoiValue1;
                lcdList[2].Rect = buf.LCDRoiValue2;
                lcdList[3].Rect = buf.LCDRoiValue3;

                int index_char = 0;
                foreach (var FNDchar in VisionBuider.Models.FNDs)
                {
                    if (EditModel.Layout.PCB_Count >= 1)
                    {
                        for (int index_led = 0; index_led < 7; index_led++)
                        {
                            FNDchar[0].PointSegments.LEDs[index_led].X = buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].X;
                            FNDchar[0].PointSegments.LEDs[index_led].Y = buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Y;
                            FNDchar[0].PointSegments.LEDs[index_led].Dir = buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Dir;
                            FNDchar[0].PointSegments.LEDs[index_led].ON = buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].ON;
                            FNDchar[0].PointSegments.LEDs[index_led].OFF = buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].OFF;
                            FNDchar[0].PointSegments.LEDs[index_led].Thresh = buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Thresh;
                            FNDchar[0].PointSegments.LEDs[index_led].Use = buf.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Use;
                        }

                        FNDchar[0].Rect = FNDchar[0].Rect;
                        //FNDchar[0].Rect = buf.RectFNDsBoard0[index_char];
                        FNDchar[0].Use = FNDchar[0].PointSegments.LEDs.Any(led => led.Use);
                    }

                    index_char++;
                }
            }
        }
        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            // Traverse up the visual tree
            while (child != null)
            {
                if (child is T parent)
                {
                    return parent;
                }
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
        private void FNDSegmentsData_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid != null)
            {
                if (dataGrid.SelectedItem != null)
                {
                    dataGrid.ScrollIntoView(dataGrid.SelectedItem);
                }
            }
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var FNDchar in VisionBuider.Models.FNDs)
            {
                if (tgbSelectA.IsChecked == true)
                {
                    FNDchar[0].Use = FNDchar[0].PointSegments.LEDs.Any(led => led.Use);
                }
            }
        }
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var FNDchar in VisionBuider.Models.FNDs)
            {
                if (tgbSelectA.IsChecked == true)
                {
                    FNDchar[0].Use = FNDchar[0].PointSegments.LEDs.Any(led => led.Use);
                }
            }
        }
        private void FND_Use_HeaderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                var dataGrid = FindParent<DataGrid>(checkbox);
                if (dataGrid != null)
                {
                    int fnd_idx = Int32.Parse(dataGrid.Name.Split('_')[2]);

                    if (tgbSelectA.IsChecked == true)
                    {
                        VisionBuider.Models.FNDs[fnd_idx][0].Use = true;
                        foreach (var singleLED in VisionBuider.Models.FNDs[fnd_idx][0].PointSegments.LEDs)
                        {
                            singleLED.Use = true;
                        }
                    }

                }
                else
                {
                    throw new Exception("could find DataGrid");
                }
            }
        }

        private void FND_Use_HeaderCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                var dataGrid = FindParent<DataGrid>(checkbox);
                if (dataGrid != null)
                {
                    int fnd_idx = Int32.Parse(dataGrid.Name.Split('_')[2]);


                    if (tgbSelectA.IsChecked == true)
                    {
                        VisionBuider.Models.FNDs[fnd_idx][0].Use = false;
                        foreach (var singleLED in VisionBuider.Models.FNDs[fnd_idx][0].PointSegments.LEDs)
                        {
                            singleLED.Use = false;
                        }
                    }

                }
                else
                {
                    throw new Exception("could find DataGrid");
                }
            }
        }

        private void VisibilityCheckboxFNDSegment_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void VisibilityCheckboxFNDSegment_Unchecked(object sender, RoutedEventArgs e)
        {

        }

        private void btGetFNDSegmentValue_Click(object sender, RoutedEventArgs e)
        {
            Program.EditModel.VisionModels.GetFNDSampleImage(Program.Capture?.LastMatFrame);
            if (tgbSelectA.IsChecked == true)
            {
                lbMatrixPointValue.Content = "";
                for (int i = 0; i < Program.EditModel.VisionModels.FNDs.Count; i++)
                {
                    lbMatrixPointValue.Content += Program.EditModel.VisionModels.FNDs[i][0].DetectedString;
                }
            }
            if (tgbSelectB.IsChecked == true)
            {
                lbMatrixPointValue.Content = "";
                for (int i = 0; i < Program.EditModel.VisionModels.FNDs.Count; i++)
                {
                    lbMatrixPointValue.Content += Program.EditModel.VisionModels.FNDs[i][1].DetectedString;
                }
            }
            if (tgbSelectC.IsChecked == true)
            {
                lbMatrixPointValue.Content = "";
                for (int i = 0; i < Program.EditModel.VisionModels.FNDs.Count; i++)
                {
                    lbMatrixPointValue.Content += Program.EditModel.VisionModels.FNDs[i][2].DetectedString;
                }
            }
            if (tgbSelectD.IsChecked == true)
            {
                lbMatrixPointValue.Content = "";
                for (int i = 0; i < Program.EditModel.VisionModels.FNDs.Count; i++)
                {
                    lbMatrixPointValue.Content += Program.EditModel.VisionModels.FNDs[i][2].DetectedString;
                }
            }
        }

        private void btFNDSegmentThresholdCalculate_Click(object sender, RoutedEventArgs e)
        {
            if (tgbSelectA.IsChecked == true)
            {
                for (int i = 0; i < Program.EditModel.VisionModels.FNDs.Count; i++)
                {
                    Program.EditModel.VisionModels.FNDs[i][0].PointSegments.CALC_THRESH();
                }
            }
            if (tgbSelectB.IsChecked == true)
            {
                for (int i = 0; i < Program.EditModel.VisionModels.FNDs.Count; i++)
                {
                    Program.EditModel.VisionModels.FNDs[i][1].PointSegments.CALC_THRESH();
                }
            }
            if (tgbSelectC.IsChecked == true)
            {
                for (int i = 0; i < Program.EditModel.VisionModels.FNDs.Count; i++)
                {
                    Program.EditModel.VisionModels.FNDs[i][2].PointSegments.CALC_THRESH();
                }
            }
            if (tgbSelectD.IsChecked == true)
            {
                for (int i = 0; i < Program.EditModel.VisionModels.FNDs.Count; i++)
                {
                    Program.EditModel.VisionModels.FNDs[i][3].PointSegments.CALC_THRESH();
                }
            }
        }

        private ScaleTransform scaleTransform;
        private TranslateTransform translateTransform;
        private const double MinScale = 1.0;
        private const double MaxScale = 5.0;

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 0.1 : -0.1;
            double newScale = scaleTransform.ScaleX + zoomFactor;

            if (newScale < MinScale || newScale > MaxScale)
                return;

            Point cursorPosition = e.GetPosition(mainCanvas);

            double relativeX = (cursorPosition.X - translateTransform.X) / scaleTransform.ScaleX;
            double relativeY = (cursorPosition.Y - translateTransform.Y) / scaleTransform.ScaleY;

            scaleTransform.ScaleX = newScale;
            scaleTransform.ScaleY = newScale;

            translateTransform.X = cursorPosition.X - relativeX * newScale;
            translateTransform.Y = cursorPosition.Y - relativeY * newScale;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            scaleTransform.ScaleX = 1.0;
            scaleTransform.ScaleY = 1.0;
            translateTransform.X = 0;
            translateTransform.Y = 0;
        }


    }
}