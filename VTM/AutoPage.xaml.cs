using HVT.Utility;
using HVT.VTM.Base;
using HVT.VTM.Program;
using Microsoft.Win32;
using System.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Timer = System.Timers.Timer;
using Camera;
using HVT.Controls;

namespace VTM
{
    /// <summary>
    /// Interaction logic for AutoPage.xaml
    /// </summary>
    public partial class AutoPage : Page
    {
        public CancellationTokenSource _shutDown = new CancellationTokenSource();

        //Variable
        private Model testModel = new Model();

        public Model TestModel
        {
            get { return testModel; }
            set
            {
                if (value != testModel)
                {
                    testModel = value;
                    if (dgModelSteps != null) dgModelSteps.ItemsSource = TestModel.Steps;
                    if (lbModelName != null) lbModelName.Text = TestModel.Name;

                    VisionTester.Models = TestModel.VisionModels.Clone();
                    VisionTester.Models.UpdateLayout(TestModel.Layout.PCB_Count);

                    VisionTester.FuntionsUpdate();

                    dtttSite1.Visibility = TestModel.Layout.PCB_Count >= 1 ? Visibility.Visible : Visibility.Collapsed;
                    dtttSite2.Visibility = TestModel.Layout.PCB_Count >= 2 ? Visibility.Visible : Visibility.Collapsed;
                    dtttSite3.Visibility = TestModel.Layout.PCB_Count >= 3 ? Visibility.Visible : Visibility.Collapsed;
                    dtttSite4.Visibility = TestModel.Layout.PCB_Count >= 4 ? Visibility.Visible : Visibility.Collapsed;

                    Program.ResultPanel.PBA = TestModel.Layout;
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
                program.StepTestChange += Program_StepTestChange;
                program.StateChange += Program_StateChange;
                program.TestRunFinish += Program_TestRunFinish;
                program.EscapTimeChange += Program_EscapTimeChange;
                program.TesttingStateChange += Program_TesttingStateChange;
                dgrBarcode.ItemsSource = program.Boards;
                dgrVersion.ItemsSource = program.Boards;
                ResultPannelHolder.Child = program.ResultPanel;
            }
        }



        private void Program_TesttingStateChange(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                btAutoRun.Content = Program.IsTestting ? "STOP" : "RUN";
            }
            ));
        }

        private void Program_EscapTimeChange(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(new Action(() =>
            lbEscapTime.Content = Program.EscapTime.ToString("0.00") + "s"
            ));
        }

        private Timer GetFNDImageSampleTimer = new Timer
        {
            Interval = 100,
        };

        public AutoPage()
        {
            InitializeComponent();
            Debug.dispatcher = this.Dispatcher;
            Debug.LogBox = rtbProgramLog;
            btAutoRun.IsEnabled = false;

            dtttSite1.Visibility = TestModel.Layout.PCB_Count >= 1 ? Visibility.Visible : Visibility.Collapsed;
            dtttSite2.Visibility = TestModel.Layout.PCB_Count >= 2 ? Visibility.Visible : Visibility.Collapsed;
            dtttSite3.Visibility = TestModel.Layout.PCB_Count >= 3 ? Visibility.Visible : Visibility.Collapsed;
            dtttSite4.Visibility = TestModel.Layout.PCB_Count >= 4 ? Visibility.Visible : Visibility.Collapsed;

            GetFNDImageSampleTimer.Elapsed += GetImageSampleTimer_Elapsed;
            GetFNDImageSampleTimer.Start();
        }

        public string LoadModel()
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
                Debug.Write("Load model:" + openFile.FileName, Debug.ContentType.Notify);

                string modelStr = System.IO.File.ReadAllText(openFile.FileName);
                try
                {
                    string modelString = Extensions.Decoder(modelStr, System.Text.Encoding.UTF7);

                    TestModel = Extensions.ConvertFromJson<Model>(modelString);
                    TestModel.Path = openFile.FileName;
                    foreach (var item in TestModel.Steps)
                    {
                        item.ValueGet1 = "";
                        item.ValueGet2 = "";
                        item.ValueGet3 = "";
                        item.ValueGet4 = "";
                    }
                    Program.TestModel = TestModel;
                    btAutoRun.IsEnabled = true;
                    return modelString;
                }
                catch (Exception)
                {
                    HVT.Utility.Debug.Write("Load model fail, file not correct format. \n" +
                        "Model folder: " + openFile.FileName, Debug.ContentType.Error);
                }
            }

            return null;
        }

        public string LoadModel(string path)
        {
            Debug.Write("Load model:" + path, HVT.Utility.Debug.ContentType.Notify);
            if (path == null) return null;

            //var fileInfor = new FileInfo(openFile.FileName);
            string modelStr = System.IO.File.ReadAllText(path);
            if (modelStr == null)
            {
                return null;
            }
            try
            {
                string modelString = Extensions.Decoder(modelStr, System.Text.Encoding.UTF7);
                //Console.WriteLine(modelString);
                //TestModel = HVT.Utility.Extensions.ConvertFromJson<Model>(modelString);
                TestModel = Extensions.ConvertFromJson<Model>(modelString);
                foreach (var item in TestModel.Steps)
                {
                    item.ValueGet1 = "";
                    item.ValueGet2 = "";
                    item.ValueGet3 = "";
                    item.ValueGet4 = "";
                }
                Program.TestModel = TestModel;
                btAutoRun.IsEnabled = true;
                return modelString;
            }
            catch (Exception)
            {
            }

            return null;
        }

        private void Program_StepTestChange(object sender, EventArgs e)
        {
            if (!this._shutDown.IsCancellationRequested)
            {
                pgbTestProgress.Dispatcher.Invoke(new Action(() =>
                {
                    var progress = Math.Round((((int)sender + 1) / (double)TestModel.Steps.Count) * 100.0, 2);
                    pgbTestProgress.Value = (int)sender > 0 ? pgbTestProgress.Value < progress ? progress : pgbTestProgress.Value : progress;
                    dgModelSteps.SelectedIndex = (int)sender;
                    dgModelSteps.ScrollIntoView(dgModelSteps.SelectedItem);
                }
                ));

                Program.VisionTester.Dispatcher.Invoke(new Action(() =>
                {
                    var currentStep = (dgModelSteps.SelectedItem as Step);
                    UpdateLcdRoi(currentStep);
                    UpdateFndRoi(currentStep);
                    UpdateLedRoi(currentStep);
                }));
            }
        }

        private void UpdateLedRoi(Step currentStep)
        {
            // when manual page is commented out,
            // the UI updates but the actual roi doesn't get updated

            if (currentStep.CMD == CMDs.LED.ToString())
            {
                var ledListAutoPage = VisionTester.Models.LED;
                ledListAutoPage[0].LEDs = currentStep.LedList;

                foreach (var led in ledListAutoPage[0].LEDs)
                {
                    led.SetPosition();
                }
                VisionTester.LedFunctionUpdate();
            }
        }

        private void UpdateLcdRoi(Step currentStep)
        {
            if (currentStep.CMD == CMDs.LCD.ToString())
            {
                var lcdList = VisionTester.Models.LCDs;
                lcdList[0].Rect = currentStep.LCDRoiValue0;
                lcdList[1].Rect = currentStep.LCDRoiValue1;
                lcdList[2].Rect = currentStep.LCDRoiValue2;
                lcdList[3].Rect = currentStep.LCDRoiValue3;
                VisionTester.LcdFunctionUpdate();
            }
        }

        private void UpdateFndRoi(Step currentStep)
        {
            if (currentStep.CMD == CMDs.FND.ToString())
            {
                int index_char = 0;
                foreach (var FNDchar in VisionTester.Models.FNDs)
                {
                    if (TestModel.Layout.PCB_Count >= 1)
                    {
                        for (int index_led = 0; index_led < 7; index_led++)
                        {
                            FNDchar[0].PointSegments.LEDs[index_led].X = currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].X;
                            FNDchar[0].PointSegments.LEDs[index_led].Y = currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Y;
                            FNDchar[0].PointSegments.LEDs[index_led].Dir = currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Dir;
                            FNDchar[0].PointSegments.LEDs[index_led].ON = currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].ON;
                            FNDchar[0].PointSegments.LEDs[index_led].OFF = currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].OFF;
                            FNDchar[0].PointSegments.LEDs[index_led].Thresh = currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Thresh;
                            FNDchar[0].PointSegments.LEDs[index_led].Use = currentStep.FNDsBoard0[index_char].PointSegments.LEDs[index_led].Use;
                        }

                        FNDchar[0].Rect = currentStep.RectFNDsBoard0[index_char];
                        FNDchar[0].Use = currentStep.UseFNDsBoard0[index_char];
                    }

                    index_char++;
                }

                VisionTester.FndFunctionUpdate();
            }
        }

        private void Program_StateChange(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new Action((
                delegate
                {
                    lbTestResultTesting.Visibility = Visibility.Hidden;
                    lbTestResultStop.Visibility = Visibility.Hidden;
                    lbTestResultPause.Visibility = Visibility.Hidden;
                    lbTestResultGood.Visibility = Visibility.Hidden;
                    lbTestResultFail.Visibility = Visibility.Hidden;
                    lbTestBusy.Visibility = Visibility.Hidden;
                    lbTestResultWait.Visibility = Visibility.Hidden;
                    lbTestReady.Visibility = Visibility.Hidden;
                }

                )));
            switch (Program.TestState)
            {
                case Program.RunTestState.WAIT:
                    Dispatcher.Invoke(new Action(delegate
                    {
                        lbTestResultWait.Visibility = Visibility.Visible;
                        Storyboard sb = (Storyboard)TryFindResource("LabelSlide");
                        if (sb != null) sb.Begin(lbTestResultWait);
                    }), DispatcherPriority.Send);

                    //Program.AppSetting.Communication.Network.NotifyStatus(MachineStatus.WAIT);

                    break;

                case Program.RunTestState.TESTTING:
                    Dispatcher.Invoke(new Action(delegate
                    {
                        lbTestResultTesting.Visibility = Visibility.Visible;
                    }), DispatcherPriority.Send);

                    //Program.AppSetting.Communication.Network.NotifyStatus(MachineStatus.TESTING);

                    break;

                case Program.RunTestState.PAUSE:
                    Dispatcher.Invoke(new Action(delegate
                    {
                        lbTestResultPause.Visibility = Visibility.Visible;
                    }), DispatcherPriority.Send);

                    break;

                case Program.RunTestState.STOP:
                    Dispatcher.Invoke(new Action(delegate
                    {
                        lbTestResultStop.Visibility = Visibility.Visible;
                    }), DispatcherPriority.Send);
                    break;

                case Program.RunTestState.GOOD:
                    Dispatcher.Invoke(new Action(delegate
                    {
                        lbTestResultGood.Visibility = Visibility.Visible;
                        Storyboard sb = (Storyboard)TryFindResource("LabelSlide");
                        if (sb != null) sb.Begin(lbTestResultGood);
                    }), DispatcherPriority.Send);


                    break;

                case Program.RunTestState.OK_TESTING:
                   
                    //Program.AppSetting.Communication.Network.NotifyStatus(MachineStatus.OK_TESTING);

                    break;


                case Program.RunTestState.FAIL:
                    Dispatcher.Invoke(new Action(delegate
                    {
                        lbTestResultFail.Visibility = Visibility.Visible;
                        Storyboard sb = (Storyboard)TryFindResource("LabelSlide");
                        if (sb != null) sb.Begin(lbTestResultFail);
                    }), DispatcherPriority.Send);

                    break;

                case Program.RunTestState.BUSY:
                    Dispatcher.Invoke(new Action(delegate
                    {
                        lbTestBusy.Visibility = Visibility.Visible;
                    }), DispatcherPriority.Send);
                    break;

                case Program.RunTestState.READY:
                    Dispatcher.Invoke(new Action(delegate
                    {
                        lbTestReady.Visibility = Visibility.Visible;
                        Storyboard sb = (Storyboard)TryFindResource("LabelSlide");
                        if (sb != null) sb.Begin(lbTestReady);
                    }), DispatcherPriority.Send);

                    //Program.AppSetting.Communication.Network.NotifyStatus(MachineStatus.READY);
                    break;

                case Program.RunTestState.DONE:

                    //EscapTimer.Stop();
                    break;

                default:
                    break;
            }
        }

        private void Program_TestRunFinish(object sender, EventArgs e)
        {

            graphResult.Dispatcher.Invoke(
                new Action(() =>
                {
                    int pass = 0;
                    int fail = 0;
                    foreach (var item in Program.Boards)
                    {
                        if (!item.UserSkip && item.Result == "OK")
                        {
                            pass++;
                        }

                        if (!item.UserSkip && item.Result == "FAIL")
                        {
                            fail++;
                        }
                    }
                    graphResult.Pass += pass;
                    graphResult.Fail += fail;
                    graphResult.Draw();
                }));
        }

        private void btAutoRun_Click(object sender, RoutedEventArgs e)
        {
            if (Program.IsTestting)
            {
                Program.TestState = Program.RunTestState.STOP;
                Program.IsTestting = false;
                btAutoRun.Content = "RUN";
            }
            else if (Program.TestState == Program.RunTestState.READY)
            {
                Program.IsTestting = true;
                btAutoRun.Content = "STOP";
            }
        }

        private void waitCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            //if (Program.IsTestting) return;
            //DependencyObject dep = (DependencyObject)e.OriginalSource;
            //while ((dep != null) && !(dep is DataGridColumnHeader))
            //{
            //    dep = VisualTreeHelper.GetParent(dep);
            //}

            //if (dep == null)
            //    return;
            //if (dep is DataGridColumnHeader)
            //{
            //    DataGridColumnHeader columnHeader = dep as DataGridColumnHeader;
            //    columnHeader.Background = new SolidColorBrush(Color.FromRgb(21, 21, 21));
            //    Console.WriteLine(columnHeader.Content);
            //    string columnEnable = columnHeader.Content.ToString();
            //    switch (columnEnable)
            //    {
            //        case "A":
            //            if (Program.Boards.Count >= 1) Program.Boards[0].UserSkip = false;
            //            break;
            //        case "B":
            //            if (Program.Boards.Count >= 2) Program.Boards[1].UserSkip = false;
            //            break;
            //        case "C":
            //            if (Program.Boards.Count >= 3) Program.Boards[2].UserSkip = false;
            //            break;
            //        case "D":
            //            if (Program.Boards.Count >= 4) Program.Boards[3].UserSkip = false;
            //            break;
            //        default:
            //            break;
            //    }
            //}
        }

        private void waitCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            //if (Program.IsTestting) return;
            //DependencyObject dep = (DependencyObject)e.OriginalSource;
            //while ((dep != null) && !(dep is DataGridColumnHeader))
            //{
            //    dep = VisualTreeHelper.GetParent(dep);
            //}

            //if (dep == null)
            //    return;
            //if (dep is DataGridColumnHeader)
            //{
            //    DataGridColumnHeader columnHeader = dep as DataGridColumnHeader;
            //    columnHeader.Background = new SolidColorBrush(Colors.Gray);
            //    Console.WriteLine(columnHeader.Content);
            //    string columnEnable = columnHeader.Content.ToString();
            //    switch (columnEnable)
            //    {
            //        case "A":
            //            if (Program.Boards.Count >= 1) Program.Boards[0].UserSkip = true;
            //            break;
            //        case "B":
            //            if (Program.Boards.Count >= 2) Program.Boards[1].UserSkip = true;
            //            break;
            //        case "C":
            //            if (Program.Boards.Count >= 3) Program.Boards[2].UserSkip = true;
            //            break;
            //        case "D":
            //            if (Program.Boards.Count >= 4) Program.Boards[3].UserSkip = true;
            //            break;
            //        default:
            //            break;
            //    }
            //}
        }

        private void GetImageSampleTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                var lastFrameToTest = Program.Capture?.LastMatFrame;
                if (lastFrameToTest == null)
                {
                    return;
                }
                VisionTester.Models.GetFNDSampleImage(lastFrameToTest);
                VisionTester.Models.GetGLEDSampleImage(lastFrameToTest);
                VisionTester.Models.GetLEDSampleImage(lastFrameToTest);
            }));
        }

        public void EnableLive()
        {
            GetFNDImageSampleTimer.Start();
        }

        public void DisableLive()
        {
            GetFNDImageSampleTimer.Stop();
        }

        private void ResultPannelHolder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Program.ResultPanel.Visibility = Visibility.Hidden;
        }

        private void waitCheckbox_Click(object sender, RoutedEventArgs e)
        {
            if (Program.IsTestting) return;
            DependencyObject dep = (DependencyObject)e.OriginalSource;
            while ((dep != null) && !(dep is DataGridColumnHeader))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep == null)
                return;
            if (dep is DataGridColumnHeader && (sender as CheckBox).IsChecked == true)
            {
                DataGridColumnHeader columnHeader = dep as DataGridColumnHeader;
                columnHeader.Background = new SolidColorBrush(Color.FromRgb(198, 198, 198));
                Console.WriteLine(columnHeader.Content);
                string columnEnable = columnHeader.Content.ToString();
                switch (columnEnable)
                {
                    case "A":
                        if (Program.Boards.Count >= 1) Program.Boards[0].UserSkip = false;
                        break;

                    case "B":
                        if (Program.Boards.Count >= 2) Program.Boards[1].UserSkip = false;
                        break;

                    case "C":
                        if (Program.Boards.Count >= 3) Program.Boards[2].UserSkip = false;
                        break;

                    case "D":
                        if (Program.Boards.Count >= 4) Program.Boards[3].UserSkip = false;
                        break;

                    default:
                        break;
                }
            }

            if (dep is DataGridColumnHeader && (sender as CheckBox).IsChecked == false)
            {
                DataGridColumnHeader columnHeader = dep as DataGridColumnHeader;
                columnHeader.Background = new SolidColorBrush(Color.FromRgb(182, 182, 182));
                Console.WriteLine(columnHeader.Content);
                string columnEnable = columnHeader.Content.ToString();
                switch (columnEnable)
                {
                    case "A":
                        if (Program.Boards.Count >= 1) Program.Boards[0].UserSkip = true;
                        break;

                    case "B":
                        if (Program.Boards.Count >= 2) Program.Boards[1].UserSkip = true;
                        break;

                    case "C":
                        if (Program.Boards.Count >= 3) Program.Boards[2].UserSkip = true;
                        break;

                    case "D":
                        if (Program.Boards.Count >= 4) Program.Boards[3].UserSkip = true;
                        break;

                    default:
                        break;
                }
            }
        }

        private void Faking_Scan_Click(object sender, RoutedEventArgs e)
        {
            program.Boards.Clear();
            Board fake = new Board();
            fake.Barcode = "7KX92LZ1DQ";
            fake.SiteName = "A";
            fake.BarcodeNext = "9FG92LZ1DW";

            program.Boards.Add(fake);
            Board fake2 = new Board();

            fake2.Barcode = "9FG92LZ1DW";
            fake2.SiteName = "A";
            program.Boards.Add(fake2);

            //dgrBarcode.ItemsSource = 
        }

        private void Faking_Signal_ON_ReedSw_Click(object sender, RoutedEventArgs e)
        {
            program.System.System_Board.MachineIO.SS_DOWN = true;
            //Task.Delay(1000).Wait();
            //program.System.System_Board.MachineIO.Reset_SS_DOWN();
        }
        private void Faking_Signal_SSDOWN_False_Click(object sender, RoutedEventArgs e)
        {
            //program.System.System_Board.MachineIO.SS_DOWN = false;

            //Task.Delay(1000).Wait();
            program.System.System_Board.MachineIO.SS_DOWN = false;
        }
    }
}