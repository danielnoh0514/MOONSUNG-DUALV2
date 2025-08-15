using HVT.VTM.Base;
using HVT.Utility;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using HVT.Controls;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using OpenCvSharp;
using System.Runtime.Remoting.Channels;
using Camera;
using System.Windows.Media.Media3D;
using System.Windows.Documents;
using Newtonsoft.Json.Linq;
using System.Reflection;
using static HVT.Controls.DMM;
using System.Text;
using static OpenCvSharp.ML.DTrees;
using HVT.Controls.DevicesControl;
using System.Linq.Expressions;
using System.CodeDom.Compiler;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using OpenCvSharp.Flann;
using System.Drawing;
using static HVT.Controls.DeviceControl.SevenSegment;
using System.Data.SqlClient;
using LiveCharts;
using LiveCharts.Wpf;
using System.Globalization;

namespace HVT.VTM.Program
{
    public partial class Program
    {
        public bool IsloadModel = false;

        private Model testModel = new Model();

        public Model TestModel
        {
            get { return testModel; }
            set
            {
                TestState = RunTestState.STOP;
                if (value != testModel)
                {
                    testModel = value;
                    SetBoards();
                    IsloadModel = true;
                    Debug.Appent("New model loaded: " + value.Path, Debug.ContentType.Notify);
                    Debug.Appent("\tUsing barcode input: " + (testModel.BarcodeOption.UseBarcodeInput ? "YES" : "NO"), testModel.BarcodeOption.UseBarcodeInput ? Debug.ContentType.Notify : Debug.ContentType.Warning);
                    Debug.Appent("\tDischarge before test: " + (testModel.Discharge.CheckBeforeTest ? "YES" : "NO"), testModel.Discharge.CheckBeforeTest ? Debug.ContentType.Notify : Debug.ContentType.Warning);
                }
                TestState = RunTestState.WAIT;
            }
        }

        public FolderMap AppFolder = new FolderMap();

        public event EventHandler StepTestChange;

        public event EventHandler AutoPageStepTestChange;

        public event EventHandler TestRunFinish;

        public event EventHandler StateChange;

        public event EventHandler EscapTimeChange;

        public event EventHandler TesttingStateChange;

        private bool _IsTestting;

        public bool IsTestting
        {
            get { return _IsTestting; }
            set
            {
                if (value != _IsTestting)
                {
                    _IsTestting = value;
                    TesttingStateChange?.Invoke(value, null);
                }
            }
        }

        public int StepTesting = 0;

        private int FailReTestStep = 0;

        private double _EscapTime;

        public double EscapTime
        {
            get { return _EscapTime; }
            set
            {
                _EscapTime = value;
                EscapTimeChange?.Invoke(_EscapTime, null);
            }
        }

        private System.Timers.Timer EscapTimer = new System.Timers.Timer()
        {
            Interval = 100,
            Enabled = true,
        };

        public enum RunTestState
        {
            WAIT,
            READY,
            TESTTING,
            MANUALTEST,
            STOP,
            PAUSE,
            GOOD,
            FAIL,
            BUSY,
            OK_TESTING,
            DONE

        }

        private RunTestState testState;

        public RunTestState TestState
        {
            get { return testState; }
            set
            {
                if (value != testState)
                {
                    testState = value;
                    StateChange?.Invoke(value, null);
                    switch (testState)
                    {
                        case RunTestState.WAIT:
                            if (TestModel.BarcodeOption.UseBarcodeInput)
                            {
                                Debug.Write("Waiting barcode", Debug.ContentType.Warning);
                            }
                            EscapTimer.Stop();
                            EscapTime = 0;
                            break;

                        case RunTestState.READY:
                            Debug.Write("Ready", Debug.ContentType.Notify, 15);
                            EscapTimer.Stop();
                            break;

                        case RunTestState.TESTTING:
                            Debug.Write("Test start", Debug.ContentType.Warning, 20);
                            EscapTime = 0;
                            EscapTimer.Start();
                            break;

                        case RunTestState.MANUALTEST:
                            EscapTimer.Stop();
                            EscapTime = 0;
                            break;

                        case RunTestState.STOP:
                            EscapTimer.Stop();
                            EscapTime = 0;
                            break;

                        case RunTestState.PAUSE:
                            break;

                        case RunTestState.GOOD:
                            Debug.Write("Test done: GOOD", Debug.ContentType.Notify, 15);
                            EscapTimer.Stop();
                            break;

                        case RunTestState.FAIL:
                            Debug.Write("Test done: FAIL", Debug.ContentType.Error, 15);
                            EscapTimer.Stop();
                            EscapTime = 0;
                            break;

                        case RunTestState.BUSY:
                            EscapTimer.Stop();
                            EscapTime = 0;
                            break;

                        case RunTestState.DONE:
                            EscapTimer.Stop();
                            EscapTime = 0;
                            break;

                        default:
                            EscapTimer.Stop();
                            EscapTime = 0;
                            break;
                    }
                }
            }
        }

        public enum PageActive
        {
            AutoPage,
            ManualPage,
            ModelPage,
            VistionPage
        }

        public PageActive pageActive;

        public async void START()
        {
            TestState = RunTestState.BUSY;
            await Task.Run(ProgramState);
        }

        private void SetWaitState()
        {
            IsTestting = false;
            TestState = RunTestState.WAIT;
        }

        private async Task ExecuteResetSequence()
        {
            Relay.Card.Release();
            Solenoid.Card.Release();
            MuxCard.Card.ReleaseChannels();

            System.System_Board.MachineIO.ADSC = true;
            System.System_Board.MachineIO.BDSC = true;
            System.System_Board.MachineIO.LPG = true;
            System.System_Board.MachineIO.LPY = false;
            System.System_Board.SendControl();

            await Task.Delay(1000);

            System.System_Board.MachineIO.ADSC = false;
            System.System_Board.MachineIO.BDSC = false;

            System.System_Board.SendControl();

            System.System_Board.MachineIO.MainUP = true;
            System.System_Board.SendControl();

            await Task.Delay(1000);

            foreach (var item in Boards)
            {
                item.Barcode = "";
                item.Skip = item.UserSkip;
            }
        }

        private async Task ProgramState()
        {
         
            while (true)
            {
                bool isMain = AppSetting.Communication.Network.IsMain;
                bool useNetwork = AppSetting.Communication.Network.Use;
                MachineStatus statusPartner = isMain ? AppSetting.Communication.Network.StatusSub : AppSetting.Communication.Network.StatusMain;

                switch (TestState)
                {
                    case RunTestState.WAIT:
                        // Wait for input Barcode
                        if (TestModel.BarcodeOption.UseBarcodeInput)
                        {
                            foreach (var item in Boards)
                            {
                                item.Skip = item.UserSkip;
                            }
                            bool BarcodeReady = true;
                            if (Boards.Count >= 1) if (!Boards[0].Skip) BarcodeReady &= Boards[0].BarcodeReady;
                            if (Boards.Count >= 2) if (!Boards[1].Skip) BarcodeReady &= Boards[1].BarcodeReady;
                            if (Boards.Count >= 3) if (!Boards[2].Skip) BarcodeReady &= Boards[2].BarcodeReady;
                            if (Boards.Count >= 4) if (!Boards[3].Skip) BarcodeReady &= Boards[3].BarcodeReady;

                            // Ready
                            if (BarcodeReady)
                            {
                                System.System_Board.MachineIO.BUZZER = false;
                                System.System_Board.MachineIO.MainDOWN = false;
                                System.System_Board.MachineIO.MainUP = true;
                                System.System_Board.PowerRelease();
                                Relay.Card.Release();
                                Solenoid.Card.Release();
                                MuxCard.Card.ReleaseChannels();
                                TestState = RunTestState.READY;
                            }
                            else if (IsTestting)
                            {
                                IsTestting = false;
                                Debug.Write("No barcode.", Debug.ContentType.Error, 30);
                                await Task.Delay(500);
                                System.System_Board.MachineIO.BUZZER = false;
                                System.System_Board.MachineIO.MainUP = true;
                                System.System_Board.SendControl();
                            }
                        }
                        else
                        {
                            System.System_Board.PowerRelease();
                            Relay.Card.Release();
                            Solenoid.Card.Release();
                            MuxCard.Card.ReleaseChannels();
                            TestState = RunTestState.READY;
                        }
                        break;


                    case RunTestState.READY:
                        if (IsTestting)
                        {
                            if (TestModel.BarcodeOption.UseBarcodeInput)
                            {
                                bool BarcodeReady = true;
                                if (Boards.Count >= 1) if (!Boards[0].Skip) BarcodeReady &= Boards[0].BarcodeReady;
                                if (Boards.Count >= 2) if (!Boards[1].Skip) BarcodeReady &= Boards[1].BarcodeReady;
                                if (Boards.Count >= 3) if (!Boards[2].Skip) BarcodeReady &= Boards[2].BarcodeReady;
                                if (Boards.Count >= 4) if (!Boards[3].Skip) BarcodeReady &= Boards[3].BarcodeReady;

                                if (BarcodeReady)
                                {
                                    if (!useNetwork || statusPartner == MachineStatus.READY)
                                    {
                                        // Either network isn't used, or partner is ready
                                        TestState = RunTestState.TESTTING;
                                        System.System_Board.MachineIO.BUZZER = false;
                                        System.System_Board.MachineIO.LPY = true;
                                        System.System_Board.SendControl();
                                    }
                                }
                                else
                                {
                                    SetWaitState();
                                }
                            }
                            else
                            {
                                // no barcode input

                                TestState = RunTestState.TESTTING;
                                System.System_Board.MachineIO.BUZZER = false;
                                System.System_Board.MachineIO.LPY = true;
                                System.System_Board.SendControl();

                            }
                        }
                        else if (AppSetting.Operations.UseRetryUpdown)
                        {
                            System.System_Board.MachineIO.BUZZER = false;
                            System.System_Board.MachineIO.MainUP = true;
                            System.System_Board.SendControl();
                            await Task.Delay(AppSetting.Operations.TestPressUpTime);
                            System.System_Board.MachineIO.MainDOWN = true;
                            System.System_Board.SendControl();
                            await Task.Delay(AppSetting.Operations.TestPressUpTime + 2000);
                        }
                        break;

                    case RunTestState.TESTTING:

                        foreach (var item in Boards)
                        {
                            item.BoardDetail = "";
                            item.Skip = item.UserSkip;
                        }
                        //Delay before start
                        System.System_Board.PowerRelease();
                        Relay.Card.Release();
                        Solenoid.Card.Release();
                        MuxCard.Card.ReleaseChannels();
                        await Task.Delay(AppSetting.Operations.StartDelaytime);

                        // Cleaning steps and set start parametter to boards
                        TestModel.CleanSteps();
                        IsTestting = true;
                        StepTesting = 0;
                        var Steps = TestModel.Steps;
                        if (Boards.Count >= 1) if (!Boards[0].Skip) Boards[0].StartTest = DateTime.Now;
                        if (Boards.Count >= 2) if (!Boards[1].Skip) Boards[1].StartTest = DateTime.Now;
                        if (Boards.Count >= 3) if (!Boards[2].Skip) Boards[2].StartTest = DateTime.Now;
                        if (Boards.Count >= 4) if (!Boards[3].Skip) Boards[3].StartTest = DateTime.Now;

                        //Discharge
                        if (TestModel.Discharge.CheckBeforeTest || AppSetting.ETCSetting.UseDischargeTestStart)
                        {
                            if (!DisCharge() && AppSetting.ETCSetting.UseDischargeError)
                            {
                                TestState = RunTestState.FAIL;
                                IsTestting = false;
                            }
                        }
                        //Start Test
                        while (IsTestting)
                        {
                            //Test done without END command
                            if (StepTesting >= Steps.Count)
                            {
                                bool TestOK = true;
                                if (Boards.Count >= 1) { Boards[0].Result = Boards[0].UserSkip ? "SKIP" : Steps.Select(x => x.Result1).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                if (Boards.Count >= 2) { Boards[1].Result = Boards[1].UserSkip ? "SKIP" : Steps.Select(x => x.Result2).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                if (Boards.Count >= 3) { Boards[2].Result = Boards[2].UserSkip ? "SKIP" : Steps.Select(x => x.Result3).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                if (Boards.Count >= 4) { Boards[3].Result = Boards[3].UserSkip ? "SKIP" : Steps.Select(x => x.Result4).Contains(Step.Ng) ? "FAIL" : "OK"; }

                                foreach (var item in Boards)
                                {
                                    if (!item.UserSkip)
                                    {
                                        item.EndTest = DateTime.Now;
                                    }
                                }

                                System.System_Board.MachineIO.ADSC = true;
                                System.System_Board.MachineIO.BDSC = true;
                                System.System_Board.MachineIO.MainUP = true;
                                System.System_Board.MachineIO.LPG = true;

                                System.System_Board.SendControl();
                                TestOK = Boards.Select(x => x.Result).Contains("FAIL");
                                TestState = TestOK ? RunTestState.FAIL : RunTestState.GOOD;
                                ResultPanel.ShowResult(Boards.ToList());
                                IsTestting = false;
                                break;
                            }
                            else
                            {
                                var stepTest = Steps[StepTesting];
                                if (stepTest != null)
                                {
                                    StepTestChange?.Invoke(StepTesting, null);
                                    if (stepTest.cmd != CMDs.NON && Steps[StepTesting].cmd != CMDs.END && !stepTest.Skip)
                                    {
                                        bool IsPass = RUN_FUNCTION_TEST(stepTest);

                                        //Test pass and ejump
                                        if (!IsPass && stepTest.E_Jump != 0)
                                        {
                                            FailReTestStep = stepTest.E_Jump - 1;
                                            int StepResetErr = stepTest.No - 1;
                                            for (int i = 0; i < AppSetting.Operations.ErrorJumpCount; i++)
                                            {
                                                for (int stepRetest = FailReTestStep; stepRetest <= StepResetErr; stepRetest++)
                                                {
                                                    StepTesting = stepRetest;
                                                    StepTestChange?.Invoke(StepTesting, null);
                                                    stepTest = Steps[stepRetest];
                                                    IsPass = RUN_FUNCTION_TEST(stepTest);
                                                    if (IsPass && stepRetest == StepResetErr)
                                                    {
                                                        break;
                                                    }
                                                    if (!IsTestting || TestState != RunTestState.TESTTING)
                                                    {
                                                        break;
                                                    }
                                                    await Task.Delay(100);
                                                }
                                                if (IsPass)
                                                {
                                                    break;
                                                }
                                                if (!IsTestting || TestState != RunTestState.TESTTING)
                                                {
                                                    break;
                                                }
                                            }
                                        }

                                        //Stop with res test fail
                                        if (AppSetting.Operations.FailResistanceStopAll && stepTest.cmd == CMDs.RES && !IsPass)
                                        {
                                            Debug.Write("RES step test fail -> Fail RES stop all enable -> Force stop all", Debug.ContentType.Error);
                                            IsTestting = false;
                                            TestState = RunTestState.STOP;
                                        }

                                        //Stop with stop all when fail
                                        if (AppSetting.Operations.FailStopAll && !IsPass)
                                        {
                                            Debug.Write("Step test fail -> Fail stop all enable -> Force stop all", Debug.ContentType.Error);
                                            IsTestting = false;
                                            TestState = RunTestState.FAIL;
                                        }

                                        //Skip fail Step
                                        if (AppSetting.Operations.FailStopPCB && !IsPass)
                                        {
                                            if (Boards.Count >= 1) { Boards[0].Result = Boards[0].Skip ? "SKIP" : Steps.Select(x => x.Result1).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                            if (Boards.Count >= 2) { Boards[1].Result = Boards[1].Skip ? "SKIP" : Steps.Select(x => x.Result2).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                            if (Boards.Count >= 3) { Boards[2].Result = Boards[2].Skip ? "SKIP" : Steps.Select(x => x.Result3).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                            if (Boards.Count >= 4) { Boards[3].Result = Boards[3].Skip ? "SKIP" : Steps.Select(x => x.Result4).Contains(Step.Ng) ? "FAIL" : "OK"; }

                                            bool TestOK = true;

                                            if (Boards.Count >= 1) { Boards[0].Result = Boards[0].UserSkip ? "SKIP" : Steps.Select(x => x.Result1).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                            if (Boards.Count >= 2) { Boards[1].Result = Boards[1].UserSkip ? "SKIP" : Steps.Select(x => x.Result2).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                            if (Boards.Count >= 3) { Boards[2].Result = Boards[2].UserSkip ? "SKIP" : Steps.Select(x => x.Result3).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                            if (Boards.Count >= 4) { Boards[3].Result = Boards[3].UserSkip ? "SKIP" : Steps.Select(x => x.Result4).Contains(Step.Ng) ? "FAIL" : "OK"; }

                                            TestOK = Boards.Select(x => x.Result).Contains("OK");
                                            if (!TestOK)
                                            {
                                                foreach (var item in Boards)
                                                {
                                                    if (!item.UserSkip)
                                                    {
                                                        item.EndTest = DateTime.Now;
                                                    }
                                                }

                                                System.System_Board.MachineIO.ADSC = false;
                                                System.System_Board.MachineIO.BDSC = false;
                                                System.System_Board.MachineIO.MainUP = false;
                                                System.System_Board.MachineIO.LPG = false;
                                                System.System_Board.SendControl();
                                                TestState = RunTestState.FAIL;
                                                ResultPanel.ShowResult(Boards.ToList());
                                                IsTestting = false;
                                                break;
                                            }
                                            else
                                            {
                                                if (Boards.Count >= 1) if (!Boards[0].Skip) Boards[0].Skip = Boards[0].Result == "FAIL";
                                                if (Boards.Count >= 2) if (!Boards[1].Skip) Boards[1].Skip = Boards[1].Result == "FAIL";
                                                if (Boards.Count >= 3) if (!Boards[2].Skip) Boards[2].Skip = Boards[2].Result == "FAIL";
                                                if (Boards.Count >= 4) if (!Boards[3].Skip) Boards[3].Skip = Boards[3].Result == "FAIL";

                                                Debug.Write("Step test fail -> Skip sites:", Debug.ContentType.Warning);
                                                if (Boards.Count >= 1) if (Boards[0].Skip) Debug.Appent("\t\tSite A", Debug.ContentType.Warning);
                                                if (Boards.Count >= 2) if (Boards[1].Skip) Debug.Appent("\t\tSite B", Debug.ContentType.Warning);
                                                if (Boards.Count >= 3) if (Boards[2].Skip) Debug.Appent("\t\tSite C", Debug.ContentType.Warning);
                                                if (Boards.Count >= 4) if (Boards[3].Skip) Debug.Appent("\t\tSite D", Debug.ContentType.Warning);
                                            }
                                        }
                                    }

                                    if (stepTest.cmd == CMDs.END)
                                    {
                                        bool TestOK = true;
                                        if (Boards.Count >= 1) { Boards[0].Result = Boards[0].UserSkip ? "SKIP" : Steps.Select(x => x.Result1).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                        if (Boards.Count >= 2) { Boards[1].Result = Boards[1].UserSkip ? "SKIP" : Steps.Select(x => x.Result2).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                        if (Boards.Count >= 3) { Boards[2].Result = Boards[2].UserSkip ? "SKIP" : Steps.Select(x => x.Result3).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                        if (Boards.Count >= 4) { Boards[3].Result = Boards[3].UserSkip ? "SKIP" : Steps.Select(x => x.Result4).Contains(Step.Ng) ? "FAIL" : "OK"; }

                                        foreach (var item in Boards)
                                        {
                                            if (!item.UserSkip)
                                            {
                                                item.EndTest = DateTime.Now;
                                            }
                                        }

                                        System.System_Board.MachineIO.ADSC = true;
                                        System.System_Board.MachineIO.BDSC = true;

                                        System.System_Board.SendControl();
                                        TestOK = Boards.Select(x => x.Result).Contains("FAIL");
                                        TestState = TestOK ? RunTestState.FAIL : RunTestState.GOOD;
                                        ResultPanel.ShowResult(Boards.ToList());
                                        IsTestting = false;
                                        break;
                                    }

                                    if (stepTest.cmd == CMDs.UCN)
                                    {
                                        if (Boards.Count >= 1) if (!Boards[0].Skip && CommandDescriptions.CommandRemark_Version.Contains(stepTest.Remark)) Boards[0].BoardDetail += stepTest.Remark + ": " + stepTest.ValueGet1 + " ";
                                        if (Boards.Count >= 2) if (!Boards[1].Skip && CommandDescriptions.CommandRemark_Version.Contains(stepTest.Remark)) Boards[1].BoardDetail += stepTest.Remark + ": " + stepTest.ValueGet2 + " ";
                                        if (Boards.Count >= 3) if (!Boards[2].Skip && CommandDescriptions.CommandRemark_Version.Contains(stepTest.Remark)) Boards[2].BoardDetail += stepTest.Remark + ": " + stepTest.ValueGet3 + " ";
                                        if (Boards.Count >= 4) if (!Boards[3].Skip && CommandDescriptions.CommandRemark_Version.Contains(stepTest.Remark)) Boards[3].BoardDetail += stepTest.Remark + ": " + stepTest.ValueGet4 + " ";
                                    }

                                    await Task.Delay(10); // delay for data binding
                                }
                                StepTesting++;
                            }
                        }
                        break;

                    case RunTestState.MANUALTEST:
                        StepTesting = 0;
                        Steps = TestModel.Steps;
                        foreach (var item in Boards)
                        {
                            item.Skip = item.UserSkip;
                        }
                        //Start Test
                        while (IsTestting)
                        {
                            while (TestState == RunTestState.PAUSE)
                            {
                                await Task.Delay(500);
                            }

                            //Test done without END command
                            if (StepTesting >= Steps.Count)
                            {
                                System.System_Board.MachineIO.ADSC = true;
                                System.System_Board.MachineIO.BDSC = true;
                                System.System_Board.SendControl();
                                await Task.Delay(1000);

                                System.System_Board.MachineIO.ADSC = false;
                                System.System_Board.MachineIO.BDSC = false;
                                System.System_Board.SendControl();
                                IsTestting = false;
                                TestRunFinish?.Invoke(null, null);
                                break;
                            }
                            else
                            {
                                var lastStep = StepTesting;
                                var stepTest = Steps[StepTesting];
                                if (stepTest != null)
                                {
                                    StepTestChange?.Invoke(StepTesting, null);
                                    if (stepTest.cmd != CMDs.NON && !stepTest.Skip)
                                    {
                                        bool IsPass = RUN_FUNCTION_TEST(stepTest);

                                        //Test pass and ejump
                                        if (!IsPass && stepTest.E_Jump != 0)
                                        {
                                            FailReTestStep = stepTest.E_Jump - 1;
                                            int StepResetErr = stepTest.No - 1;
                                            for (int i = 0; i < AppSetting.Operations.ErrorJumpCount; i++)
                                            {
                                                for (int stepRetest = FailReTestStep; stepRetest <= StepResetErr; stepRetest++)
                                                {
                                                    while (TestState == RunTestState.PAUSE)
                                                    {
                                                        await Task.Delay(500);
                                                    }

                                                    StepTesting = stepRetest;
                                                    StepTestChange?.Invoke(StepTesting, null);
                                                    stepTest = Steps[stepRetest];
                                                    IsPass = RUN_FUNCTION_TEST(stepTest);
                                                    if (IsPass && stepRetest == StepResetErr)
                                                    {
                                                        StepTesting = lastStep;
                                                        break;
                                                    }
                                                    if (!IsTestting)
                                                    {
                                                        StepTesting = lastStep;
                                                        break;
                                                    }
                                                    await Task.Delay(100);
                                                }
                                                if (!IsTestting)
                                                {
                                                    StepTesting = lastStep;
                                                    break;
                                                }
                                                StepTesting = lastStep;
                                            }
                                        }

                                        //Stop with res test fail
                                        if (AppSetting.Operations.FailResistanceStopAll && stepTest.cmd == CMDs.RES && !IsPass)
                                        {
                                            Debug.Write("RES step test fail -> Fail RES stop all enable -> Force stop all", Debug.ContentType.Error);
                                            IsTestting = false;
                                            TestState = RunTestState.STOP;
                                        }

                                        //Stop with stop all when fail
                                        if (AppSetting.Operations.FailStopAll && !IsPass)
                                        {
                                            Debug.Write("Step test fail -> Fail stop all enable -> Force stop all", Debug.ContentType.Error);
                                            IsTestting = false;
                                            TestState = RunTestState.FAIL;
                                        }

                                        //Skip fail Step
                                        if (AppSetting.Operations.FailStopPCB && !IsPass)
                                        {
                                            if (Boards.Count >= 1) { Boards[0].Result = Boards[0].Skip ? "SKIP" : Steps.Select(x => x.Result1).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                            if (Boards.Count >= 2) { Boards[1].Result = Boards[1].Skip ? "SKIP" : Steps.Select(x => x.Result2).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                            if (Boards.Count >= 3) { Boards[2].Result = Boards[2].Skip ? "SKIP" : Steps.Select(x => x.Result3).Contains(Step.Ng) ? "FAIL" : "OK"; }
                                            if (Boards.Count >= 4) { Boards[3].Result = Boards[3].Skip ? "SKIP" : Steps.Select(x => x.Result4).Contains(Step.Ng) ? "FAIL" : "OK"; }

                                            if (Boards.Count >= 1) if (!Boards[0].Skip) Boards[0].Skip = Boards[0].Result != "OK";
                                            if (Boards.Count >= 2) if (!Boards[1].Skip) Boards[1].Skip = Boards[1].Result != "OK";
                                            if (Boards.Count >= 3) if (!Boards[2].Skip) Boards[2].Skip = Boards[2].Result != "OK";
                                            if (Boards.Count >= 4) if (!Boards[3].Skip) Boards[3].Skip = Boards[3].Result != "OK";
                                            Debug.Write("Step test fail -> Skip sites:", Debug.ContentType.Warning);
                                            if (Boards.Count >= 1) if (Boards[0].Skip) Debug.Appent("\t\tSite A", Debug.ContentType.Warning);
                                            if (Boards.Count >= 2) if (Boards[1].Skip) Debug.Appent("\t\tSite B", Debug.ContentType.Warning);
                                            if (Boards.Count >= 3) if (Boards[2].Skip) Debug.Appent("\t\tSite C", Debug.ContentType.Warning);
                                            if (Boards.Count >= 4) if (Boards[3].Skip) Debug.Appent("\t\tSite D", Debug.ContentType.Warning);
                                        }

                                        if (!IsTestting)
                                        {
                                            StepTesting = lastStep;
                                            break;
                                        }
                                    }

                                    if (stepTest.cmd == CMDs.END)
                                    {
                                        System.System_Board.MachineIO.ADSC = true;
                                        System.System_Board.MachineIO.BDSC = true;
                                        System.System_Board.SendControl();

                                        await Task.Delay(1000);

                                        System.System_Board.MachineIO.ADSC = false;
                                        System.System_Board.MachineIO.BDSC = false;
                                        System.System_Board.SendControl();
                                        IsTestting = false;
                                        TestRunFinish?.Invoke(null, null);
                                        break;
                                    }

                                    await Task.Delay(10); // delay for data binding
                                }
                                StepTesting++;
                            }
                        }
                        break;

                    case RunTestState.PAUSE:
                        await Task.Delay(100);
                        break;

                    case RunTestState.STOP:

                        IsTestting = false;
                        StepTesting = 0;
                        StepTestChange?.Invoke(StepTesting, null);
                        System.System_Board.MachineIO.ADSC = true;
                        System.System_Board.MachineIO.BDSC = true;
                        System.System_Board.MachineIO.LPR = true;
                        System.System_Board.SendControl();
                        await Task.Delay(2000);
                        System.System_Board.MachineIO.ADSC = false;
                        System.System_Board.MachineIO.BDSC = false;
                        System.System_Board.MachineIO.MainUP = false;
                        System.System_Board.MachineIO.LPR = true;
                        System.System_Board.SendControl();
                        await Task.Delay(1000);

                        if (TestModel.BarcodeOption.UseBarcodeInput)
                        {
                            TestState = RunTestState.WAIT;
                        }
                        else
                        {
                            TestState = RunTestState.READY;
                        }
                        foreach (var item in Boards)
                        {
                            item.Barcode = "";
                            item.Result = "";
                            item.Skip = item.UserSkip;
                        }
                        break;

                    case RunTestState.GOOD:
                        
                        TestRunFinish?.Invoke("", null);
                        
                        if (!AppSetting.Communication.MainPc)
                        {
                            System.System_Board.SendOkToMain();
                        }

                        if (Boards.Count >= 1)
                        {
                            if (!Boards[0].Skip && Boards[0].Result == "OK")
                            {
                                if (TestModel.BarcodeOption.UseBarcodeInput)
                                {
                                    //Printer.GT800.SendStringToPrinter(Printer.QRcode.GenerateCode("A", Boards[0].Barcode, Boards[0].StartTest, Boards[0].EndTest,
                                    //    TestModel.Steps.Select(x => x.IMQSCode).ToList(), TestModel.Steps.Select(x => x.Min).ToList(), TestModel.Steps.Select(x => x.Max).ToList(),
                                    //    TestModel.Steps.Select(x => x.ValueGet1).ToList(), TestModel.Steps.Where(x => x.Remark == "MAIN VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet1,
                                    //    TestModel.Steps.Where(x => x.Remark == "SUB VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet1, out string barcodeOut));
                                    //Boards[0].QRout = barcodeOut;
                                    //Debug.Appent("\t\tBoard A: GOOD - qr printed:" + Boards[0].QRout, Debug.ContentType.Notify);
                                }
                            }
                        }
                        if (Boards.Count >= 2)
                        {
                            if (!Boards[1].Skip && Boards[1].Result == "OK")
                            {
                                if (TestModel.BarcodeOption.UseBarcodeInput)
                                {
                                    //Printer.GT800.SendStringToPrinter(Printer.QRcode.GenerateCode("B", Boards[1].Barcode, Boards[1].StartTest, Boards[1].EndTest,
                                    //    TestModel.Steps.Select(x => x.IMQSCode).ToList(), TestModel.Steps.Select(x => x.Min).ToList(), TestModel.Steps.Select(x => x.Max).ToList(),
                                    //    TestModel.Steps.Select(x => x.ValueGet2).ToList(), TestModel.Steps.Where(x => x.Remark == "MAIN VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet2,
                                    //    TestModel.Steps.Where(x => x.Remark == "SUB VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet2, out string barcodeOut));
                                    //Boards[1].QRout = barcodeOut;
                                    //Debug.Appent("\t\tBoard B: GOOD - qr printed:" + Boards[1].QRout, Debug.ContentType.Notify);
                                }
                            }
                        }
                        if (Boards.Count >= 3)
                        {
                            if (!Boards[2].Skip && Boards[2].Result == "OK")
                            {
                                if (TestModel.BarcodeOption.UseBarcodeInput)
                                {
                                    //Printer.GT800.SendStringToPrinter(Printer.QRcode.GenerateCode("C", Boards[2].Barcode, Boards[2].StartTest, Boards[2].EndTest,
                                    //    TestModel.Steps.Select(x => x.IMQSCode).ToList(), TestModel.Steps.Select(x => x.Min).ToList(), TestModel.Steps.Select(x => x.Max).ToList(),
                                    //    TestModel.Steps.Select(x => x.ValueGet3).ToList(), TestModel.Steps.Where(x => x.Remark == "MAIN VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet3,
                                    //    TestModel.Steps.Where(x => x.Remark == "SUB VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet3, out string barcodeOut));
                                    //Boards[2].QRout = barcodeOut;
                                    //Debug.Appent("\t\tBoard C: GOOD - qr printed:" + Boards[2].QRout, Debug.ContentType.Notify);
                                }
                            }
                        }
                        if (Boards.Count >= 4)
                        {
                            if (!Boards[3].Skip && Boards[3].Result == "OK")
                            {
                                if (TestModel.BarcodeOption.UseBarcodeInput)
                                {
                                    //Printer.GT800.SendStringToPrinter(Printer.QRcode.GenerateCode("D", Boards[3].Barcode, Boards[3].StartTest, Boards[3].EndTest,
                                    //    TestModel.Steps.Select(x => x.IMQSCode).ToList(), TestModel.Steps.Select(x => x.Min).ToList(), TestModel.Steps.Select(x => x.Max).ToList(),
                                    //    TestModel.Steps.Select(x => x.ValueGet4).ToList(), TestModel.Steps.Where(x => x.Remark == "MAIN VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet4,
                                    //    TestModel.Steps.Where(x => x.Remark == "SUB VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet4, out string barcodeOut));
                                    //Boards[3].QRout = barcodeOut;
                                    //Debug.Appent("\t\tBoard D: GOOD - qr printed:" + Boards[3].QRout, Debug.ContentType.Notify);
                                }
                            }
                        }

                        foreach (var board in Boards)
                        {
                            if (!board.UserSkip)
                            {
                                board.TestStep = TestModel.Steps.ToList();
                                if (AppSetting.Operations.SaveFailPCB)
                                {
                                    bool is_final_result_fail = false;

                                    if (board.TestStep != null)
                                    {
                                        if (board.SiteName == "A")
                                        {
                                            is_final_result_fail = !board.TestStep.All(step => step.Result1 == Step.Ok || step.Skip == true);
                                        }
                                        if (board.SiteName == "B")
                                        {
                                            is_final_result_fail = !board.TestStep.All(step => step.Result2 == Step.Ok || step.Skip == true);
                                        }
                                    }
                                    AppFolder.SaveHistory(board);

                                    AppFolder.SaveLogFile(is_final_result_fail, board, StepTesting, true);
                                }
                                else
                                {
                                    if (board.Result == "OK")
                                    {
                                        AppFolder.SaveHistory(board);
                                    }
                                }
                            }
                        }


                        TestState = RunTestState.OK_TESTING;

                        break;

                    case RunTestState.OK_TESTING:

                        // Common condition for executing the reset sequence
                        //if (!useNetwork)
                        //{
                        //    await ExecuteResetSequence();
                        //    TestState = TestModel.BarcodeOption.UseBarcodeInput ? RunTestState.WAIT : RunTestState.READY;
                        //}

                        if (AppSetting.Communication.MainPc)
                        {
                            while (System.System_Board.MachineIO.SubOk || System.System_Board.MachineIO.SubNg)
                            {
                                await Task.Delay(100);
                            }

                            if (System.System_Board.MachineIO.SubOk)
                            {
                                await ExecuteResetSequence();

                            }
                        }


                        if (AppSetting.Communication.MainPc)
                        {
                            System.System_Board.MachineIO.SubOk = false;
                            System.System_Board.MachineIO.SubNg = false;
                        }

                        TestState = TestModel.BarcodeOption.UseBarcodeInput ? RunTestState.WAIT : RunTestState.READY;

                        break;       


                    case RunTestState.FAIL:
                        TestRunFinish?.Invoke("", null);

                        if (!AppSetting.Communication.MainPc)
                        {
                            System.System_Board.SendNgToMain();
                        }


                        if (Printer.QRcode.TestPCBPassPrint)
                        {
                            if (Boards.Count >= 1)
                            {
                                if (!Boards[0].Skip && Boards[0].Result == "OK")
                                {
                                    if (TestModel.BarcodeOption.UseBarcodeInput)
                                    {
                                        //Printer.GT800.SendStringToPrinter(Printer.QRcode.GenerateCode("A", Boards[0].Barcode, Boards[0].StartTest, Boards[0].EndTest,
                                        //    TestModel.Steps.Select(x => x.IMQSCode).ToList(), TestModel.Steps.Select(x => x.Min).ToList(), TestModel.Steps.Select(x => x.Max).ToList(),
                                        //    TestModel.Steps.Select(x => x.ValueGet1).ToList(), TestModel.Steps.Where(x => x.Remark == "MAIN VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet1,
                                        //    TestModel.Steps.Where(x => x.Remark == "SUB VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet1, out string barcodeOut));
                                        //Boards[0].QRout = barcodeOut;
                                        //Debug.Write("\t\tBoard A: GOOD - qr printed:" + Boards[0].QRout, Debug.ContentType.Notify);
                                    }
                                }
                                else
                                {
                                    Debug.Appent(String.Format("\t\tBoard A: {0}", Boards[0].Result), Debug.ContentType.Error);
                                }
                            }
                            if (Boards.Count >= 2)
                            {
                                if (!Boards[1].Skip && Boards[1].Result == "OK")
                                {
                                    if (TestModel.BarcodeOption.UseBarcodeInput)
                                    {
                                        //Printer.GT800.SendStringToPrinter(Printer.QRcode.GenerateCode("B", Boards[1].Barcode, Boards[1].StartTest, Boards[1].EndTest,
                                        //    TestModel.Steps.Select(x => x.IMQSCode).ToList(), TestModel.Steps.Select(x => x.Min).ToList(), TestModel.Steps.Select(x => x.Max).ToList(),
                                        //    TestModel.Steps.Select(x => x.ValueGet2).ToList(), TestModel.Steps.Where(x => x.Remark == "MAIN VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet2,
                                        //    TestModel.Steps.Where(x => x.Remark == "SUB VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet2, out string barcodeOut));
                                        //Boards[1].QRout = barcodeOut;
                                        //Debug.Appent("\t\tBoard B: GOOD - qr printed:" + Boards[1].QRout, Debug.ContentType.Notify);
                                    }
                                }
                                else
                                {
                                    Debug.Appent(String.Format("\t\tBoard B: {0}", Boards[1].Result), Debug.ContentType.Error);
                                }
                            }
                            if (Boards.Count >= 3)
                            {
                                if (!Boards[2].Skip && Boards[2].Result == "OK")
                                {
                                    if (TestModel.BarcodeOption.UseBarcodeInput)
                                    {
                                        //Printer.GT800.SendStringToPrinter(Printer.QRcode.GenerateCode("C", Boards[2].Barcode, Boards[2].StartTest, Boards[2].EndTest,
                                        //    TestModel.Steps.Select(x => x.IMQSCode).ToList(), TestModel.Steps.Select(x => x.Min).ToList(), TestModel.Steps.Select(x => x.Max).ToList(),
                                        //    TestModel.Steps.Select(x => x.ValueGet3).ToList(), TestModel.Steps.Where(x => x.Remark == "MAIN VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet3,
                                        //    TestModel.Steps.Where(x => x.Remark == "SUB VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet3, out string barcodeOut));
                                        //Boards[2].QRout = barcodeOut;
                                        //Debug.Appent("\t\tBoard C: GOOD - qr printed:" + Boards[2].QRout, Debug.ContentType.Notify);
                                    }
                                }
                                else
                                {
                                    Debug.Appent(String.Format("\t\tBoard C: {0}", Boards[2].Result), Debug.ContentType.Error);
                                }
                                if (Boards.Count >= 4)
                                {
                                    if (!Boards[3].Skip && Boards[3].Result == "OK")
                                    {
                                        if (TestModel.BarcodeOption.UseBarcodeInput)
                                        {
                                            //Printer.GT800.SendStringToPrinter(Printer.QRcode.GenerateCode("D", Boards[3].Barcode, Boards[3].StartTest, Boards[3].EndTest,
                                            //    TestModel.Steps.Select(x => x.IMQSCode).ToList(), TestModel.Steps.Select(x => x.Min).ToList(), TestModel.Steps.Select(x => x.Max).ToList(),
                                            //    TestModel.Steps.Select(x => x.ValueGet4).ToList(), TestModel.Steps.Where(x => x.Remark == "MAIN VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet4,
                                            //    TestModel.Steps.Where(x => x.Remark == "SUB VERSION").DefaultIfEmpty(new Step()).FirstOrDefault().ValueGet4, out string barcodeOut));
                                            //Boards[3].QRout = barcodeOut;
                                            //Debug.Appent("\t\tBoard D: GOOD - qr printed:" + Boards[3].QRout, Debug.ContentType.Notify);
                                        }
                                    }
                                    else
                                    {
                                        Debug.Appent(String.Format("\t\tBoard D: {0}", Boards[3].Result), Debug.ContentType.Error);
                                    }
                                }
                            }

                            foreach (var board in Boards)
                            {
                                if (!board.UserSkip)
                                {
                                    board.TestStep = TestModel.Steps.ToList();
                                    if (AppSetting.Operations.SaveFailPCB)
                                    {
                                        bool is_final_result_fail = true;

                                        if (board.TestStep != null)
                                        {
                                            if (board.SiteName == "A")
                                            {
                                                is_final_result_fail = board.TestStep.Any(step => step.Result1 != Step.Ok && step.Skip != true);
                                            }
                                            if (board.SiteName == "B")
                                            {
                                                is_final_result_fail = board.TestStep.Any(step => step.Result1 != Step.Ok && step.Skip != true);
                                            }
                                            if (board.SiteName == "C")
                                            {
                                                is_final_result_fail = board.TestStep.Any(step => step.Result3 != Step.Ok && step.Skip != true);
                                            }
                                            if (board.SiteName == "D")
                                            {
                                                is_final_result_fail = board.TestStep.Any(step => step.Result4 != Step.Ok && step.Skip != true);
                                            }
                                        }

                                        //AppFolder.SaveHistory(item);
                                        AppFolder.SaveLogFile(is_final_result_fail, board, StepTesting, false);
                                    }
                                    else
                                    {
                                        if (board.Result == "OK")
                                        {
                                            AppFolder.SaveHistory(board);
                                        }
                                    }
                                }
                            }
                        }

                        if (AppSetting.Communication.MainPc)
                        {
                            while (System.System_Board.MachineIO.SubOk || System.System_Board.MachineIO.SubNg)
                            {
                                await Task.Delay(100);
                            }
                        }

                        Relay.Card.Release();
                        Solenoid.Card.Release();
                        MuxCard.Card.ReleaseChannels();
                        System.System_Board.MachineIO.ADSC = false;
                        System.System_Board.MachineIO.BDSC = false;
                        System.System_Board.MachineIO.LPR = true;
                        System.System_Board.MachineIO.MainUP = false;
                        System.System_Board.MachineIO.BUZZER = true;
                        System.System_Board.MachineIO.AC220 = false;
                        System.System_Board.MachineIO.LPY = false;
                        System.System_Board.SendControl();
                        System.System_Board.MachineIO.ADSC = false;
                        System.System_Board.MachineIO.BDSC = false;
                        System.System_Board.MachineIO.MainUP = false;
                        System.System_Board.SendControl();
                        await Task.Delay(2000);
                        System.System_Board.MachineIO.BUZZER = false;
                        System.System_Board.SendControl();
                        foreach (var item in Boards)
                        {
                            item.Barcode = "";
                            item.Result = "";
                            item.Skip = item.UserSkip;
                        }


                        if (AppSetting.Communication.MainPc)
                        {
                            System.System_Board.MachineIO.SubOk = false;
                            System.System_Board.MachineIO.SubNg = false;
                        }

                        if (TestModel.BarcodeOption.UseBarcodeInput)
                        {
                            TestState = RunTestState.WAIT;
                        }
                        else
                        {
                            TestState = RunTestState.READY;
                        }
                        break;

                  

                    default:
                        break;
                }
                await Task.Delay(500);
            }
        }

        private void EscapTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            EscapTime += 0.1;
        }

        public void RUN_MANUAL_TEST()
        {
            if (!IsTestting)
            {
                TestState = RunTestState.MANUALTEST;
                IsTestting = true;
            }
        }

        public async void Run_Manual_Test()
        {
            if (TestModel.Steps.Count < 2)
            {
                return;
            }
            TestModel.CleanSteps();
            for (int i = 0; i < TestModel.Steps.Count; i++)
            {
                if (TestState == RunTestState.STOP)
                {
                    TestState = RunTestState.DONE;
                    break;
                }
                var stepTest = TestModel.Steps[i];
                if (stepTest != null)
                {
                    StepTesting = i;
                    if (stepTest.cmd != CMDs.NON || !stepTest.Skip)
                    {
                        StepTestChange?.Invoke(i, null);
                        RUN_FUNCTION_TEST(stepTest);
                        await Task.Delay(10); // delay for data binding
                    }
                }
                while (TestState == RunTestState.PAUSE)
                {
                    Task.Delay(100).Wait();
                }
            }
            TestState = RunTestState.DONE;
            TestRunFinish?.Invoke(null, null);
        }

        public void ResetTest()
        {
            TestState = RunTestState.WAIT;
            //SYSTEM.System_Board.MachineIO.ADSC = true;
            //SYSTEM.System_Board.MachineIO.BDSC = true;
            //SYSTEM.System_Board.MachineIO.ADSC = false;
            //SYSTEM.System_Board.MachineIO.BDSC = false;
            //SYSTEM.System_Board.SendControl();
            TestModel.CleanSteps();
            Relay.Card.Release();
            MuxCard.Card.ReleaseChannels();
            Solenoid.Card.Release();
        }

        public Step currentStep = new Step();

        public async void RunStep()
        {
            if (!FunctionTesting)
            {
                await Task.Run(RunFunctionsTest);
            }
        }

        private bool FunctionTesting = false;

        public async void RunFunctionsTest()
        {
            FunctionTesting = true;
            try
            {
                currentStep.ValueGet1 = "";
                currentStep.ValueGet2 = "";
                currentStep.ValueGet3 = "";
                currentStep.ValueGet4 = "";
                RUN_FUNCTION_TEST(currentStep);
            }
            catch (Exception err)
            {
                Debug.Write(string.Format("{0} : {1}", currentStep.TestContent, err.StackTrace), Debug.ContentType.Error);
            }
            await Task.Delay(10);
            FunctionTesting = false;
        }

        public bool RUN_FUNCTION_TEST(Step step)
        {
            step.Result1 = Step.DontCare;
            step.Result2 = Step.DontCare;
            step.Result3 = Step.DontCare;
            step.Result4 = Step.DontCare;

            step.ValueGet1 = "";
            step.ValueGet2 = "";
            step.ValueGet3 = "";
            step.ValueGet4 = "";

            bool isSkipAll = Boards.Where(x => x.Skip).Count() == Boards.Count;
            if (isSkipAll) return false;

            if (!step.Skip)

                switch (step.cmd)
                {
                    case CMDs.NON:
                        break;

                    case CMDs.PWR:
                        PWR(step);
                        break;

                    case CMDs.DLY:
                        DLY(step);
                        break;

                    case CMDs.GEN:
                        GEN(step);
                        break;

                    case CMDs.BUZ:
                        BUZ(step);
                        break;

                    case CMDs.RLY:
                        RLY_SYSTEM_BOARD(step);
                        //RLY_RELAY_BOARD(step);

                        Task.Delay(50).Wait();
                        break;

                    case CMDs.KEY:
                        KEY(step);
                        Task.Delay(50).Wait();
                        break;

                    case CMDs.MAK:
                        break;

                    case CMDs.DIS:
                        DIS(step);
                        break;

                    case CMDs.END:
                        END(step);
                        break;

                    case CMDs.ACV:
                        ACV(step);
                        break;

                    case CMDs.DCV:
                        DCV(step);
                        break;

                    case CMDs.FRQ:
                        FREQ(step);
                        break;

                    case CMDs.RES:
                        RES(step);
                        break;

                    case CMDs.URD:
                        //URD(step, PCB_SKIP_CHECK); update late
                        break;

                    case CMDs.UTN:
                        UTN(step);
                        break;

                    case CMDs.UTX:
                        UTX(step);
                        break;

                    case CMDs.UCN:
                        UCN(step);
                        break;

                    //case CMDs.UCP:
                    //    break;

                    case CMDs.STL:
                        STL(step);
                        break;

                    case CMDs.EDL:
                        EDL(step);
                        break;

                    case CMDs.LCC:
                        LCC(step);
                        break;

                    case CMDs.LEC:
                        LEC(step);
                        break;

                    case CMDs.LSQ:
                        LSQ(step);
                        break;

                    case CMDs.LTM:
                        LTM(step);
                        break;

                    case CMDs.CAL:
                        break;

                    case CMDs.GLED:
                        ReadGLED(step);
                        break;

                    case CMDs.FND:
                        ReadFND(step);
                        break;

                    case CMDs.LED:
                        ReadLED(step);
                        break;

                    case CMDs.LCD:
                        ReadLCD(step);
                        break;

                    case CMDs.PCB:
                        PCB(step);
                        break;

                    case CMDs.SEV:
                        SEV(step);
                        break;

                    case CMDs.CAM:
                        CAM(step);
                        Task.Delay(1000).Wait();
                        break;

                    case CMDs.MOT:
                        MOT(step);
                        break;

                    default:
                        break;
                }
            return StepTestResult(step);
        }

        #region Functions Code

        public void GEN(Step step)
        {
            if (!Double.TryParse(step.Condition1, out double frequency))
            {
                FunctionsParameterError("Condition", step);
                return;
            }

            List<string> Channels = step.Oper.Split('/').ToList();
            List<int> ChannelsInt = new List<int>();
            foreach (var Channel in Channels)
            {
                if (!Int32.TryParse(Channel, out int channel))
                {
                    FunctionsParameterError("Oper", step);
                    return;
                }
                else
                {
                    if (channel == 0 || channel > 4)
                    {
                        FunctionsParameterError("Oper", step);
                        return;
                    }
                    else
                    {
                        ChannelsInt.Add(channel);
                    }
                }
            }

            if (!System.System_Board.SerialPort.Port.IsOpen)
            {
                FunctionsParameterError("Sys", step);
                return;
            }

            if (!System.System_Board.GEN((int)frequency, ChannelsInt))
            {
                FunctionsParameterError("Sys", step);
            }
            else
            {
                if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = "exe";
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = "exe";
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = "exe";
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = "exe";
            }
        }

        public void BUZ(Step step)
        {
            if (Boards.Count > 2)
            {
                FunctionsParameterError("Site number", step);
                return;
            }
            if (!System.System_Board.SerialPort.Port.IsOpen)
            {
                FunctionsParameterError("Sys", step);
                return;
            }

            string function_type = step.Oper;

            if (!(function_type == "START" || function_type == "READ"))
            {
                FunctionsParameterError("Oper", step);
                return;
            }

            if (function_type == "START")
            {
                double sampling_rate;
                if (!Double.TryParse(step.Condition1, out sampling_rate))
                {
                    FunctionsParameterError("Condition1", step);
                    return;
                }

                System.StartRecordMic(sampling_rate);

                if (Boards.Count >= 1)
                {
                    step.ValueGet1 = "exe";
                    step.Result1 = Step.Ok;
                }
                if (Boards.Count >= 2)
                {
                    step.ValueGet1 = "exe";
                    step.Result2 = Step.Ok;
                }
            }
            else
            {
                System.StopRecordMic();

                double min;
                if (!Double.TryParse(step.Min, out min))
                {
                    if (step.Min == "")
                    {
                        min = 0;
                    }
                    else
                    {
                        FunctionsParameterError("Min", step);
                        return;
                    }
                }

                double max;
                if (!Double.TryParse(step.Max, out max))
                {
                    if (step.Max == "")
                    {
                        max = Double.MaxValue;
                    }
                    else
                    {
                        FunctionsParameterError("Max", step);
                        return;
                    }
                }

                if (Boards.Count >= 1)
                {
                    if (!Boards[0].Skip)
                    {
                        List<int> samples = System.System_Board.MachineIO.SamplesMicA;

                        step.ValueGet1 = samples.Max().ToString();
                        if (samples.Max() <= max & samples.Max() >= min)
                        {
                            step.Result1 = Step.Ok;
                        }
                        else
                        {
                            step.Result1 = Step.Ng;
                        }

                        return;
                    }
                }
                if (Boards.Count >= 2)
                {
                    if (!Boards[1].Skip)
                    {
                        List<int> samples = System.System_Board.MachineIO.SamplesMicB;

                        step.ValueGet2 = samples.Max().ToString();
                        if (samples.Max() <= max & samples.Max() >= min)
                        {
                            step.Result2 = Step.Ok;
                        }
                        else
                        {
                            step.Result2 = Step.Ng;
                        }
                        return;
                    }
                }
            }
        }

        public void SEV(Step step)
        {
            if (!BoardExtension1.SerialPort.Port.IsOpen)
            {
                switch (Boards.Count)
                {
                    case 1:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }
                        break;

                    case 2:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Sys";
                            step.Result3 = Step.Ng;
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Sys";
                            step.Result3 = Step.Ng;
                        }
                        if (!Boards[3].Skip)
                        {
                            step.ValueGet4 = "Sys";
                            step.Result4 = Step.Ng;
                        }
                        break;

                    default:
                        break;
                }
                return;
            }

            bool result = false;
            string stringCompare = string.Empty;

            double minValue = 0;
            double maxValue = 0;

            if (!Double.TryParse(step.Min, out minValue))
            {
                if (string.IsNullOrEmpty(step.Min))
                {
                    minValue = 0;
                }
                else
                {
                    step.ValueGet1 = "Min";

                    step.Result1 = Step.Ng;

                    return;
                }
            }

            if (!Double.TryParse(step.Max, out maxValue))
            {
                if (string.IsNullOrEmpty(step.Max))
                {
                    maxValue = Double.MaxValue;
                }
                else
                {
                    step.ValueGet1 = "Max";

                    step.Result1 = Step.Ng;

                    return;
                }
            }

            //if (!int.TryParse(step.Oper, out int Threshold))
            //{
            //    FunctionsParameterError("Oper", step);
            //    return;
            //}

            int threshold = 4;
            int delayTime = 0;

            if (step.Condition1 == "RPM")
            {
                delayTime = 50;
            }
            if (step.Condition1 == "String")
            {
                threshold = 6;
                delayTime = 150;
            }
            if (step.Condition1 == "Icon")
            {
                delayTime = 100;
            }
            if (step.Condition1 == "LED")
            {
                threshold = 100;
                delayTime = 100;
            }
            if (int.TryParse(step.Condition2, out int scanTime))
            {
                DateTime start = DateTime.Now;
                while (DateTime.Now.Subtract(start).TotalMilliseconds < scanTime)
                {
                    if (step.Condition1 == "String" || step.Condition1 == "RPM")
                    {
                        stringCompare = string.Empty;

                        #region Board 1

                        BoardExtension1.SevenSegment.SignalRead();
                        BoardExtension1.SevenSegment.Parse();

                        Console.WriteLine($"----------------------------------------------------------------------------------------------------");

                        #region Digit 0

                        StringBuilder binaryString_digit0 = new StringBuilder();
                        Console.WriteLine($"digit 0:");

                        for (int i = 0; i < 7; i++)
                        {
                            bool[] segment16bit = BoardExtension1.SevenSegment.Digit0.Skip(i * 16).Take(16).ToArray();
                            int analogValue = BoolListToDecimal(segment16bit);
                            Console.WriteLine($"segment {i}: {analogValue}");
                            binaryString_digit0.Append((analogValue >= threshold) ? "1" : "0");
                        }

                        SegementCharacter segchar_digit0 = FND.SEG_LOOKUP.Where(seg => seg.digitString == binaryString_digit0.ToString()).ToList().FirstOrDefault();
                        stringCompare = (segchar_digit0 == null ? "" : segchar_digit0.character.ToString()) + stringCompare;
                        // 01

                        #endregion Digit 0

                        Console.WriteLine($"----------------------------------------------------------------------------------------------------");

                        #region Digit 1

                        StringBuilder binaryString_digit1 = new StringBuilder();
                        Console.WriteLine($"digit 1:");

                        for (int i = 0; i < 7; i++)
                        {
                            bool[] segment16bit = BoardExtension1.SevenSegment.Digit1.Skip(i * 16).Take(16).ToArray();
                            int analogValue = BoolListToDecimal(segment16bit);
                            Console.WriteLine($"segment {i}: {analogValue}");
                            binaryString_digit1.Append((analogValue >= threshold) ? "1" : "0");
                        }

                        SegementCharacter segchar_digit1 = FND.SEG_LOOKUP.Where(seg => seg.digitString == binaryString_digit1.ToString()).ToList().FirstOrDefault();
                        stringCompare = (segchar_digit1 == null ? "" : segchar_digit1.character.ToString()) + stringCompare;

                        #endregion Digit 1

                        #endregion Board 1

                        #region Board 2

                        Console.WriteLine($"----------------------------------------------------------------------------------------------------");

                        BoardExtension2.SevenSegment.SignalRead();
                        BoardExtension2.SevenSegment.Parse();

                        #region Sign

                        StringBuilder binaryString_sign = new StringBuilder();
                        Console.WriteLine($"sign:");

                        for (int i = 0; i < 2; i++)
                        {
                            bool[] segment16bit = BoardExtension2.SevenSegment.Sign.Skip(i * 16).Take(16).ToArray();
                            int analogValue = BoolListToDecimal(segment16bit);
                            Console.WriteLine($"sign {i}: {analogValue}");
                            binaryString_sign.Append((analogValue >= threshold) ? "1" : "0");
                        }

                        if (binaryString_sign.ToString() == "11")
                        {
                            stringCompare = ":" + stringCompare;
                        }
                        if ((binaryString_sign.ToString() == "01") || (binaryString_sign.ToString() == "10"))
                        {
                            stringCompare = "." + stringCompare;
                        }

                        #endregion Sign

                        Console.WriteLine($"----------------------------------------------------------------------------------------------------");

                        #region Digit 2

                        StringBuilder binaryString_digit2 = new StringBuilder();
                        Console.WriteLine($"digit 2:");

                        for (int i = 0; i < 7; i++)
                        {
                            bool[] segment16bit = BoardExtension2.SevenSegment.Digit2.Skip(i * 16).Take(16).ToArray();
                            int analogValue = BoolListToDecimal(segment16bit);
                            Console.WriteLine($"segment {i}: {analogValue}");
                            binaryString_digit2.Append((analogValue >= threshold) ? "1" : "0");
                        }

                        SegementCharacter segchar_digit2 = FND.SEG_LOOKUP.Where(seg => seg.digitString == binaryString_digit2.ToString()).ToList().FirstOrDefault();
                        stringCompare = (segchar_digit2 == null ? "" : segchar_digit2.character.ToString()) + stringCompare;

                        #endregion Digit 2

                        Console.WriteLine($"----------------------------------------------------------------------------------------------------");

                        #region Digit 3

                        StringBuilder binaryString_digit3 = new StringBuilder();
                        Console.WriteLine($"digit 3:");

                        for (int i = 0; i < 2; i++)
                        {
                            bool[] segment16bit = BoardExtension2.SevenSegment.Digit3.Skip(i * 16).Take(16).ToArray();
                            int analogValue = BoolListToDecimal(segment16bit);
                            Console.WriteLine($"segment {i}: {analogValue}");
                            binaryString_digit3.Append((analogValue >= threshold) ? "1" : "0");
                        }

                        binaryString_digit3 = new StringBuilder("0" + binaryString_digit3.ToString() + "0000");

                        SegementCharacter segchar_digit3 = FND.SEG_LOOKUP.Where(seg => seg.digitString == binaryString_digit3.ToString()).ToList().FirstOrDefault();
                        stringCompare = (segchar_digit3 == null ? "" : segchar_digit3.character.ToString()) + stringCompare;

                        #endregion Digit 3

                        #endregion Board 2

                        if (step.Condition1 == "RPM")
                        {
                            int numberCompare = 0;
                            if (!string.IsNullOrEmpty(stringCompare))
                            {
                                try
                                {
                                    numberCompare = Int32.Parse(stringCompare);
                                }
                                catch
                                {
                                    numberCompare = 0;
                                }
                            }
                            result = numberCompare <= maxValue && numberCompare >= minValue;
                        }

                        if (step.Condition1 == "String")
                        {
                            result = stringCompare.Equals(step.Spect);
                        }

                        step.ValueGet1 = stringCompare;

                        if (result)
                        {
                            break;
                        }
                    }
                    if (step.Condition1 == "Icon")
                    {
                        stringCompare = string.Empty;

                        BoardExtension2.SevenSegment.SignalRead();
                        BoardExtension2.SevenSegment.Parse();

                        #region Icon

                        StringBuilder binaryString_icon = new StringBuilder();
                        Console.WriteLine($"icon:");

                        for (int i = 0; i < 2; i++)
                        {
                            bool[] segment16bit = BoardExtension2.SevenSegment.Icons.Skip(i * 16).Take(16).ToArray();
                            int analogValue = BoolListToDecimal(segment16bit);
                            Console.WriteLine($"icon {i}: {analogValue}");
                            binaryString_icon.Append((analogValue >= threshold) ? "1" : "0");
                        }

                        step.ValueGet1 = binaryString_icon.ToString();

                        if (binaryString_icon.ToString() == "11")
                        {
                            result = true;
                        }
                        else
                        {
                            result = false;
                        }

                        #endregion Icon

                        if (result)
                        {
                            break;
                        }
                    }
                    if (step.Condition1 == "LED")
                    {
                        List<String> LEDs = new List<string>();

                        if (step.Spect.Contains("/"))
                        {
                            LEDs = step.Spect.Split('/').ToList();
                        }
                        else
                        {
                            LEDs.Add(step.Spect);
                        }

                        List<int> indices = new List<int>();

                        foreach (var item in LEDs)
                        {
                            if (item.Contains('~'))
                            {
                                int startindex = Convert.ToInt32(item.Split('~')[0]);
                                int endindex = Convert.ToInt32(item.Split('~')[1]);
                                for (int index = startindex; index < endindex; index++)
                                {
                                    indices.Add(index - 1);
                                }
                            }
                            else
                            {
                                indices.Add(Convert.ToInt32(item) - 1);
                            }
                        }

                        stringCompare = string.Empty;
                        StringBuilder binaryString = new StringBuilder();
                        StringBuilder resultString = new StringBuilder();


                        BoardExtension2.SevenSegment.SignalRead(13);
                        BoardExtension2.SevenSegment.LEDs = BoardExtension2.SevenSegment.LogicLevel.ToList();
                        int idxLed = 0;
                        for (int i = 0; i < 13; i++)
                        {
                            idxLed++;

                            bool[] segment16bit = BoardExtension2.SevenSegment.LEDs.Skip(i * 16).Take(16).ToArray();
                            int analogValue = BoolListToDecimal(segment16bit);
                            Console.WriteLine($"led {idxLed}: {analogValue}");
                            binaryString.Append((analogValue >= threshold) ? "1" : "0");
                        }

                        BoardExtension1.SevenSegment.SignalRead(6);
                        BoardExtension1.SevenSegment.LEDs = BoardExtension1.SevenSegment.LogicLevel.ToList();
                        for (int i = 0; i < 6; i++)
                        {
                            idxLed++;
                            bool[] segment16bit = BoardExtension1.SevenSegment.LEDs.Skip(i * 16).Take(16).ToArray();
                            int analogValue = BoolListToDecimal(segment16bit);
                            Console.WriteLine($"led {idxLed}: {analogValue}");
                            binaryString.Append((analogValue >= threshold) ? "1" : "0");
                        }

                        foreach (int index in indices)
                        {
                            // Check if the index is within bounds
                            if (index >= 0 && index < binaryString.Length)
                            {
                                // Get the substring (in this case a single character at the index)
                                string substring = binaryString.ToString().Substring(index, 1);
                                // Append the substring to the result
                                resultString.Append(substring);
                            }
                        }

                        if (step.Oper == "ON")
                        {
                            bool allOnes = resultString.ToString().All(c => c == '1');
                            result = allOnes;
                        }
                        else
                        {
                            bool allZeros = resultString.ToString().All(c => c == '0');
                            result = allZeros;
                        }

                        step.ValueGet1 = resultString.ToString();
                        if (result)
                        {
                            step.ValueGet1 = Step.Ok;
                            break;
                        }
                    }

                    Task.Delay(delayTime).Wait();
                }
            }
            else
            {
                FunctionsParameterError("Condition2", step);
                return;
            }

            if (result)
            {
                step.Result1 = Step.Ok;
            }
            else
            {
                step.Result1 = Step.Ng;
            }

            return;
        }

        public void CAM(Step step)
        {
            if (Capture == null)
            {
                FunctionsParameterError("no cam", step);
                return;
            }

            Camera.CameraControl.VideoProperties properties;

            if (Enum.TryParse<Camera.CameraControl.VideoProperties>(step.Condition1, out properties))
            {
                if (properties == Camera.CameraControl.VideoProperties.Reset)
                {
                    Capture?.SetParammeter(TestModel.CameraSetting);
                    return;
                }

                int value = 0;

                if (Int32.TryParse(step.Oper, out value))
                {
                    Capture?.SetParammeter(properties, value, true);
                }
                else
                {
                    FunctionsParameterError("Oper", step);
                }
            }
            else
            {
                FunctionsParameterError("condition", step);
                return;
            }
        }

        public void UTN(Step step)
        {
            TxData txData = new TxData();
            txData = TestModel.Naming.TxDatas.Where(x => x.Name == step.Condition1).DefaultIfEmpty(null).FirstOrDefault();
            if (txData == null)
            {
                FunctionsParameterError("Naming", step);
                return;
            }
            foreach (var item in UUTs)
            {
                if (step.Oper == "P1" && item.Config != TestModel.P1_Config)
                {
                    item.Config = TestModel.P1_Config;
                }
                else if (step.Oper == "P2")
                {
                    item.Config = TestModel.P2_Config;
                }
            }

            var startTime = DateTime.Now;
            Int32 delay = 10;
            Int32 limittime = 10;
            int tryCount = 1;
            Int32.TryParse(step.Count, out delay);
            Int32.TryParse(step.Condition2, out limittime);
            Int32.TryParse(step.Min, out tryCount);

            switch (step.Mode)
            {
                case "NORMAL":
                    UTN_NORMAL(step, txData);
                    break;

                case "SEND-R":
                    var listTask1 = new List<Task<bool>>();

                    if (Boards.Count >= 1) if (!Boards[0].Skip) listTask1.Add(UTN_SEND_R(step, UUTs[0], 1, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 2) if (!Boards[1].Skip) listTask1.Add(UTN_SEND_R(step, UUTs[1], 2, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 3) if (!Boards[2].Skip) listTask1.Add(UTN_SEND_R(step, UUTs[2], 3, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 4) if (!Boards[3].Skip) listTask1.Add(UTN_SEND_R(step, UUTs[3], 4, txData, delay, limittime, tryCount));

                    try
                    {
                        // Wait for all the tasks to finish.
                        Task.WaitAll(listTask1.ToArray());

                        // We should never get to this point
                        Console.WriteLine("WaitAll() has not thrown exceptions. THIS WAS NOT EXPECTED.");
                    }
                    catch (AggregateException e)
                    {
                        Console.WriteLine("\nThe following exceptions have been thrown by WaitAll(): (THIS WAS EXPECTED)");
                        for (int j = 0; j < e.InnerExceptions.Count; j++)
                        {
                            Console.WriteLine("\n-------------------------------------------------\n{0}", e.InnerExceptions[j].ToString());
                        }
                    }
                    break;

                case "SEND_R":
                    var listTask2 = new List<Task<bool>>();

                    if (Boards.Count >= 1) if (!Boards[0].Skip) listTask2.Add(UTN_SEND_R(step, UUTs[0], 1, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 2) if (!Boards[1].Skip) listTask2.Add(UTN_SEND_R(step, UUTs[1], 2, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 3) if (!Boards[2].Skip) listTask2.Add(UTN_SEND_R(step, UUTs[2], 3, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 4) if (!Boards[3].Skip) listTask2.Add(UTN_SEND_R(step, UUTs[3], 4, txData, delay, limittime, tryCount));

                    try
                    {
                        // Wait for all the tasks to finish.
                        Task.WaitAll(listTask2.ToArray());

                        // We should never get to this point
                        Console.WriteLine("WaitAll() has not thrown exceptions. THIS WAS NOT EXPECTED.");
                    }
                    catch (AggregateException e)
                    {
                        Console.WriteLine("\nThe following exceptions have been thrown by WaitAll(): (THIS WAS EXPECTED)");
                        for (int j = 0; j < e.InnerExceptions.Count; j++)
                        {
                            Console.WriteLine("\n-------------------------------------------------\n{0}", e.InnerExceptions[j].ToString());
                        }
                    }
                    break;

                case "TIMER":
                    UTN_SendTimer(step, txData);
                    break;

                default:
                    break;
            }
        }

        public void UTX(Step step)
        {
            string txData = step.Condition1;
            if (txData == null || txData.Length < 2)
            {
                FunctionsParameterError("Condition", step);
                return;
            }
            foreach (var item in UUTs)
            {
                if (step.Oper == "P1" && item.Config != TestModel.P1_Config)
                {
                    item.Config = TestModel.P1_Config;
                }
                else if (step.Oper == "P2")
                {
                    item.Config = TestModel.P2_Config;
                }
            }

            var startTime = DateTime.Now;
            Int32 delay = 10;
            Int32 limittime = 10;
            int tryCount = 1;
            Int32.TryParse(step.Count, out delay);
            Int32.TryParse(step.Condition2, out limittime);
            Int32.TryParse(step.Min, out tryCount);

            switch (step.Mode)
            {
                case "NORMAL":
                    UTN_NORMAL(step, txData);
                    break;

                case "SEND-R":
                    var listTask1 = new List<Task<bool>>();

                    if (Boards.Count >= 1) if (!Boards[0].Skip) listTask1.Add(UTN_SEND_R(step, UUTs[0], 1, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 2) if (!Boards[1].Skip) listTask1.Add(UTN_SEND_R(step, UUTs[1], 2, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 3) if (!Boards[2].Skip) listTask1.Add(UTN_SEND_R(step, UUTs[2], 3, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 4) if (!Boards[3].Skip) listTask1.Add(UTN_SEND_R(step, UUTs[3], 4, txData, delay, limittime, tryCount));

                    try
                    {
                        // Wait for all the tasks to finish.
                        Task.WaitAll(listTask1.ToArray());

                        // We should never get to this point
                        Console.WriteLine("WaitAll() has not thrown exceptions. THIS WAS NOT EXPECTED.");
                    }
                    catch (AggregateException e)
                    {
                        Console.WriteLine("\nThe following exceptions have been thrown by WaitAll(): (THIS WAS EXPECTED)");
                        for (int j = 0; j < e.InnerExceptions.Count; j++)
                        {
                            Console.WriteLine("\n-------------------------------------------------\n{0}", e.InnerExceptions[j].ToString());
                        }
                    }
                    break;

                case "SEND_R":
                    var listTask2 = new List<Task<bool>>();

                    if (Boards.Count >= 1) if (!Boards[0].Skip) listTask2.Add(UTN_SEND_R(step, UUTs[0], 1, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 2) if (!Boards[1].Skip) listTask2.Add(UTN_SEND_R(step, UUTs[1], 2, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 3) if (!Boards[2].Skip) listTask2.Add(UTN_SEND_R(step, UUTs[2], 3, txData, delay, limittime, tryCount));
                    if (Boards.Count >= 4) if (!Boards[3].Skip) listTask2.Add(UTN_SEND_R(step, UUTs[3], 4, txData, delay, limittime, tryCount));

                    try
                    {
                        // Wait for all the tasks to finish.
                        Task.WaitAll(listTask2.ToArray());

                        // We should never get to this point
                        Console.WriteLine("WaitAll() has not thrown exceptions. THIS WAS NOT EXPECTED.");
                    }
                    catch (AggregateException e)
                    {
                        Console.WriteLine("\nThe following exceptions have been thrown by WaitAll(): (THIS WAS EXPECTED)");
                        for (int j = 0; j < e.InnerExceptions.Count; j++)
                        {
                            Console.WriteLine("\n-------------------------------------------------\n{0}", e.InnerExceptions[j].ToString());
                        }
                    }
                    break;

                case "TIMER":
                    UTN_SendTimer(step, txData);
                    break;

                default:
                    break;
            }
        }

        private void UTN_NORMAL(Step step, TxData txData)
        {
            if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result1 = UUTs[0].Send(txData) ? Step.Ok : Step.Ng;
            if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result2 = UUTs[1].Send(txData) ? Step.Ok : Step.Ng;
            if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result3 = UUTs[2].Send(txData) ? Step.Ok : Step.Ng;
            if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result4 = UUTs[3].Send(txData) ? Step.Ok : Step.Ng;

            if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = step.Result1 == Step.Ok ? Step.Ok : "Tx";
            if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = step.Result2 == Step.Ok ? Step.Ok : "Tx";
            if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = step.Result3 == Step.Ok ? Step.Ok : "Tx";
            if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = step.Result4 == Step.Ok ? Step.Ok : "Tx";
        }

        private void UTN_NORMAL(Step step, string txData)
        {
            if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result1 = UUTs[0].Send(txData) ? Step.Ok : Step.Ng;
            if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result2 = UUTs[1].Send(txData) ? Step.Ok : Step.Ng;
            if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result3 = UUTs[2].Send(txData) ? Step.Ok : Step.Ng;
            if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result4 = UUTs[3].Send(txData) ? Step.Ok : Step.Ng;

            if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = step.Result1 == Step.Ok ? Step.Ok : "Tx";
            if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = step.Result2 == Step.Ok ? Step.Ok : "Tx";
            if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = step.Result3 == Step.Ok ? Step.Ok : "Tx";
            if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = step.Result4 == Step.Ok ? Step.Ok : "Tx";
        }

        private void UTN_SendTimer(Step step, TxData txData)
        {
            if (int.TryParse(step.Count, out int time))
            {
                if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = UUTs[0].SendTimer(txData, time) ? Step.Ok : "Sys";
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = UUTs[1].SendTimer(txData, time) ? Step.Ok : "Sys";
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = UUTs[2].SendTimer(txData, time) ? Step.Ok : "Sys";
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = UUTs[3].SendTimer(txData, time) ? Step.Ok : "Sys";
            }
            else
            {
                if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = "Set time";
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = "Set time";
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = "Set time";
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = "Set time";
            }
        }

        private void UTN_SendTimer(Step step, string txData)
        {
            if (int.TryParse(step.Count, out int time))
            {
                if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = UUTs[0].SendTimer(txData, time) ? Step.Ok : "Sys";
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = UUTs[1].SendTimer(txData, time) ? Step.Ok : "Sys";
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = UUTs[2].SendTimer(txData, time) ? Step.Ok : "Sys";
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = UUTs[3].SendTimer(txData, time) ? Step.Ok : "Sys";
            }
            else
            {
                if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = "Set time";
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = "Set time";
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = "Set time";
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = "Set time";
            }
        }

        private async Task<bool> UTN_SEND_R(Step step, UUTPort UUT, int boardIndex, TxData txData, int DelayTime, int limitTime, int tryCount)
        {
            var start = DateTime.Now;
            for (int i = 0; i < tryCount; i++)
            {
                if (DateTime.Now.Subtract(start).TotalMilliseconds > limitTime)
                {
                    if (UUT.HaveBuffer())
                    {
                        SetValue(step, boardIndex, "OK");
                        return true;
                    }
                    else
                    {
                        SetValue(step, boardIndex, "Timeout", true);
                        return false;
                    }
                }
                else
                {
                    var sendOK = UUT.Send(txData);
                    if (!sendOK)
                    {
                        SetValue(step, boardIndex, "Tx", true);
                    }
                    await Task.Delay(DelayTime);
                    if (UUT.HaveBuffer())
                    {
                        SetValue(step, boardIndex, "OK");
                        return true;
                    }
                    else
                    {
                        SetValue(step, boardIndex, "Rx", true);
                    }
                }
            }
            return false;
        }

        private async Task<bool> UTN_SEND_R(Step step, UUTPort UUT, int boardIndex, string txData, int DelayTime, int limitTime, int tryCount)
        {
            var start = DateTime.Now;
            while (true)
            {
                var sendOK = UUT.Send(txData);
                if (sendOK)
                {
                    SetValue(step, boardIndex, "OK");
                }
                else
                {
                    SetValue(step, boardIndex, "Tx", true);
                }

                await Task.Delay(DelayTime);

                if (UUT.HaveBuffer())
                {
                    SetValue(step, boardIndex, "OK");
                    return true;
                }
                else
                {
                    SetValue(step, boardIndex, "Rx", true);
                    if (DateTime.Now.Subtract(start).TotalMilliseconds < limitTime)
                        return false;
                }
            }
        }

        private void SetValue(Step step, int Index, string value, bool IsFail = false)
        {
            switch (Index)
            {
                case 1:
                    if (Boards.Count > 0)
                    {
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = value;
                            if (IsFail)
                            {
                                step.Result1 = Step.Ng;
                            }
                            else
                            {
                                step.Result1 = Step.DontCare;
                            }
                        }
                    }
                    break;

                case 2:
                    if (Boards.Count > 1)
                    {
                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = value;
                            if (IsFail)
                            {
                                step.Result2 = Step.Ng;
                            }
                            else
                            {
                                step.Result2 = Step.DontCare;
                            }
                        }
                    }
                    break;

                case 3:
                    if (Boards.Count > 2)
                    {
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = value;
                            if (IsFail)
                            {
                                step.Result3 = Step.Ng;
                            }
                            else
                            {
                                step.Result3 = Step.DontCare;
                            }
                        }
                    }
                    break;

                case 4:
                    if (Boards.Count > 3)
                    {
                        if (!Boards[3].Skip)
                        {
                            step.ValueGet4 = value;
                            if (IsFail)
                            {
                                step.Result4 = Step.Ng;
                            }
                            else
                            {
                                step.Result4 = Step.DontCare;
                            }
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        public void UCN(Step step)
        {
            RxData rxData = new RxData();
            rxData = TestModel.Naming.RxDatas.Where(x => x.Name == step.Condition1).DefaultIfEmpty(null).FirstOrDefault();

            if (rxData != null)
            {
                if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = UUTs[0].CheckBufferString(rxData);
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = UUTs[1].CheckBufferString(rxData);
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = UUTs[2].CheckBufferString(rxData);
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = UUTs[3].CheckBufferString(rxData);

                if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result1 = step.ValueGet1 == step.Spect ? Step.Ok : Step.Ng;
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result2 = step.ValueGet2 == step.Spect ? Step.Ok : Step.Ng;
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result3 = step.ValueGet3 == step.Spect ? Step.Ok : Step.Ng;
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result4 = step.ValueGet4 == step.Spect ? Step.Ok : Step.Ng;
            }
            else
            {
                if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result1 = Step.Ng;
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result2 = Step.Ng;
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result3 = Step.Ng;
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result4 = Step.Ng;
            }
        }

        public void ReadLCD(Step step)
        {
            step.Result = true;
            if (int.TryParse(step.Condition2, out int scanTime))
            {
                DateTime start = DateTime.Now;
                while (DateTime.Now.Subtract(start).TotalMilliseconds < scanTime)
                {
                    int idx = 1;
                    foreach (var LCDitem in VisionTester.Models.LCDs)
                    {
                        if (idx == 1)
                        {
                            LCDitem.TestImage(Capture.LastMatFrame, step.Oper, false, step.LCDRoiValue0);
                        }

                        if (idx == 2)
                        {
                            LCDitem.TestImage(Capture.LastMatFrame, step.Oper, false, step.LCDRoiValue1);
                        }

                        if (idx == 3)
                        {
                            LCDitem.TestImage(Capture.LastMatFrame, step.Oper, false, step.LCDRoiValue2);
                        }

                        if (idx == 4)
                        {
                            LCDitem.TestImage(Capture.LastMatFrame, step.Oper, false, step.LCDRoiValue3);
                        }

                        idx++;
                    }

                    step.Result = true;

                    if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = step.Result1 != Step.Ok ?
                                    (VisionTester.Models.LCDs[0].DetectedString.Replace("B", "8") == step.Oper.Replace("B", "8") ? step.Oper :
                                    VisionTester.Models.LCDs[0].DetectedString) : step.ValueGet1;

                    if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = step.Result2 != Step.Ok ?
                                    (VisionTester.Models.LCDs[0].DetectedString.Replace("B", "8") == step.Oper.Replace("B", "8") ? step.Oper :
                                    VisionTester.Models.LCDs[1].DetectedString) : step.ValueGet2;

                    if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = step.Result3 != Step.Ok ?
                                    (VisionTester.Models.LCDs[0].DetectedString.Replace("B", "8") == step.Oper.Replace("B", "8") ? step.Oper :
                                    VisionTester.Models.LCDs[2].DetectedString) : step.ValueGet3;

                    if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = step.Result4 != Step.Ok ?
                                    (VisionTester.Models.LCDs[0].DetectedString.Replace("B", "8") == step.Oper.Replace("B", "8") ? step.Oper :
                                    VisionTester.Models.LCDs[3].DetectedString) : step.ValueGet4;

                    if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result1 = step.ValueGet1 == step.Oper ? Step.Ok : Step.Ng;
                    if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result2 = step.ValueGet2 == step.Oper ? Step.Ok : Step.Ng;
                    if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result3 = step.ValueGet3 == step.Oper ? Step.Ok : Step.Ng;
                    if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result4 = step.ValueGet4 == step.Oper ? Step.Ok : Step.Ng;

                    if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result &= (step.Result1 == Step.Ok);
                    if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result &= (step.Result2 == Step.Ok);
                    if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result &= (step.Result3 == Step.Ok);
                    if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result &= (step.Result4 == Step.Ok);

                    if (step.Result)
                        break;
                    Task.Delay(300).Wait();
                }
            }
            else
            {
                if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = VisionTester.Models.LCDs[0].DetectedString;
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = VisionTester.Models.LCDs[1].DetectedString;
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = VisionTester.Models.LCDs[2].DetectedString;
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = VisionTester.Models.LCDs[3].DetectedString;

                if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result1 = step.ValueGet1 == step.Oper ? Step.Ok : Step.Ng;
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result2 = step.ValueGet2 == step.Oper ? Step.Ok : Step.Ng;
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result3 = step.ValueGet3 == step.Oper ? Step.Ok : Step.Ng;
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result4 = step.ValueGet4 == step.Oper ? Step.Ok : Step.Ng;

                if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result &= (step.Result1 == Step.Ok);
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result &= (step.Result2 == Step.Ok);
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result &= (step.Result3 == Step.Ok);
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result &= (step.Result4 == Step.Ok);
            }
        }

        public void ReadFND(Step step)
        {
            //step.Result = true;

            //var random1 = new Random();
            //var random2 = new Random();
            //var random3 = new Random();
            //var random4 = new Random();

            //step.Result1 = random1.Next(10) < 4 ? Step.Ok : Step.Ng;
            //step.Result2 = random2.Next(10) < 4 ? Step.Ok : Step.Ng;
            //step.Result3 = random3.Next(10) < 4 ? Step.Ok : Step.Ng;
            //step.Result4 = random4.Next(10) < 4 ? Step.Ok : Step.Ng;

            //if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result &= (step.Result1 == Step.Ok);
            //if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result &= (step.Result2 == Step.Ok);
            //if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result &= (step.Result3 == Step.Ok);
            //if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result &= (step.Result4 == Step.Ok);

            //return;

            if (int.TryParse(step.Condition2, out int scanTime))
            {
                DateTime start = DateTime.Now;
                while (DateTime.Now.Subtract(start).TotalMilliseconds < scanTime)
                {
                    step.Result = true;

                    string DetectedString_Board0 = string.Empty;
                    string DetectedString_Board1 = string.Empty;
                    string DetectedString_Board2 = string.Empty;
                    string DetectedString_Board3 = string.Empty;

                    foreach (var fnds_char in VisionTester.Models.FNDs)
                    {
                        DetectedString_Board0 += fnds_char[0].DetectedString;
                        DetectedString_Board1 += fnds_char[1].DetectedString;
                        DetectedString_Board2 += fnds_char[2].DetectedString;
                        DetectedString_Board3 += fnds_char[3].DetectedString;
                    }


                    if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = step.Result1 != Step.Ok ? DetectedString_Board0 : step.ValueGet1;
                    if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = step.Result2 != Step.Ok ? DetectedString_Board1 : step.ValueGet2;
                    if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = step.Result3 != Step.Ok ? DetectedString_Board2 : step.ValueGet3;
                    if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = step.Result4 != Step.Ok ? DetectedString_Board3 : step.ValueGet4;

                    if (step.Condition1.Equals("Range [DEC]"))
                    {
                        if (int.TryParse(step.Max, out int max) && int.TryParse(step.Min, out int min))
                        {
                            if (Boards.Count >= 1 && (!Boards[0].Skip))
                            {
                                if (int.TryParse(step.ValueGet1, out int value))
                                {
                                    step.Result1 = (value >= min && value <= max) ? Step.Ok : Step.Ng;
                                }
                                else
                                {
                                    step.Result1 = Step.Ng;
                                }
                            }
                            if (Boards.Count >= 2 && (!Boards[1].Skip))
                            {
                                if (int.TryParse(step.ValueGet2, out int value))
                                {
                                    step.Result2 = (value >= min && value <= max) ? Step.Ok : Step.Ng;
                                }
                                else
                                {
                                    step.Result2 = Step.Ng;
                                }
                            }
                            if (Boards.Count >= 3 && (!Boards[2].Skip))
                            {
                                if (int.TryParse(step.ValueGet3, out int value))
                                {
                                    step.Result3 = (value >= min && value <= max) ? Step.Ok : Step.Ng;
                                }
                                else
                                {
                                    step.Result3 = Step.Ng;
                                }
                            }
                            if (Boards.Count >= 4 && (!Boards[3].Skip))
                            {
                                if (int.TryParse(step.ValueGet4, out int value))
                                {
                                    step.Result4 = (value >= min && value <= max) ? Step.Ok : Step.Ng;
                                }
                                else
                                {
                                    step.Result4 = Step.Ng;
                                }
                            }

                        }
                        else
                        {
                            step.Result1 = Step.Ng;
                            step.Result2 = Step.Ng;
                            step.Result3 = Step.Ng;
                            step.Result4 = Step.Ng;


                        }
                    }
                    else if (step.Condition1.Equals("Range [HEX]"))
                    {
                        if (int.TryParse(step.Max, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int max) &&
                            int.TryParse(step.Min, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int min))
                        {
                            if (Boards.Count >= 1 && (!Boards[0].Skip))
                            {
                                if (int.TryParse(step.ValueGet1, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
                                {
                                    step.Result1 = (value >= min && value <= max) ? Step.Ok : Step.Ng;
                                }
                                else
                                {
                                    step.Result1 = Step.Ng;
                                }
                            }
                            if (Boards.Count >= 2 && (!Boards[1].Skip))
                            {
                                if (int.TryParse(step.ValueGet2, out int value))
                                {
                                    step.Result2 = (value >= min && value <= max) ? Step.Ok : Step.Ng;
                                }
                                else
                                {
                                    step.Result2 = Step.Ng;
                                }
                            }
                            if (Boards.Count >= 3 && (!Boards[2].Skip))
                            {
                                if (int.TryParse(step.ValueGet3, out int value))
                                {
                                    step.Result3 = (value >= min && value <= max) ? Step.Ok : Step.Ng;
                                }
                                else
                                {
                                    step.Result3 = Step.Ng;
                                }
                            }
                            if (Boards.Count >= 4 && (!Boards[3].Skip))
                            {
                                if (int.TryParse(step.ValueGet4, out int value))
                                {
                                    step.Result4 = (value >= min && value <= max) ? Step.Ok : Step.Ng;
                                }
                                else
                                {
                                    step.Result4 = Step.Ng;
                                }
                            }

                        }
                        else
                        {
                            step.Result1 = Step.Ng;
                            step.Result2 = Step.Ng;
                            step.Result3 = Step.Ng;
                            step.Result4 = Step.Ng;


                        }
                    }
                    else
                    {
                        if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result1 = step.ValueGet1 == step.Oper ? Step.Ok : Step.Ng;
                        if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result2 = step.ValueGet2 == step.Oper ? Step.Ok : Step.Ng;
                        if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result3 = step.ValueGet3 == step.Oper ? Step.Ok : Step.Ng;
                        if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result4 = step.ValueGet4 == step.Oper ? Step.Ok : Step.Ng;


                    }

                    if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result &= (step.Result1 == Step.Ok);
                    if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result &= (step.Result2 == Step.Ok);
                    if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result &= (step.Result3 == Step.Ok);
                    if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result &= (step.Result4 == Step.Ok);
                    if (step.Result)
                        break;

                    Task.Delay(200).Wait();
                }
            }
            else
            {
                Console.WriteLine();

                string DetectedString_Board0 = string.Empty;
                string DetectedString_Board1 = string.Empty;
                string DetectedString_Board2 = string.Empty;
                string DetectedString_Board3 = string.Empty;

                foreach (var fnds_char in VisionTester.Models.FNDs)
                {
                    DetectedString_Board0 += fnds_char[0].DetectedString;
                    DetectedString_Board1 += fnds_char[1].DetectedString;
                    DetectedString_Board2 += fnds_char[2].DetectedString;
                    DetectedString_Board3 += fnds_char[3].DetectedString;
                }

                if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = DetectedString_Board0;
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = DetectedString_Board1;
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = DetectedString_Board2;
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = DetectedString_Board3;

                if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result1 = step.ValueGet1 == step.Oper ? Step.Ok : Step.Ng;
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result2 = step.ValueGet2 == step.Oper ? Step.Ok : Step.Ng;
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result3 = step.ValueGet3 == step.Oper ? Step.Ok : Step.Ng;
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result4 = step.ValueGet4 == step.Oper ? Step.Ok : Step.Ng;

                if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result &= (step.Result1 == Step.Ok);
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result &= (step.Result2 == Step.Ok);
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result &= (step.Result3 == Step.Ok);
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result &= (step.Result4 == Step.Ok);
            }
        }

        public void ReadLED(Step step)
        {
            if (int.TryParse(step.Condition2, out int scanTime))
            {
                step.Result = true;
                DateTime start = DateTime.Now;
                while (DateTime.Now.Subtract(start).TotalMilliseconds < scanTime)
                {
                    step.Result = true;
                    if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = VisionTester.Models.LED[0].CalculatorOutputString;
                    if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = VisionTester.Models.LED[1].CalculatorOutputString;
                    if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = VisionTester.Models.LED[2].CalculatorOutputString;
                    if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = VisionTester.Models.LED[3].CalculatorOutputString;

                    if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result1 = step.ValueGet1 == step.Oper ? Step.Ok : Step.Ng;
                    if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result2 = step.ValueGet2 == step.Oper ? Step.Ok : Step.Ng;
                    if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result3 = step.ValueGet3 == step.Oper ? Step.Ok : Step.Ng;
                    if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result4 = step.ValueGet4 == step.Oper ? Step.Ok : Step.Ng;

                    if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result &= (step.Result1 == Step.Ok);
                    if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result &= (step.Result2 == Step.Ok);
                    if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result &= (step.Result3 == Step.Ok);
                    if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result &= (step.Result4 == Step.Ok);

                    if (step.Result)
                        break;
                    Task.Delay(300).Wait();
                }
            }
            else
            {
                step.Result = true;
                if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = VisionTester.Models.LED[0].CalculatorOutputString;
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = VisionTester.Models.LED[1].CalculatorOutputString;
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = VisionTester.Models.LED[2].CalculatorOutputString;
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = VisionTester.Models.LED[3].CalculatorOutputString;

                if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result1 = step.ValueGet1 == step.Oper ? Step.Ok : Step.Ng;
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result2 = step.ValueGet2 == step.Oper ? Step.Ok : Step.Ng;
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result3 = step.ValueGet3 == step.Oper ? Step.Ok : Step.Ng;
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result4 = step.ValueGet4 == step.Oper ? Step.Ok : Step.Ng;

                if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result &= (step.Result1 == Step.Ok);
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result &= (step.Result2 == Step.Ok);
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result &= (step.Result3 == Step.Ok);
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result &= (step.Result4 == Step.Ok);
            }
        }

        public void ReadGLED(Step step)
        {
            if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = VisionTester.Models.GLED[0].CalculatorOutputString;
            if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = VisionTester.Models.GLED[1].CalculatorOutputString;
            if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = VisionTester.Models.GLED[2].CalculatorOutputString;
            if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = VisionTester.Models.GLED[3].CalculatorOutputString;

            if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result1 = step.ValueGet1 == step.Oper ? Step.Ok : Step.Ng;
            if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result2 = step.ValueGet2 == step.Oper ? Step.Ok : Step.Ng;
            if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result3 = step.ValueGet3 == step.Oper ? Step.Ok : Step.Ng;
            if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result4 = step.ValueGet4 == step.Oper ? Step.Ok : Step.Ng;

            if (Boards.Count >= 1) if (!Boards[0].Skip) step.Result &= (step.Result1 == Step.Ok);
            if (Boards.Count >= 2) if (!Boards[1].Skip) step.Result &= (step.Result2 == Step.Ok);
            if (Boards.Count >= 3) if (!Boards[2].Skip) step.Result &= (step.Result3 == Step.Ok);
            if (Boards.Count >= 4) if (!Boards[3].Skip) step.Result &= (step.Result4 == Step.Ok);
        }

        public void RLY_SYSTEM_BOARD(Step step)
        {
            if (!System.System_Board.SerialPort.Port.IsOpen)
            {
                FunctionsParameterError("Sys", step);
                return;
            }

            if (int.TryParse(step.Condition1, out int idxRLY))
            {
                System.System_Board.RLYControl(idxRLY, step.Oper == "ON");

                if (Int32.TryParse(step.Condition2, out int pressDelayTime))
                {
                    if (pressDelayTime > 0)
                    {
                        Task.Delay(pressDelayTime).Wait();

                        System.System_Board.RLYControl(idxRLY, false);
                    }
                }
                else
                {
                    FunctionsParameterError("Condition2", step);
                    return;
                }
            }
            else
            {
                FunctionsParameterError("Condition1", step);
                return;
            }

            if (Int32.TryParse(step.Count, out int delayTime))
            {
                Task.Delay(delayTime).Wait();
            }
            else
            {
                FunctionsParameterError("Count", step);
                return;
            }

            step.ValueGet1 = "exe";
            step.Result1 = Step.Ok;
        }

        public void RLY_RELAY_BOARD(Step step)
        {
            List<String> Channel = new List<string>();
            if (step.Condition1.Contains("/"))
            {
                Channel = step.Condition1.Split('/').ToList();
            }
            else
            {
                Channel.Add(step.Condition1);
            }
            List<int> numberChannel = new List<int>();
            foreach (var item in Channel)
            {
                if (item.Contains('~'))
                {
                    int startChannel = Convert.ToInt32(item.Split('~')[0]);
                    int endChannel = Convert.ToInt32(item.Split('~')[1]);
                    for (int i = startChannel; i < endChannel; i++)
                    {
                        numberChannel.Add(i - 1);
                    }
                }
                else
                {
                    numberChannel.Add(Convert.ToInt32(item) - 1);
                }
            }

            bool SetOK = Relay.SetChannels(numberChannel, step.Oper == "ON");
            switch (Boards.Count)
            {
                case 1:
                    if (!Boards[0].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet1 = "exe";
                            step.Result1 = Step.Ok;
                        }
                    }
                    break;

                case 2:
                    if (!Boards[0].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet1 = "exe";
                            step.Result1 = Step.Ok;
                        }
                    }

                    if (!Boards[1].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet2 = "exe";
                            step.Result2 = Step.Ok;
                        }
                    }
                    break;

                case 3:
                    if (!Boards[0].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet1 = "exe";
                            step.Result1 = Step.Ok;
                        }
                    }

                    if (!Boards[1].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet2 = "exe";
                            step.Result2 = Step.Ok;
                        }
                    }
                    if (!Boards[2].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet3 = "Sys";
                            step.Result3 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet3 = "exe";
                            step.Result3 = Step.Ok;
                        }
                    }
                    break;

                case 4:
                    if (!Boards[0].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet1 = "exe";
                            step.Result1 = Step.Ok;
                        }
                    }

                    if (!Boards[1].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet2 = "exe";
                            step.Result2 = Step.Ok;
                        }
                    }
                    if (!Boards[2].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet3 = "Sys";
                            step.Result3 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet3 = "exe";
                            step.Result3 = Step.Ok;
                        }
                    }
                    if (!Boards[3].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet4 = "Sys";
                            step.Result4 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet4 = "exe";
                            step.Result4 = Step.Ok;
                        }
                    }
                    break;

                default:
                    break;
            }
            if (Int32.TryParse(step.Condition2, out int pressDelayTime))
            {
                if (pressDelayTime > 0)
                {
                    Task.Delay(pressDelayTime).Wait();
                    SetOK = Relay.SetChannels(numberChannel, false);
                    if (!SetOK)
                    {
                        FunctionsParameterError("Sys", step);
                    }
                }
            }

            if (Int32.TryParse(step.Count, out int delayTime))
            {
                Task.Delay(delayTime).Wait();
            }
        }

        public void KEY(Step step)
        {
            List<String> Channel = new List<string>();
            if (step.Condition1 == null)
            {
                FunctionsParameterError("Condition 1", step);
                return;
            }
            if (step.Condition1.Contains("/"))
            {
                Channel = step.Condition1.Split('/').ToList();
            }
            else
            {
                Channel.Add(step.Condition1);
            }
            List<int> numberChannel = new List<int>();
            foreach (var item in Channel)
            {
                if (item.Contains('~'))
                {
                    if (!int.TryParse(item.Split('~')[0], out int startChannel))
                    {
                        FunctionsParameterError("Condition start", step);
                        return;
                    }
                    if (!int.TryParse(item.Split('~')[0], out int endChannel))
                    {
                        FunctionsParameterError("Condition start", step);
                        return;
                    }
                    for (int i = startChannel; i < endChannel; i++)
                    {
                        numberChannel.Add(i);
                    }
                }
                else
                {
                    if (int.TryParse(item, out int channelNumber))
                    {
                        numberChannel.Add(Convert.ToInt32(channelNumber));
                    }
                    else
                    {
                        FunctionsParameterError("Condition format", step);
                        return;
                    }
                }
            }
            bool SetOK = Solenoid.SetChannels(numberChannel, step.Oper == "ON");
            switch (Boards.Count)
            {
                case 1:
                    if (!Boards[0].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet1 = "ON";
                            step.Result1 = Step.Ok;
                        }
                    }
                    break;

                case 2:
                    if (!Boards[0].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet1 = "ON";
                            step.Result1 = Step.Ok;
                        }
                    }

                    if (!Boards[1].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet2 = "ON";
                            step.Result2 = Step.Ok;
                        }
                    }
                    break;

                case 3:
                    if (!Boards[0].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet1 = "ON";
                            step.Result1 = Step.Ok;
                        }
                    }

                    if (!Boards[1].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet2 = "ON";
                            step.Result2 = Step.Ok;
                        }
                    }
                    if (!Boards[2].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet3 = "Sys";
                            step.Result3 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet3 = "ON";
                            step.Result3 = Step.Ok;
                        }
                    }
                    break;

                case 4:
                    if (!Boards[0].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet1 = "ON";
                            step.Result1 = Step.Ok;
                        }
                    }

                    if (!Boards[1].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet2 = "ON";
                            step.Result2 = Step.Ok;
                        }
                    }
                    if (!Boards[2].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet3 = "Sys";
                            step.Result3 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet3 = "ON";
                            step.Result3 = Step.Ok;
                        }
                    }
                    if (!Boards[3].Skip)
                    {
                        if (!SetOK)
                        {
                            step.ValueGet4 = "Sys";
                            step.Result4 = Step.Ng;
                        }
                        else
                        {
                            step.ValueGet4 = "ON";
                            step.Result4 = Step.Ok;
                        }
                    }
                    break;

                default:
                    break;
            }
            if (Int32.TryParse(step.Condition2, out int pressDelayTime))
            {
                if (pressDelayTime > 0)
                {
                    Task.Delay(pressDelayTime).Wait();
                    SetOK = Solenoid.SetChannels(numberChannel, false);
                    if (!SetOK)
                    {
                        FunctionsParameterError("Sys", step);
                    }
                }
            }

            if (Int32.TryParse(step.Count, out int delayTime))
            {
                Task.Delay(delayTime).Wait();
            }
        }

        public void RES(Step step)
        {
            DMM.DMM_Rate rate;
            DMM.DMM_RES_Range range;
            try
            {
                range = (DMM.DMM_RES_Range)Enum.Parse(typeof(DMM.DMM_RES_Range), step.Oper, true);
            }
            catch (Exception)
            {
                switch (Boards.Count)
                {
                    case 1:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }
                        break;

                    case 2:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Oper";
                            step.Result2 = Step.Ng;
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Oper";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Oper";
                            step.Result3 = Step.Ng;
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Oper";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Oper";
                            step.Result3 = Step.Ng;
                        }
                        if (!Boards[3].Skip)
                        {
                            step.ValueGet4 = "Oper";
                            step.Result4 = Step.Ng;
                        }
                        break;

                    default:
                        break;
                }

                return;
            }
            try
            {
                rate = (DMM.DMM_Rate)Enum.Parse(typeof(DMM.DMM_Rate), step.Condition2, true);
                Console.WriteLine(rate.ToString());
            }
            catch (Exception)
            {
                switch (Boards.Count)
                {
                    case 1:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }
                        break;

                    case 2:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Condition";
                            step.Result3 = Step.Ng;
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Condition";
                            step.Result3 = Step.Ng;
                        }
                        if (!Boards[3].Skip)
                        {
                            step.ValueGet4 = "Condition";
                            step.Result4 = Step.Ng;
                        }
                        break;

                    default:
                        break;
                }

                return;
            }

            _DMM.SetModeRES(range, rate);
            DMM_BOARD_TEST(step);
        }

        public void ACV(Step step)
        {
            DMM.DMM_Rate rate;
            DMM.DMM_ACV_Range range;

            try
            {
                range = (DMM.DMM_ACV_Range)Enum.Parse(typeof(DMM.DMM_ACV_Range), step.Oper, true);
                Console.WriteLine(range.ToString());
            }
            catch (Exception)
            {
                FunctionsParameterError("Oper", step);

                return;
            }
            try
            {
                rate = (DMM.DMM_Rate)Enum.Parse(typeof(DMM.DMM_Rate), step.Condition2, true);
                Console.WriteLine(rate.ToString());
            }
            catch (Exception)
            {
                FunctionsParameterError("Condition", step);

                return;
            }

            _DMM.SetModeAC(range, rate);

            DMM_BOARD_TEST(step);
        }

        public void DCV(Step step)
        {
            DMM.DMM_Rate rate;
            DMM.DMM_DCV_Range range;

            try
            {
                range = (DMM.DMM_DCV_Range)Enum.Parse(typeof(DMM.DMM_DCV_Range), step.Oper, true);
                Console.WriteLine(range.ToString());
            }
            catch (Exception)
            {
                switch (Boards.Count)
                {
                    case 1:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }
                        break;

                    case 2:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Oper";
                            step.Result2 = Step.Ng;
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Oper";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Oper";
                            step.Result3 = Step.Ng;
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Oper";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Oper";
                            step.Result3 = Step.Ng;
                        }
                        if (!Boards[3].Skip)
                        {
                            step.ValueGet4 = "Oper";
                            step.Result4 = Step.Ng;
                        }
                        break;

                    default:
                        break;
                }

                return;
            }

            try
            {
                rate = (DMM.DMM_Rate)Enum.Parse(typeof(DMM.DMM_Rate), step.Condition2, true);
                Console.WriteLine(rate.ToString());
            }
            catch (Exception)
            {
                switch (Boards.Count)
                {
                    case 1:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }
                        break;

                    case 2:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Condition";
                            step.Result3 = Step.Ng;
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Condition";
                            step.Result3 = Step.Ng;
                        }
                        if (!Boards[3].Skip)
                        {
                            step.ValueGet4 = "Condition";
                            step.Result4 = Step.Ng;
                        }
                        break;

                    default:
                        break;
                }

                return;
            }

            _DMM.SetModeDC(range, rate);
            DMM_BOARD_TEST(step);
        }

        public void FREQ(Step step)
        {
            DMM.DMM_Rate rate;
            DMM.DMM_ACV_Range range;

            try
            {
                range = (DMM.DMM_ACV_Range)Enum.Parse(typeof(DMM.DMM_ACV_Range), step.Oper, true);
                Console.WriteLine(range.ToString());
            }
            catch (Exception)
            {
                switch (Boards.Count)
                {
                    case 1:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }
                        break;

                    case 2:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Oper";
                            step.Result2 = Step.Ng;
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Oper";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Oper";
                            step.Result3 = Step.Ng;
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Oper";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Oper";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Oper";
                            step.Result3 = Step.Ng;
                        }
                        if (!Boards[3].Skip)
                        {
                            step.ValueGet4 = "Oper";
                            step.Result4 = Step.Ng;
                        }
                        break;

                    default:
                        break;
                }

                return;
            }
            try
            {
                rate = (DMM.DMM_Rate)Enum.Parse(typeof(DMM.DMM_Rate), step.Condition2, true);
                Console.WriteLine(rate.ToString());
            }
            catch (Exception)
            {
                switch (Boards.Count)
                {
                    case 1:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }
                        break;

                    case 2:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Condition";
                            step.Result3 = Step.Ng;
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Condition";
                            step.Result3 = Step.Ng;
                        }
                        if (!Boards[3].Skip)
                        {
                            step.ValueGet4 = "Condition";
                            step.Result4 = Step.Ng;
                        }
                        break;

                    default:
                        break;
                }

                return;
            }

            _DMM.SetModeFREQ(range, rate);
            DMM_BOARD_TEST(step);
        }

        public void DIODE(Step step)
        {
            DMM.DMM_Rate rate;

            try
            {
                rate = (DMM.DMM_Rate)Enum.Parse(typeof(DMM.DMM_Rate), step.Condition2, true);
                Console.WriteLine(rate.ToString());
            }
            catch (Exception)
            {
                switch (Boards.Count)
                {
                    case 1:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }
                        break;

                    case 2:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Condition";
                            step.Result3 = Step.Ng;
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Condition";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Condition";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Condition";
                            step.Result3 = Step.Ng;
                        }
                        if (!Boards[3].Skip)
                        {
                            step.ValueGet4 = "Condition";
                            step.Result4 = Step.Ng;
                        }
                        break;

                    default:
                        break;
                }

                return;
            }

            _DMM.SetModeDiode(rate);

            DMM_BOARD_TEST(step);
        }

        private void DMM_BOARD_TEST(Step step)
        {
            if (!_DMM.DMM1.SerialPort.Port.IsOpen & !_DMM.DMM2.SerialPort.Port.IsOpen)
            {
                switch (Boards.Count)
                {
                    case 1:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }
                        break;

                    case 2:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Sys";
                            step.Result3 = Step.Ng;
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip)
                        {
                            step.ValueGet1 = "Sys";
                            step.Result1 = Step.Ng;
                        }

                        if (!Boards[1].Skip)
                        {
                            step.ValueGet2 = "Sys";
                            step.Result2 = Step.Ng;
                        }
                        if (!Boards[2].Skip)
                        {
                            step.ValueGet3 = "Sys";
                            step.Result3 = Step.Ng;
                        }
                        if (!Boards[3].Skip)
                        {
                            step.ValueGet4 = "Sys";
                            step.Result4 = Step.Ng;
                        }
                        break;

                    default:
                        break;
                }
                return;
            }
            Task.Delay(100).Wait();

            bool IsMux2WhenTest1Board = false;
            switch (Boards.Count)
            {
                case 1:
                    if (Boards[0].Skip) return;
                    if (!SetBoardMux(step.Condition1, 1, out IsMux2WhenTest1Board))
                    {
                        step.ValueGet1 = "condition1";
                        step.Result1 = Step.Ng;
                        return;
                    }
                    DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                    ReadDMMAndCompareBoard1(step, IsMux2WhenTest1Board);
                    break;

                case 2:
                    if (Boards[0].Skip && Boards[1].Skip) return;

                    if (!Boards[0].Skip)
                    {
                        if (!SetBoardMux(step.Condition1, 1, out _))
                        {
                            step.ValueGet1 = "condition1";
                            step.Result1 = Step.Ng;
                        }
                    }
                    if (!Boards[1].Skip)
                    {
                        if (!SetBoardMux(step.Condition1, 2, out _))
                        {
                            step.ValueGet2 = "condition1";
                            step.Result2 = Step.Ng;
                        }
                    }

                    if (step.Result1 != Step.Ng || step.Result2 != Step.Ng)
                        DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);

                    if (!Boards[0].Skip && !Boards[1].Skip)
                    {
                        if (!(step.Result1 == Step.Ng || step.Result2 == Step.Ng))
                            ReadDMMAndCompareBoard12(step);
                        else if (step.Result1 != Step.Ng)
                            ReadDMMAndCompareBoard1(step, false);
                        else if (step.Result2 != Step.Ng)
                            ReadDMMAndCompareBoard2(step);
                    }
                    else if (!Boards[0].Skip)
                    {
                        if (step.Result1 != Step.Ng)
                            ReadDMMAndCompareBoard1(step, false);
                    }
                    else if (!Boards[1].Skip)
                    {
                        if (step.Result2 != Step.Ng)
                            ReadDMMAndCompareBoard2(step);
                    }

                    break;

                case 3:
                    if (Boards[0].Skip && Boards[1].Skip && Boards[2].Skip) return;
                    if (!Boards[0].Skip)
                    {
                        if (!SetBoardMux(step.Condition1, 1, out _))
                        {
                            step.ValueGet1 = "condition1";
                            step.Result1 = Step.Ng;
                        }
                    }
                    if (!Boards[1].Skip)
                    {
                        if (!SetBoardMux(step.Condition1, 2, out _))
                        {
                            step.ValueGet2 = "condition1";
                            step.Result2 = Step.Ng;
                        }
                    }

                    if (!Boards[0].Skip || !Boards[1].Skip)
                    {
                        if (step.Result1 != Step.Ng || step.Result2 != Step.Ng)
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                    }

                    if (!Boards[0].Skip && !Boards[1].Skip)
                    {
                        if (!(step.Result1 == Step.Ng || step.Result2 == Step.Ng))
                            ReadDMMAndCompareBoard12(step);
                        else if (step.Result1 != Step.Ng)
                            ReadDMMAndCompareBoard1(step, false);
                        else if (step.Result2 != Step.Ng)
                            ReadDMMAndCompareBoard2(step);
                    }
                    else if (!Boards[0].Skip)
                    {
                        if (step.Result1 != Step.Ng)
                            ReadDMMAndCompareBoard1(step, false);
                    }
                    else if (!Boards[1].Skip)
                    {
                        if (step.Result2 != Step.Ng)
                            ReadDMMAndCompareBoard2(step);
                    }

                    if (!Boards[2].Skip)
                    {
                        if (!SetBoardMux(step.Condition1, 2, out _))
                        {
                            step.ValueGet3 = "condition1";
                            step.Result3 = Step.Ng;
                        }
                        else
                        {
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                        }
                    }

                    if (!Boards[2].Skip)
                        if (step.Result3 != Step.Ng)
                            ReadDMMAndCompareBoard3(step);
                        else
                            goto Out;

                    break;

                case 4:
                    if (Boards[0].Skip && Boards[1].Skip && Boards[2].Skip && Boards[3].Skip) return;
                    if (!Boards[0].Skip)
                    {
                        if (!SetBoardMux(step.Condition1, 1, out _))
                        {
                            step.ValueGet1 = "condition1";
                            step.Result1 = Step.Ng;
                        }
                    }
                    if (!Boards[1].Skip)
                    {
                        if (!SetBoardMux(step.Condition1, 2, out _))
                        {
                            step.ValueGet2 = "condition1";
                            step.Result2 = Step.Ng;
                        }
                    }

                    if (!Boards[0].Skip || !Boards[1].Skip)
                    {
                        if (!(step.Result1 == Step.Ng || step.Result2 == Step.Ng))
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                    }

                    if (!Boards[0].Skip && !Boards[1].Skip)
                    {
                        if (!(step.Result1 == Step.Ng || step.Result2 == Step.Ng))
                            ReadDMMAndCompareBoard12(step);
                        else if (step.Result1 != Step.Ng)
                            ReadDMMAndCompareBoard1(step, false);
                        else if (step.Result2 != Step.Ng)
                            ReadDMMAndCompareBoard2(step);
                    }
                    else if (!Boards[0].Skip)
                    {
                        if (step.Result1 != Step.Ng)
                            ReadDMMAndCompareBoard1(step, false);
                    }
                    else if (!Boards[1].Skip)
                    {
                        if (step.Result2 != Step.Ng)
                            ReadDMMAndCompareBoard2(step);
                    }

                    if (!Boards[2].Skip)
                    {
                        if (!SetBoardMux(step.Condition1, 3, out _))
                        {
                            step.ValueGet3 = "condition1";
                            step.Result3 = Step.Ng;
                        }
                    }
                    if (!Boards[3].Skip)
                    {
                        if (!SetBoardMux(step.Condition1, 4, out _))
                        {
                            step.ValueGet4 = "condition1";
                            step.Result4 = Step.Ng;
                        }
                    }

                    if (!Boards[2].Skip || !Boards[3].Skip)
                    {
                        if (!(step.Result3 == Step.Ng || step.Result4 == Step.Ng))
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                    }

                    if (!Boards[2].Skip && !Boards[3].Skip)
                    {
                        if (!(step.Result3 == Step.Ng || step.Result4 == Step.Ng))
                            ReadDMMAndCompareBoard34(step);
                        else if (step.Result3 != Step.Ng)
                            ReadDMMAndCompareBoard3(step);
                        else if (step.Result4 != Step.Ng)
                            ReadDMMAndCompareBoard4(step);
                    }
                    else if (!Boards[2].Skip)
                    {
                        if (step.Result3 != Step.Ng)
                            ReadDMMAndCompareBoard3(step);
                    }
                    else if (!Boards[3].Skip)
                    {
                        if (step.Result4 != Step.Ng)
                            ReadDMMAndCompareBoard4(step);
                    }
                    break;

                default:
                    break;
            }
        Out:
            Task.Delay(AppSetting.ETCSetting.DelayDMMRead).Wait();
            MuxCard.Card.ReleaseChannels();
        }

        private bool ReadDMMAndCompareBoard1(Step step, bool ReadByDmm2)
        {
            var retry = Int32.Parse(step.Count);
            retry = retry < 2 ? 1 : retry;

            if (step.Mode != "Spect")
            {
                _DMM.DMM1.RequestValues(retry);
                _DMM.DMM2.RequestValues(retry);
            }

            double minValue = 0;
            double maxValue = 0;

            if (!Double.TryParse(step.Min, out minValue))
            {
                step.ValueGet1 = "Min";

                step.Result1 = Step.Ng;

                return false;
            }

            if (!Double.TryParse(step.Max, out maxValue))
            {
                if (step.Max == "L")
                {
                    maxValue = Double.MaxValue;
                }
                else
                {
                    step.ValueGet1 = "Max";

                    step.Result1 = Step.Ng;

                    return false;
                }
            }

            if (step.Result1 == Step.Ng) return false;

            switch (step.Mode)
            {
                case "SPEC":
                    for (int i = 0; i < retry; i++)
                    {
                        if (step.Result1 != Step.Ok & Boards.Count > 0)
                        {
                            if (ReadByDmm2) _DMM.DMM2.GetValue();
                            else _DMM.DMM1.GetValue();
                            step.ValueGet1 = _DMM.DMM1.LastStringValue;
                            if (_DMM.DMM1.LastDoubleValue <= maxValue & _DMM.DMM1.LastDoubleValue >= minValue)
                            {
                                step.Result1 = Step.Ok;
                                break;
                            }
                            else
                            {
                                step.Result1 = Step.Ng;
                            }
                        }
                        Task.Delay(AppSetting.ETCSetting.DelayDMMRead).Wait();
                    }
                    break;

                case "CONT":
                    for (int i = 0; i < 40; i++)
                    {
                        if (ReadByDmm2) _DMM.DMM2.GetValue(i, 1);
                        else _DMM.DMM1.GetValue(i, 1);
                        Task.Delay(AppSetting.ETCSetting.DelayDMMRead).Wait();
                    }
                    var DMM1_PassValues = _DMM.DMM1.ValuesCount1.Where(x => (x >= minValue && x <= maxValue)).ToList();
                    if (DMM1_PassValues.Count == retry)
                    {
                        step.ValueGet1 = _DMM.DMM1.LastStringValue;
                        step.Result1 = Step.Ng;
                    }
                    else
                    {
                        step.Result1 = Step.Ok;
                        step.ValueGet1 = _DMM.DMM1.GetStringValue(DMM1_PassValues[0]);
                    }

                    break;

                case "MIN":
                    for (int i = 0; i < retry; i++)
                    {
                        if (ReadByDmm2) _DMM.DMM2.GetValue(i, 1);
                        else _DMM.DMM1.GetValue(i, 1);
                    }

                    step.Result1 = Step.Ok;

                    step.ValueGet1 = _DMM.DMM1.GetStringValue(_DMM.DMM1.MIN_1);

                    if (_DMM.DMM1.MIN_1 <= maxValue && _DMM.DMM1.MIN_1 >= minValue)
                    {
                        step.Result1 = Step.Ng;
                    }
                    break;

                case "AVR":
                    for (int i = 0; i < retry; i++)
                    {
                        if (ReadByDmm2) _DMM.DMM2.GetValue(i, 1);
                        else _DMM.DMM1.GetValue(i, 1);
                    }
                    step.Result1 = Step.Ok;

                    step.ValueGet1 = _DMM.DMM1.GetStringValue(_DMM.DMM1.AVR_1);

                    if (_DMM.DMM1.AVR_1 > maxValue || _DMM.DMM1.AVR_1 < minValue)
                    {
                        step.Result1 = Step.Ng;
                    }
                    step.Result2 = Step.Ng;

                    break;

                case "MAX":
                    for (int i = 0; i < retry; i++)
                    {
                        if (ReadByDmm2) _DMM.DMM2.GetValue(i, 1);
                        else _DMM.DMM1.GetValue(i, 1);
                    }

                    step.Result1 = Step.Ok;

                    step.ValueGet1 = _DMM.DMM1.GetStringValue(_DMM.DMM1.MAX_1);
                    if (_DMM.DMM1.MAX_1 <= maxValue && _DMM.DMM1.MAX_1 >= minValue)
                    {
                        step.Result1 = Step.Ng;
                    }
                    break;

                default:
                    break;
            }

            return StepTestResult(step);
        }

        private bool ReadDMMAndCompareBoard2(Step step)
        {
            var retry = Int32.Parse(step.Count);
            retry = retry < 2 ? 1 : retry;

            if (step.Mode != "Spect")
            {
                _DMM.DMM2.RequestValues(retry);
            }

            double minValue = 0;
            double maxValue = 0;

            if (!Double.TryParse(step.Min, out minValue))
            {
                step.ValueGet2 = "Min";

                step.Result2 = Step.Ng;

                return false;
            }

            if (!Double.TryParse(step.Max, out maxValue))
            {
                if (step.Max == "L")
                {
                    maxValue = Double.MaxValue;
                }
                else
                {
                    step.ValueGet2 = "Max";

                    step.Result2 = Step.Ng;

                    return false;
                }
            }

            if (step.Result2 == Step.Ng) return false;

            switch (step.Mode)
            {
                case "SPEC":
                    for (int i = 0; i < retry; i++)
                    {
                        if (step.Result2 != Step.Ok & Boards.Count > 1)
                        {
                            _DMM.DMM2.GetValue();
                            step.ValueGet2 = _DMM.DMM2.LastStringValue;
                            if (_DMM.DMM2.LastDoubleValue <= maxValue & _DMM.DMM2.LastDoubleValue >= minValue)
                            {
                                step.Result2 = Step.Ok;
                                break;
                            }
                            else
                            {
                                step.Result2 = Step.Ng;
                            }
                        }
                        Task.Delay(AppSetting.ETCSetting.DelayDMMRead).Wait();
                    }
                    break;

                case "CONT":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM2.GetValue(i, 1);
                        Task.Delay(AppSetting.ETCSetting.DelayDMMRead).Wait();
                    }
                    var DMM2_PassValues = _DMM.DMM2.ValuesCount1.Where(x => (x >= minValue && x <= maxValue)).ToList();

                    if (DMM2_PassValues.Count == 0)
                    {
                        step.ValueGet2 = _DMM.DMM2.LastStringValue;
                        step.Result2 = Step.Ng;
                    }
                    else
                    {
                        step.ValueGet2 = _DMM.DMM2.GetStringValue(DMM2_PassValues[0]);
                        step.Result2 = Step.Ok;
                    }

                    break;

                case "MIN":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM2.GetValue(i, 1);
                    }

                    step.Result2 = Step.Ok;

                    step.ValueGet2 = _DMM.DMM2.GetStringValue(_DMM.DMM2.MIN_1);

                    if (_DMM.DMM2.MIN_1 <= maxValue && _DMM.DMM2.MIN_1 >= minValue)
                    {
                        step.Result2 = Step.Ng;
                    }
                    break;

                case "AVR":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM2.GetValue(i, 1);
                    }
                    step.Result2 = Step.Ok;
                    step.ValueGet2 = _DMM.DMM2.GetStringValue(_DMM.DMM2.AVR_1);

                    if (_DMM.DMM2.AVR_1 > maxValue || _DMM.DMM2.AVR_1 < minValue)
                    {
                        step.Result2 = Step.Ng;
                    }
                    break;

                case "MAX":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM2.GetValue(i, 1);
                    }

                    step.Result2 = Step.Ok;

                    step.ValueGet2 = _DMM.DMM2.GetStringValue(_DMM.DMM2.MAX_1);

                    if (_DMM.DMM2.MAX_1 <= maxValue && _DMM.DMM2.MAX_1 >= minValue)
                    {
                        step.Result2 = Step.Ng;
                    }
                    break;

                default:
                    break;
            }

            return StepTestResult(step);
        }

        private bool ReadDMMAndCompareBoard3(Step step)
        {
            var retry = Int32.Parse(step.Count);
            retry = retry < 2 ? 2 : retry;

            if (step.Mode != "Spect")
            {
                _DMM.DMM1.RequestValues(retry);
            }

            double minValue = 0;
            double maxValue = 0;

            if (!Double.TryParse(step.Min, out minValue))
            {
                step.ValueGet3 = "Min";

                step.Result3 = Step.Ng;

                return false;
            }

            if (!Double.TryParse(step.Max, out maxValue))
            {
                if (step.Max == "L")
                {
                    maxValue = Double.MaxValue;
                }
                else
                {
                    step.ValueGet3 = "Max";

                    step.Result3 = Step.Ng;

                    return false;
                }
            }

            if (step.Result3 == Step.Ng) return false;

            switch (step.Mode)
            {
                case "SPEC":
                    for (int i = 0; i < retry; i++)
                    {
                        if (step.Result3 != Step.Ok & Boards.Count > 0)
                        {
                            _DMM.DMM1.GetValue();
                            step.ValueGet3 = _DMM.DMM1.LastStringValue;
                            if (_DMM.DMM1.LastDoubleValue <= maxValue & _DMM.DMM1.LastDoubleValue >= minValue)
                            {
                                step.Result3 = Step.Ok;
                                break;
                            }
                            else
                            {
                                step.Result3 = Step.Ng;
                            }
                        }
                        Task.Delay(AppSetting.ETCSetting.DelayDMMRead).Wait();
                    }
                    break;

                case "CONT":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                    }
                    var DMM1_PassValues = _DMM.DMM1.ValuesCount1.Where(x => (x >= minValue && x <= maxValue)).ToList();
                    if (DMM1_PassValues.Count == 0)
                    {
                        step.ValueGet3 = _DMM.DMM1.LastStringValue;
                        step.Result3 = Step.Ng;
                    }
                    else
                    {
                        step.Result3 = Step.Ok;
                        step.ValueGet3 = _DMM.DMM1.GetStringValue(DMM1_PassValues[0]);
                    }

                    break;

                case "MIN":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                    }

                    step.Result3 = Step.Ok;

                    step.ValueGet3 = _DMM.DMM1.GetStringValue(_DMM.DMM1.MIN_1);

                    if (_DMM.DMM1.MIN_1 <= maxValue && _DMM.DMM1.MIN_1 >= minValue)
                    {
                        step.Result3 = Step.Ng;
                    }
                    break;

                case "AVR":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                    }
                    step.Result3 = Step.Ok;

                    step.ValueGet3 = _DMM.DMM1.GetStringValue(_DMM.DMM1.AVR_1);

                    if (_DMM.DMM1.AVR_1 > maxValue || _DMM.DMM1.AVR_1 < minValue)
                    {
                        step.Result3 = Step.Ng;
                    }
                    break;

                case "MAX":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                    }

                    step.Result3 = Step.Ok;

                    step.ValueGet3 = _DMM.DMM1.GetStringValue(_DMM.DMM1.MAX_1);
                    if (_DMM.DMM1.MAX_1 <= maxValue && _DMM.DMM1.MAX_1 >= minValue)
                    {
                        step.Result3 = Step.Ng;
                    }
                    break;

                default:
                    break;
            }

            return StepTestResult(step);
        }

        private bool ReadDMMAndCompareBoard4(Step step)
        {
            var retry = Int32.Parse(step.Count);
            retry = retry < 2 ? 2 : retry;

            if (step.Mode != "Spect")
            {
                _DMM.DMM1.RequestValues(retry);
                _DMM.DMM2.RequestValues(retry);
            }

            double minValue = 0;
            double maxValue = 0;

            if (!Double.TryParse(step.Min, out minValue))
            {
                step.ValueGet4 = "Min";

                step.Result4 = Step.Ng;

                return false;
            }

            if (!Double.TryParse(step.Max, out maxValue))
            {
                if (step.Max == "L")
                {
                    maxValue = Double.MaxValue;
                }
                else
                {
                    step.ValueGet4 = "Max";

                    step.Result4 = Step.Ng;

                    return false;
                }
            }

            if (step.Result4 == Step.Ng) return false;

            switch (step.Mode)
            {
                case "SPEC":
                    for (int i = 0; i < retry; i++)
                    {
                        if (step.Result4 != Step.Ok & Boards.Count > 1)
                        {
                            _DMM.DMM2.GetValue();
                            step.ValueGet4 = _DMM.DMM2.LastStringValue;
                            if (_DMM.DMM2.LastDoubleValue <= maxValue & _DMM.DMM2.LastDoubleValue >= minValue)
                            {
                                step.Result4 = Step.Ok;
                            }
                            else
                            {
                                step.Result4 = Step.Ng;
                            }
                        }
                    }
                    break;

                case "CONT":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM2.GetValue(i, 1);
                    }
                    var DMM2_PassValues = _DMM.DMM2.ValuesCount1.Where(x => (x >= minValue && x <= maxValue)).ToList();

                    if (DMM2_PassValues.Count == 0)
                    {
                        step.ValueGet4 = _DMM.DMM2.LastStringValue;
                        step.Result4 = Step.Ng;
                    }
                    else
                    {
                        step.Result4 = Step.Ok;
                        step.ValueGet4 = _DMM.DMM2.GetStringValue(DMM2_PassValues[0]);
                    }

                    break;

                case "MIN":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM2.GetValue(i, 1);
                    }

                    step.Result4 = Step.Ok;

                    step.ValueGet4 = _DMM.DMM2.GetStringValue(_DMM.DMM2.MIN_1);

                    if (_DMM.DMM2.MIN_1 <= maxValue && _DMM.DMM2.MIN_1 >= minValue)
                    {
                        step.Result4 = Step.Ng;
                    }
                    break;

                case "AVR":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM2.GetValue(i, 1);
                    }
                    step.Result4 = Step.Ok;

                    step.ValueGet4 = _DMM.DMM2.GetStringValue(_DMM.DMM2.AVR_1);

                    if (_DMM.DMM2.AVR_1 > maxValue || _DMM.DMM2.AVR_1 < minValue)
                    {
                        step.Result4 = Step.Ng;
                    }
                    break;

                case "MAX":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM2.GetValue(i, 1);
                    }

                    step.Result4 = Step.Ok;

                    step.ValueGet4 = _DMM.DMM2.GetStringValue(_DMM.DMM2.MAX_1);

                    if (_DMM.DMM2.MAX_1 <= maxValue && _DMM.DMM2.MAX_1 >= minValue)
                    {
                        step.Result4 = Step.Ng;
                    }
                    break;

                default:
                    break;
            }

            return StepTestResult(step);
        }

        private bool ReadDMMAndCompareBoard12(Step step)
        {
            var retry = Int32.Parse(step.Count);
            retry = retry < 2 ? 1 : retry;

            if (step.Mode != "Spect")
            {
                _DMM.DMM1.RequestValues(retry);
                _DMM.DMM2.RequestValues(retry);
            }

            double minValue = 0;
            double maxValue = 0;

            if (!Double.TryParse(step.Min, out minValue))
            {
                step.ValueGet1 = "Min";
                step.ValueGet2 = "Min";
                step.ValueGet3 = "Min";
                step.ValueGet4 = "Min";

                step.Result1 = Step.Ng;
                step.Result2 = Step.Ng;
                step.Result3 = Step.Ng;
                step.Result4 = Step.Ng;

                return false;
            }

            if (!Double.TryParse(step.Max, out maxValue))
            {
                if (step.Max == "L")
                {
                    maxValue = Double.MaxValue;
                }
                else
                {
                    step.ValueGet1 = "Max";
                    step.ValueGet2 = "Max";
                    step.ValueGet3 = "Max";
                    step.ValueGet4 = "Max";

                    step.Result1 = Step.Ng;
                    step.Result2 = Step.Ng;
                    step.Result3 = Step.Ng;
                    step.Result4 = Step.Ng;

                    return false;
                }
            }

            switch (step.Mode)
            {
                case "SPEC":
                    for (int i = 0; i < retry; i++)
                    {
                        if (step.Result1 != Step.Ok & Boards.Count > 0)
                        {
                            _DMM.DMM1.GetValue();
                            step.ValueGet1 = _DMM.DMM1.LastStringValue;
                            if (_DMM.DMM1.LastDoubleValue <= maxValue & _DMM.DMM1.LastDoubleValue >= minValue)
                            {
                                step.Result1 = Step.Ok;
                            }
                            else
                            {
                                step.Result1 = Step.Ng;
                            }
                        }
                        if (step.Result2 != Step.Ok & Boards.Count > 1)
                        {
                            _DMM.DMM2.GetValue();
                            step.ValueGet2 = _DMM.DMM2.LastStringValue;
                            if (_DMM.DMM2.LastDoubleValue <= maxValue & _DMM.DMM2.LastDoubleValue >= minValue)
                            {
                                step.Result2 = Step.Ok;
                            }
                            else
                            {
                                step.Result2 = Step.Ng;
                            }
                        }
                        if (step.Result1 == Step.Ok && step.Result2 == Step.Ok)
                        {
                            break;
                        }
                        Task.Delay(AppSetting.ETCSetting.DelayDMMRead).Wait();
                    }
                    break;

                case "CONT":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                        _DMM.DMM2.GetValue(i, 1);
                        Task.Delay(AppSetting.ETCSetting.DelayDMMRead).Wait();
                    }
                    var DMM1_PassValues = _DMM.DMM1.ValuesCount1.Where(x => (x >= minValue && x <= maxValue)).ToList();
                    var DMM2_PassValues = _DMM.DMM2.ValuesCount1.Where(x => (x >= minValue && x <= maxValue)).ToList();
                    if (DMM1_PassValues.Count == 0)
                    {
                        step.ValueGet1 = _DMM.DMM1.LastStringValue;
                        step.Result1 = Step.Ng;
                    }
                    else
                    {
                        step.Result1 = Step.Ok;
                        step.ValueGet1 = _DMM.DMM1.GetStringValue(DMM1_PassValues[0]);
                    }

                    if (DMM2_PassValues.Count == 0)
                    {
                        step.ValueGet2 = _DMM.DMM2.LastStringValue;
                        step.Result2 = Step.Ng;
                    }
                    else
                    {
                        step.Result2 = Step.Ok;
                        step.ValueGet2 = _DMM.DMM2.GetStringValue(DMM2_PassValues[0]);
                    }

                    break;

                case "MIN":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                        _DMM.DMM2.GetValue(i, 1);
                    }

                    step.Result1 = Step.Ok;
                    step.Result2 = Step.Ok;

                    step.ValueGet1 = _DMM.DMM1.GetStringValue(_DMM.DMM1.MIN_1);
                    step.ValueGet2 = _DMM.DMM2.GetStringValue(_DMM.DMM2.MIN_1);

                    if (_DMM.DMM1.MIN_1 <= maxValue && _DMM.DMM1.MIN_1 >= minValue)
                    {
                        step.Result1 = Step.Ng;
                    }
                    if (_DMM.DMM2.MIN_1 <= maxValue && _DMM.DMM2.MIN_1 >= minValue)
                    {
                        step.Result2 = Step.Ng;
                    }
                    break;

                case "AVR":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                        _DMM.DMM2.GetValue(i, 1);
                    }
                    step.Result1 = Step.Ok;
                    step.Result2 = Step.Ok;

                    step.ValueGet1 = _DMM.DMM1.GetStringValue(_DMM.DMM1.AVR_1);
                    step.ValueGet2 = _DMM.DMM2.GetStringValue(_DMM.DMM2.AVR_1);

                    if (_DMM.DMM1.AVR_1 > maxValue || _DMM.DMM1.AVR_1 < minValue)
                    {
                        step.Result1 = Step.Ng;
                    }
                    if (_DMM.DMM2.AVR_1 > maxValue || _DMM.DMM2.AVR_1 < minValue)
                    {
                        step.Result2 = Step.Ng;
                    }
                    break;

                case "MAX":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                        _DMM.DMM2.GetValue(i, 1);
                    }

                    step.Result1 = Step.Ok;
                    step.Result2 = Step.Ok;

                    step.ValueGet1 = _DMM.DMM1.GetStringValue(_DMM.DMM1.MAX_1);
                    step.ValueGet2 = _DMM.DMM2.GetStringValue(_DMM.DMM2.MAX_1);
                    if (_DMM.DMM1.MAX_1 <= maxValue && _DMM.DMM1.MAX_1 >= minValue)
                    {
                        step.Result1 = Step.Ng;
                    }
                    if (_DMM.DMM2.MAX_1 <= maxValue && _DMM.DMM2.MAX_1 >= minValue)
                    {
                        step.Result2 = Step.Ng;
                    }
                    break;

                default:
                    break;
            }

            return StepTestResult(step);
        }

        private bool ReadDMMAndCompareBoard34(Step step)
        {
            var retry = Int32.Parse(step.Count);
            retry = retry < 2 ? 2 : retry;

            _DMM.DMM1.RequestValues(retry);
            _DMM.DMM2.RequestValues(retry);

            double minValue = 0;
            double maxValue = 0;

            if (!Double.TryParse(step.Min, out minValue))
            {
                step.ValueGet3 = "Min";
                step.ValueGet4 = "Min";

                step.Result3 = Step.Ng;
                step.Result4 = Step.Ng;

                return false;
            }

            if (!Double.TryParse(step.Max, out maxValue))
            {
                if (step.Max == "L")
                {
                    maxValue = Double.MaxValue;
                }
                else
                {
                    step.ValueGet3 = "Max";
                    step.ValueGet4 = "Max";

                    step.Result3 = Step.Ng;
                    step.Result4 = Step.Ng;

                    return false;
                }
            }

            step.Result3 = Step.DontCare;
            step.Result4 = Step.DontCare;

            switch (step.Mode)
            {
                case "SPEC":
                    for (int i = 0; i < retry; i++)
                    {
                        if (step.Result3 != Step.Ok & Boards.Count > 0)
                        {
                            _DMM.DMM1.GetValue();
                            step.ValueGet3 = _DMM.DMM1.LastStringValue;
                            if (_DMM.DMM1.LastDoubleValue <= maxValue & _DMM.DMM1.LastDoubleValue >= minValue)
                            {
                                step.Result3 = Step.Ok;
                            }
                            else
                            {
                                step.Result3 = Step.Ng;
                            }
                        }
                        if (step.Result4 != Step.Ok & Boards.Count > 1)
                        {
                            _DMM.DMM2.GetValue();
                            step.ValueGet4 = _DMM.DMM2.LastStringValue;
                            if (_DMM.DMM2.LastDoubleValue <= maxValue & _DMM.DMM2.LastDoubleValue >= minValue)
                            {
                                step.Result4 = Step.Ok;
                            }
                            else
                            {
                                step.Result4 = Step.Ng;
                            }
                        }
                    }
                    break;

                case "CONT":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                        _DMM.DMM2.GetValue(i, 1);
                    }
                    var DMM1_PassValues = _DMM.DMM1.ValuesCount1.Where(x => (x >= minValue && x <= maxValue)).ToList();
                    var DMM2_PassValues = _DMM.DMM2.ValuesCount1.Where(x => (x >= minValue && x <= maxValue)).ToList();
                    if (DMM1_PassValues.Count == 0)
                    {
                        step.ValueGet3 = _DMM.DMM1.LastStringValue;
                        step.Result3 = Step.Ng;
                    }
                    else
                    {
                        step.Result3 = Step.Ok;
                        step.ValueGet3 = _DMM.DMM1.GetStringValue(DMM1_PassValues[0]);
                    }

                    if (DMM2_PassValues.Count == 0)
                    {
                        step.ValueGet4 = _DMM.DMM2.LastStringValue;
                        step.Result4 = Step.Ng;
                    }
                    else
                    {
                        step.Result4 = Step.Ok;
                        step.ValueGet4 = _DMM.DMM2.GetStringValue(DMM2_PassValues[0]);
                    }

                    break;

                case "MIN":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                        _DMM.DMM2.GetValue(i, 1);
                    }

                    step.Result3 = Step.Ok;
                    step.Result4 = Step.Ok;

                    step.ValueGet3 = _DMM.DMM1.GetStringValue(_DMM.DMM1.MIN_1);
                    step.ValueGet4 = _DMM.DMM2.GetStringValue(_DMM.DMM2.MIN_1);

                    if (_DMM.DMM1.MIN_1 <= maxValue && _DMM.DMM1.MIN_1 >= minValue)
                    {
                        step.Result3 = Step.Ng;
                    }
                    if (_DMM.DMM2.MIN_1 <= maxValue && _DMM.DMM2.MIN_1 >= minValue)
                    {
                        step.Result4 = Step.Ng;
                    }
                    break;

                case "AVR":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                        _DMM.DMM2.GetValue(i, 1);
                    }
                    step.Result3 = Step.Ok;
                    step.Result4 = Step.Ok;

                    step.ValueGet2 = _DMM.DMM1.GetStringValue(_DMM.DMM1.AVR_1);
                    step.ValueGet4 = _DMM.DMM2.GetStringValue(_DMM.DMM2.AVR_1);

                    if (_DMM.DMM1.AVR_1 > maxValue || _DMM.DMM1.AVR_1 < minValue)
                    {
                        step.Result3 = Step.Ng;
                    }
                    if (_DMM.DMM2.AVR_1 > maxValue || _DMM.DMM2.AVR_1 < minValue)
                    {
                        step.Result4 = Step.Ng;
                    }
                    break;

                case "MAX":
                    for (int i = 0; i < retry; i++)
                    {
                        _DMM.DMM1.GetValue(i, 1);
                        _DMM.DMM2.GetValue(i, 1);
                    }

                    step.Result3 = Step.Ok;
                    step.Result4 = Step.Ok;

                    step.ValueGet2 = _DMM.DMM1.GetStringValue(_DMM.DMM1.MAX_1);
                    step.ValueGet4 = _DMM.DMM2.GetStringValue(_DMM.DMM2.MAX_1);
                    if (_DMM.DMM1.MAX_1 <= maxValue && _DMM.DMM1.MAX_1 >= minValue)
                    {
                        step.Result3 = Step.Ng;
                    }
                    if (_DMM.DMM2.MAX_1 <= maxValue && _DMM.DMM2.MAX_1 >= minValue)
                    {
                        step.Result4 = Step.Ng;
                    }
                    break;

                default:
                    break;
            }

            return StepTestResult(step);
        }

        private void DelayAfterMuxSellect(DMM.DMM_Mode mode, DMM.DMM_Rate rate)
        {
            switch (mode)
            {
                case DMM.DMM_Mode.NONE:
                    break;

                case DMM.DMM_Mode.DCV:
                    switch (rate)
                    {
                        case DMM.DMM_Rate.NONE:
                            break;

                        case DMM.DMM_Rate.SLOW:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_slow_DCV).Wait();
                            break;

                        case DMM.DMM_Rate.MID:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_Mid_DCV).Wait();
                            break;

                        case DMM.DMM_Rate.FAST:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_Fast_DCV).Wait();
                            break;

                        default:
                            break;
                    }
                    break;

                case DMM.DMM_Mode.ACV:
                    switch (rate)
                    {
                        case DMM.DMM_Rate.NONE:
                            break;

                        case DMM.DMM_Rate.SLOW:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_slow_ACVFRQ).Wait();
                            break;

                        case DMM.DMM_Rate.MID:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_Mid_ACVFRQ).Wait();
                            break;

                        case DMM.DMM_Rate.FAST:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_Fast_ACVFRQ).Wait();
                            break;

                        default:
                            break;
                    }
                    break;

                case DMM.DMM_Mode.FREQ:
                    switch (rate)
                    {
                        case DMM.DMM_Rate.NONE:
                            break;

                        case DMM.DMM_Rate.SLOW:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_slow_ACVFRQ).Wait();
                            break;

                        case DMM.DMM_Rate.MID:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_Mid_ACVFRQ).Wait();
                            break;

                        case DMM.DMM_Rate.FAST:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_Fast_ACVFRQ).Wait();
                            break;

                        default:
                            break;
                    }
                    break;

                case DMM.DMM_Mode.RES:
                    switch (rate)
                    {
                        case DMM.DMM_Rate.NONE:
                            break;

                        case DMM.DMM_Rate.SLOW:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_slow_RES).Wait();
                            break;

                        case DMM.DMM_Rate.MID:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_Mid_RES).Wait();
                            break;

                        case DMM.DMM_Rate.FAST:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_Fast_RES).Wait();
                            break;

                        default:
                            break;
                    }
                    break;

                case DMM.DMM_Mode.DIODE:
                    switch (rate)
                    {
                        case DMM.DMM_Rate.NONE:
                            break;

                        case DMM.DMM_Rate.SLOW:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_slow_DCV).Wait();
                            break;

                        case DMM.DMM_Rate.MID:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_Mid_DCV).Wait();
                            break;

                        case DMM.DMM_Rate.FAST:
                            Task.Delay(AppSetting.ETCSetting.MUXdelay_Fast_DCV).Wait();
                            break;

                        default:
                            break;
                    }
                    break;

                default:
                    break;
            }
        }

        private bool DisCharge()
        {
            if (!_DMM.DMM1.SerialPort.Port.IsOpen & !_DMM.DMM2.SerialPort.Port.IsOpen)
            {
                return false;
            }
            System.System_Board.MachineIO.ADSC = true;
            System.System_Board.MachineIO.BDSC = true;
            System.System_Board.SendControl();

            //Discharge item 1
            bool DisChargeItem1Pass = false;
            bool DisChargeItem2Pass = false;
            bool DisChargeItem3Pass = false;

            DateTime StartDisChargeTime = DateTime.Now;
            if (TestModel.Discharge.DischargeItem1 != 0) // check none discharge mode
            {
                switch (TestModel.Discharge.DischargeItem1)
                {
                    case 1:
                        _DMM.SetModeDC(DMM.DMM_DCV_Range.DC1000V, DMM.DMM_Rate.MID);
                        break;

                    case 2:
                        _DMM.SetModeAC(DMM.DMM_ACV_Range.AC750V, DMM.DMM_Rate.MID);
                        break;

                    default:
                        break;
                }
                switch (Boards.Count)
                {
                    case 1:
                        if (Boards[0].Skip) return true;
                        if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out bool IsMux2WhenTest1Board))
                        {
                            return false;
                        }
                        DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                        StartDisChargeTime = DateTime.Now;
                        while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                        {
                            if (IsMux2WhenTest1Board)
                            {
                                _DMM.DMM1.GetValue();
                                if (_DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow)
                                {
                                    DisChargeItem1Pass = true;
                                    break;
                                }
                            }
                            else
                            {
                                _DMM.DMM2.GetValue();
                                if (_DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow)
                                {
                                    DisChargeItem1Pass = true;
                                    break;
                                }
                            }
                        }
                        break;

                    case 2:
                        if (Boards[0].Skip && Boards[1].Skip) return true;
                        if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out _))
                        {
                            return false;
                        }
                        if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 2, out _))
                        {
                            return false;
                        }
                        DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                        StartDisChargeTime = DateTime.Now;
                        while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                        {
                            _DMM.DMM1.GetValue();
                            _DMM.DMM2.GetValue();
                            if (!Boards[0].Skip && !Boards[1].Skip)
                            {
                                DisChargeItem1Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                    & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                if (DisChargeItem1Pass) break;
                            }
                            else if (!Boards[0].Skip)
                            {
                                DisChargeItem1Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                if (DisChargeItem1Pass) break;
                            }
                            else if (!Boards[1].Skip)
                            {
                                DisChargeItem1Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                if (DisChargeItem1Pass) break;
                            }
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip || !Boards[1].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out _))
                            {
                                return false;
                            }
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 2, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                _DMM.DMM2.GetValue();
                                if (!Boards[0].Skip && !Boards[1].Skip)
                                {
                                    DisChargeItem1Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                        & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem1Pass) break;
                                }
                                else if (!Boards[0].Skip)
                                {
                                    DisChargeItem1Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem1Pass) break;
                                }
                                else if (!Boards[1].Skip)
                                {
                                    DisChargeItem1Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem1Pass) break;
                                }
                            }
                        }

                        if (!Boards[2].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 3, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                if (_DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow)
                                {
                                    DisChargeItem1Pass &= true;
                                    break;
                                }
                            }
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip || !Boards[1].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out _))
                            {
                                return false;
                            }
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 2, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                _DMM.DMM2.GetValue();
                                if (!Boards[0].Skip && !Boards[1].Skip)
                                {
                                    DisChargeItem1Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                        & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem1Pass) break;
                                }
                                else if (!Boards[0].Skip)
                                {
                                    DisChargeItem1Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem1Pass) break;
                                }
                                else if (!Boards[1].Skip)
                                {
                                    DisChargeItem1Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem1Pass) break;
                                }
                            }
                        }

                        if (!Boards[3].Skip || !Boards[2].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 3, out _))
                            {
                                return false;
                            }
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 4, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                _DMM.DMM2.GetValue();
                                if (!Boards[2].Skip && !Boards[3].Skip)
                                {
                                    DisChargeItem1Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                        & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem1Pass) break;
                                }
                                else if (!Boards[2].Skip)
                                {
                                    DisChargeItem1Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem1Pass) break;
                                }
                                else if (!Boards[3].Skip)
                                {
                                    DisChargeItem1Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem1Pass) break;
                                }
                            }
                        }
                        break;
                }
            }

            if (TestModel.Discharge.DischargeItem2 != 0 && DisChargeItem1Pass) // check none discharge mode
            {
                switch (TestModel.Discharge.DischargeItem2)
                {
                    case 1:
                        _DMM.SetModeDC(DMM.DMM_DCV_Range.DC1000V, DMM.DMM_Rate.MID);
                        break;

                    case 2:
                        _DMM.SetModeAC(DMM.DMM_ACV_Range.AC750V, DMM.DMM_Rate.MID);
                        break;

                    default:
                        break;
                }
                switch (Boards.Count)
                {
                    case 1:
                        if (Boards[0].Skip) return true;
                        if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out bool IsMux2WhenTest1Board))
                        {
                            return false;
                        }
                        DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                        StartDisChargeTime = DateTime.Now;
                        while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                        {
                            if (IsMux2WhenTest1Board)
                            {
                                _DMM.DMM1.GetValue();
                                if (_DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow)
                                {
                                    DisChargeItem2Pass = true;
                                    break;
                                }
                            }
                            else
                            {
                                _DMM.DMM2.GetValue();
                                if (_DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow)
                                {
                                    DisChargeItem2Pass = true;
                                    break;
                                }
                            }
                        }
                        break;

                    case 2:
                        if (Boards[0].Skip && Boards[1].Skip) return true;
                        if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out _))
                        {
                            return false;
                        }
                        if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 2, out _))
                        {
                            return false;
                        }
                        DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                        StartDisChargeTime = DateTime.Now;
                        while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                        {
                            _DMM.DMM1.GetValue();
                            _DMM.DMM2.GetValue();
                            if (!Boards[0].Skip && !Boards[1].Skip)
                            {
                                DisChargeItem2Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                    & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                if (DisChargeItem2Pass) break;
                            }
                            else if (!Boards[0].Skip)
                            {
                                DisChargeItem2Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                if (DisChargeItem2Pass) break;
                            }
                            else if (!Boards[1].Skip)
                            {
                                DisChargeItem2Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                if (DisChargeItem2Pass) break;
                            }
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip || !Boards[1].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out _))
                            {
                                return false;
                            }
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 2, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                _DMM.DMM2.GetValue();
                                if (!Boards[0].Skip && !Boards[1].Skip)
                                {
                                    DisChargeItem2Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                        & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem2Pass) break;
                                }
                                else if (!Boards[0].Skip)
                                {
                                    DisChargeItem2Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem2Pass) break;
                                }
                                else if (!Boards[1].Skip)
                                {
                                    DisChargeItem2Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem2Pass) break;
                                }
                            }
                        }

                        if (!Boards[2].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 3, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                if (_DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow)
                                {
                                    DisChargeItem2Pass &= true;
                                    break;
                                }
                            }
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip || !Boards[1].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out _))
                            {
                                return false;
                            }
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 2, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                _DMM.DMM2.GetValue();
                                if (!Boards[0].Skip && !Boards[1].Skip)
                                {
                                    DisChargeItem2Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                        & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem2Pass) break;
                                }
                                else if (!Boards[0].Skip)
                                {
                                    DisChargeItem2Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem2Pass) break;
                                }
                                else if (!Boards[1].Skip)
                                {
                                    DisChargeItem2Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem2Pass) break;
                                }
                            }
                        }

                        if (!Boards[3].Skip || !Boards[2].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 3, out _))
                            {
                                return false;
                            }
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 4, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                _DMM.DMM2.GetValue();
                                if (!Boards[2].Skip && !Boards[3].Skip)
                                {
                                    DisChargeItem2Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                        & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem2Pass) break;
                                }
                                else if (!Boards[2].Skip)
                                {
                                    DisChargeItem2Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem2Pass) break;
                                }
                                else if (!Boards[3].Skip)
                                {
                                    DisChargeItem2Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem2Pass) break;
                                }
                            }
                        }
                        break;
                }
            }

            if (TestModel.Discharge.DischargeItem3 != 0 && DisChargeItem2Pass) // check none discharge mode
            {
                switch (TestModel.Discharge.DischargeItem3)
                {
                    case 1:
                        _DMM.SetModeDC(DMM.DMM_DCV_Range.DC1000V, DMM.DMM_Rate.MID);
                        break;

                    case 2:
                        _DMM.SetModeAC(DMM.DMM_ACV_Range.AC750V, DMM.DMM_Rate.MID);
                        break;

                    default:
                        break;
                }
                switch (Boards.Count)
                {
                    case 1:
                        if (Boards[0].Skip) return true;
                        if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out bool IsMux2WhenTest1Board))
                        {
                            return false;
                        }
                        DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                        StartDisChargeTime = DateTime.Now;
                        while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                        {
                            if (IsMux2WhenTest1Board)
                            {
                                _DMM.DMM1.GetValue();
                                if (_DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow)
                                {
                                    DisChargeItem3Pass = true;
                                    break;
                                }
                            }
                            else
                            {
                                _DMM.DMM2.GetValue();
                                if (_DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow)
                                {
                                    DisChargeItem3Pass = true;
                                    break;
                                }
                            }
                        }
                        break;

                    case 2:
                        if (Boards[0].Skip && Boards[1].Skip) return true;
                        if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out _))
                        {
                            return false;
                        }
                        if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 2, out _))
                        {
                            return false;
                        }
                        DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                        StartDisChargeTime = DateTime.Now;
                        while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                        {
                            _DMM.DMM1.GetValue();
                            _DMM.DMM2.GetValue();
                            if (!Boards[0].Skip && !Boards[1].Skip)
                            {
                                DisChargeItem3Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                    & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                if (DisChargeItem3Pass) break;
                            }
                            else if (!Boards[0].Skip)
                            {
                                DisChargeItem3Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                if (DisChargeItem3Pass) break;
                            }
                            else if (!Boards[1].Skip)
                            {
                                DisChargeItem3Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                if (DisChargeItem3Pass) break;
                            }
                        }
                        break;

                    case 3:
                        if (!Boards[0].Skip || !Boards[1].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out _))
                            {
                                return false;
                            }
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 2, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                _DMM.DMM2.GetValue();
                                if (!Boards[0].Skip && !Boards[1].Skip)
                                {
                                    DisChargeItem3Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                        & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem3Pass) break;
                                }
                                else if (!Boards[0].Skip)
                                {
                                    DisChargeItem3Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem3Pass) break;
                                }
                                else if (!Boards[1].Skip)
                                {
                                    DisChargeItem3Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem3Pass) break;
                                }
                            }
                        }

                        if (!Boards[2].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 3, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                if (_DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow)
                                {
                                    DisChargeItem3Pass &= true;
                                    break;
                                }
                            }
                        }
                        break;

                    case 4:
                        if (!Boards[0].Skip || !Boards[1].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 1, out _))
                            {
                                return false;
                            }
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 2, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                _DMM.DMM2.GetValue();
                                if (!Boards[0].Skip && !Boards[1].Skip)
                                {
                                    DisChargeItem3Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                        & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem3Pass) break;
                                }
                                else if (!Boards[0].Skip)
                                {
                                    DisChargeItem3Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem3Pass) break;
                                }
                                else if (!Boards[1].Skip)
                                {
                                    DisChargeItem3Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem3Pass) break;
                                }
                            }
                        }

                        if (!Boards[3].Skip || !Boards[2].Skip)
                        {
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 3, out _))
                            {
                                return false;
                            }
                            if (!SetBoardMux(String.Format("{0}/{1}", TestModel.Discharge.Item1ChannelP, TestModel.Discharge.Item1ChannelN), 4, out _))
                            {
                                return false;
                            }
                            DelayAfterMuxSellect(_DMM.Mode, _DMM.Rate);
                            StartDisChargeTime = DateTime.Now;
                            while (DateTime.Now.Subtract(StartDisChargeTime).TotalMilliseconds < TestModel.Discharge.DischargeTime)
                            {
                                _DMM.DMM1.GetValue();
                                _DMM.DMM2.GetValue();
                                if (!Boards[2].Skip && !Boards[3].Skip)
                                {
                                    DisChargeItem3Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow
                                        & _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem3Pass) break;
                                }
                                else if (!Boards[2].Skip)
                                {
                                    DisChargeItem3Pass = _DMM.DMM1.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem3Pass) break;
                                }
                                else if (!Boards[3].Skip)
                                {
                                    DisChargeItem3Pass = _DMM.DMM2.LastDoubleValue < TestModel.Discharge.Item1VoltageBelow;
                                    if (DisChargeItem3Pass) break;
                                }
                            }
                        }
                        break;
                }
            }

            return DisChargeItem1Pass & DisChargeItem2Pass & DisChargeItem3Pass;
        }

        private void PCB(Step step)
        {
            List<string> BoardSellect = step.Condition1.Split(',').ToList();
            foreach (var item in Boards)
            {
                item.Skip = true;
            }
            foreach (var item in BoardSellect)
            {
                switch (item)
                {
                    case "1":
                        if (Boards.Count > 1) Boards[0].Skip = false;
                        break;

                    case "2":
                        if (Boards.Count > 2) Boards[1].Skip = false;
                        break;

                    case "3":
                        if (Boards.Count > 3) Boards[2].Skip = false;
                        break;

                    case "4":
                        if (Boards.Count > 4) Boards[3].Skip = false;
                        break;

                    default:
                        break;
                }
            }
        }

        private void STL(Step step)
        {
            if (!Level.SerialPort.Port.IsOpen)
            {
                FunctionsParameterError("Sys", step);
                return;
            }

            if (!Int32.TryParse(step.Condition2, out int totalTime))
            {
                if (string.IsNullOrEmpty(step.Condition2))
                {
                    totalTime = -1;
                }
                else
                {
                    FunctionsParameterError("Condition2", step);
                    return;
                }
            }

            int sample_time = 0;
            if (!Int32.TryParse(step.Oper, out sample_time))
            {
                if (string.IsNullOrEmpty(step.Oper))
                {
                    sample_time = 100;
                    step.Oper = "100";
                }
                else
                {
                    FunctionsParameterError("Oper", step);
                    return;
                }
            }

            Level.StartGetSample(sample_time);
            if (!(totalTime == -1))
            {
                LevelChannel arbitrary_channel = Boards[0].LevelChannels.Where(channel => channel.IsUse == true).ToList().FirstOrDefault();
                if (arbitrary_channel != null)
                {
                    while (arbitrary_channel.Samples.Count * sample_time < totalTime)
                    {
                        // Do nothing, it's blocking;
                    }
                    Level.StopGetSample();
                }
            }

            bool SetOK = true;

            for (int board_idx = 0; board_idx < Boards.Count; board_idx++)
            {
                var Board = Boards[board_idx];

                if (!Board.Skip)
                {
                    if (!SetOK)
                    {
                        step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "Sys");
                        step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ng);
                    }
                    else
                    {
                        step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "exe");
                        step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ok);
                    }
                }
            }
        }

        private void EDL(Step step)
        {
            if (!Level.SerialPort.Port.IsOpen)
            {
                FunctionsParameterError("Sys", step);
                return;
            }
            Level.StopGetSample();
            bool SetOK = true;

            for (int board_idx = 0; board_idx < Boards.Count; board_idx++)
            {
                var Board = Boards[board_idx];

                if (!Board.Skip)
                {
                    if (!SetOK)
                    {
                        step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "Sys");
                        step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ng);
                    }
                    else
                    {
                        step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "exe");
                        step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ok);
                    }
                }
            }
        }

        private void LCC(Step step)
        {
            if (!Level.SerialPort.Port.IsOpen)
            {
                FunctionsParameterError("Sys", step);
                return;
            }

            int channel = 0;
            if (!Int32.TryParse(step.Condition1, out channel))
            {
                FunctionsParameterError("Condition1", step);
                return;
            }
            channel = channel - 1;

            if (channel >= Boards[0].LevelChannels.Count())
            {
                FunctionsParameterError("Condition1", step);
                return;
            }

            int skip_samples = 0;
            if (!Int32.TryParse(step.Condition2, out skip_samples))
            {
                if (string.IsNullOrEmpty(step.Condition2))
                {
                    skip_samples = 0;
                }
                else
                {
                    FunctionsParameterError("Condition2", step);
                    return;
                }
            }

            bool IsHigh = step.Oper.Contains("H");

            for (int board_idx = 0; board_idx < Boards.Count; board_idx++)
            {
                var Board = Boards[board_idx];
                if (!Board.Skip)
                {
                    List<LevelSample> samples = Board.LevelChannels[channel].Samples.Skip(skip_samples).ToList();

                    if (samples.Count > 0)
                    {
                        if (samples.Where(x => x.Level != IsHigh).Count() > 0)
                        {
                            var failChannels = samples.Where(x => x.Level != IsHigh).ToList();
                            for (int i = 0; i < failChannels.Count; i++)
                            {
                                Console.WriteLine("{0}->{1}", i, failChannels[i].Level);
                            }

                            step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "NG");
                            step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ng);
                        }
                        else
                        {
                            step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "OK");
                            step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ok);
                        }
                    }
                    else
                    {
                        step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "NG");
                        step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ng);
                    }
                }
            }
        }

        private void LEC(Step step)
        {
            int channel;
            if (!Int32.TryParse(step.Condition1, out channel))
            {
                FunctionsParameterError("Condition1", step);
                return;
            }
            channel = channel - 1;

            if (channel >= Boards[0].LevelChannels.Count())
            {
                FunctionsParameterError("Condition1", step);
                return;
            }
            if (step.Oper != "H" && step.Oper != "L")
            {
                FunctionsParameterError("Oper", step);
                return;
            }
            bool IsHigh = step.Oper == "H";

            int spect;
            double max = 0;
            double min = 0;
            bool useSpect = false;
            bool useMaxMin = false;

            if (Int32.TryParse(step.Spect, out spect))
            {
                useSpect = true;
            }

            if (Double.TryParse(step.Max, out max) && Double.TryParse(step.Min, out min))
            {
                useMaxMin = true;
            }

            int skip_samples = 0;
            if (step.Condition2 != null)
            {
                if (step.Condition2.Length >= 1)
                {
                    if (!Int32.TryParse(step.Condition2, out skip_samples))
                    {
                        if (string.IsNullOrEmpty(step.Condition2))
                        {
                            skip_samples = 0;
                        }
                        else
                        {
                            FunctionsParameterError("Condition2", step);
                            return;
                        }
                    }
                }
            }

            for (int board_idx = 0; board_idx < Boards.Count; board_idx++)
            {
                Board board = Boards[board_idx];

                if (!board.Skip)
                {
                    int countA = board.LEVEL_COUNT(IsHigh, channel, skip_samples);
                    step.ValueGet1 = countA.ToString();

                    if (useSpect == false && useMaxMin == false)
                    {
                        FunctionsParameterError("Spect/Max/Min", step);
                        return;
                    }
                    if (useSpect == false && useMaxMin == true)
                    {
                        if (countA >= min && countA <= max)
                        {
                            step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "OK");
                            step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ok);
                            return;
                        }
                        else
                        {
                            step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, countA.ToString());
                            step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ng);
                            return;
                        }
                    }

                    if (useSpect == true)
                    {
                        if (countA == spect)
                        {
                            step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "OK");
                            step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ok);
                            return;
                        }
                        else
                        {
                            step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, countA.ToString());
                            step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ng);
                            return;
                        }
                    }
                }
            }
        }

        private void LSQ(Step step)
        {
            if (!Level.SerialPort.Port.IsOpen)
            {
                FunctionsParameterError("Sys", step);
                return;
            }

            if (step.Oper != "H" && step.Oper != "L")
            {
                FunctionsParameterError("Oper", step);
                return;
            }

            bool level_set = step.Oper.Contains("H");

            int skip_samples;

            if (!Int32.TryParse(step.Condition2, out skip_samples))
            {
                if (string.IsNullOrEmpty(step.Condition2))
                {
                    skip_samples = 0;
                }
                else
                {
                    FunctionsParameterError("Condition2", step);
                    return;
                }
            }

            int std_channel;

            if (!Int32.TryParse(step.Spect, out std_channel))
            {
                if (string.IsNullOrEmpty(step.Spect))
                {
                    List<String> Channel = new List<string>();
                    if (step.Condition1.Contains("/"))
                    {
                        Channel = step.Condition1.Split('/').ToList();
                    }
                    else
                    {
                        Channel.Add(step.Condition1);
                    }
                    List<int> channel_idx_list_ordered = new List<int>();
                    foreach (var item in Channel)
                    {
                        if (item.Contains('~'))
                        {
                            int startChannel = Convert.ToInt32(item.Split('~')[0]);
                            int endChannel = Convert.ToInt32(item.Split('~')[1]);
                            for (int i = startChannel; i < endChannel; i++)
                            {
                                channel_idx_list_ordered.Add(i - 1);
                            }
                        }
                        else
                        {
                            channel_idx_list_ordered.Add(Convert.ToInt32(item) - 1);
                        }
                    }

                    if (channel_idx_list_ordered.Count >= 2)
                    {
                        for (int board_idx = 0; board_idx < Boards.Count; board_idx++)
                        {
                            Board board = Boards[board_idx];

                            if (!board.Skip)
                            {
                                List<int> corresponding_change_points = new List<int>();
                                foreach (int channel in channel_idx_list_ordered)
                                {
                                    var samples_channel = board.LevelChannels[channel].Samples;
                                    var change_point = board.FindChangePoint(samples_channel, level_set);
                                    corresponding_change_points.Add(change_point);
                                }

                                bool all_increasing = corresponding_change_points.Zip(corresponding_change_points.Skip(1), (a, b) => b > a).All(x => x);

                                if (!all_increasing)
                                {
                                    step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "NG");
                                    step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ng);
                                }
                                else
                                {
                                    step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "OK");
                                    step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ok);
                                }
                            }
                        }
                    }
                    else
                    {
                        FunctionsParameterError("Condition1", step);
                    }
                }
                else
                {
                    FunctionsParameterError("Spect", step);
                    return;
                }
            }
            else
            {
                std_channel = std_channel - 1;

                if (std_channel >= Boards[0].LevelChannels.Count())
                {
                    FunctionsParameterError("Spect", step);

                    return;
                }

                List<String> Channel = new List<string>();
                if (step.Condition1.Contains("/"))
                {
                    Channel = step.Condition1.Split('/').ToList();
                }
                else
                {
                    Channel.Add(step.Condition1);
                }
                List<int> channel_idx_list_ordered = new List<int>();
                foreach (var item in Channel)
                {
                    if (item.Contains('~'))
                    {
                        int startChannel = Convert.ToInt32(item.Split('~')[0]);
                        int endChannel = Convert.ToInt32(item.Split('~')[1]);
                        for (int i = startChannel; i < endChannel; i++)
                        {
                            channel_idx_list_ordered.Add(i - 1);
                        }
                    }
                    else
                    {
                        channel_idx_list_ordered.Add(Convert.ToInt32(item) - 1);
                    }
                }

                if (channel_idx_list_ordered.Count >= 2)
                {
                    for (int board_idx = 0; board_idx < Boards.Count; board_idx++)
                    {
                        Board board = Boards[board_idx];

                        if (!board.Skip)
                        {
                            var samples_std_channel = board.LevelChannels[std_channel].Samples.Skip(skip_samples).ToList();

                            var std_change_point = board.FindChangePoint(samples_std_channel, level_set);

                            List<int> corresponding_change_points = new List<int>();
                            foreach (int channel in channel_idx_list_ordered)
                            {
                                var samples_channel = board.LevelChannels[channel].Samples;
                                var change_point = board.FindChangePoint(samples_channel.Skip(std_change_point).ToList(), level_set);
                                corresponding_change_points.Add(change_point);
                            }

                            bool all_increasing = corresponding_change_points.Zip(corresponding_change_points.Skip(1), (a, b) => b > a).All(x => x);

                            if (!all_increasing)
                            {
                                step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "NG");
                                step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ng);
                            }
                            else
                            {
                                step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "OK");
                                step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ok);
                            }
                        }
                    }
                }
                else
                {
                    FunctionsParameterError("Condition1", step);
                }
            }
        }

        public void LTM(Step step)
        {
            int channel;
            if (!Int32.TryParse(step.Condition1, out channel))
            {
                FunctionsParameterError("Condition1", step);
                return;
            }

            channel = channel - 1;

            if (step.Oper != "HL" && step.Oper != "LH" && step.Oper != "LL" && step.Oper != "HH")
            {
                FunctionsParameterError("Oper", step);
                return;
            }

            int skip_samples;

            if (!Int32.TryParse(step.Condition2, out skip_samples))
            {
                if (string.IsNullOrEmpty(step.Condition2))
                {
                    skip_samples = 0;
                }
                else
                {
                    FunctionsParameterError("Condition2", step);
                    return;
                }
            }

            //int spect;
            //if (!Int32.TryParse(step.Spect, out spect))
            //{
            //    FunctionsParameterError("Spect", step);
            //    return;
            //}

            double max;
            if (!Double.TryParse(step.Max, out max))
            {
                if (string.IsNullOrEmpty(step.Max))
                {
                    max = Double.MaxValue;
                }
                else
                {
                    FunctionsParameterError("Max", step);
                    return;
                }
            }

            double min;
            if (!Double.TryParse(step.Min, out min))
            {
                FunctionsParameterError("Min", step);
                return;
            }

            for (int board_idx = 0; board_idx < Boards.Count; board_idx++)
            {
                Board board = Boards[board_idx];
                if (!board.Skip)
                {
                    var samples_channel = board.LevelChannels[channel].Samples.Skip(skip_samples).ToList();

                    bool flag_begin = true;

                    int duration = 0;

                    bool state_start = board.CharToBool(step.Oper[0]);
                    bool state_end = board.CharToBool(step.Oper[1]);

                    if (state_start != state_end)
                    {
                        for (int idx = 0; idx < samples_channel.Count; idx++)
                        {
                            if (flag_begin)
                            {
                                if (samples_channel[idx].Level == board.CharToBool(step.Oper[0]))
                                {
                                    flag_begin = false;
                                }
                            }
                            else
                            {
                                if (samples_channel[idx].Level == board.CharToBool(step.Oper[1]))
                                {
                                    duration++;
                                    break;
                                }
                                else
                                {
                                    duration++;
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int idx = 0; idx < samples_channel.Count; idx++)
                        {
                            if (flag_begin)
                            {
                                if (samples_channel[idx].Level == board.CharToBool(step.Oper[0]))
                                {
                                    flag_begin = false;
                                }
                            }
                            else
                            {
                                if (samples_channel[idx].Level == board.CharToBool(step.Oper[0]))
                                {
                                    duration++;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }

                    if (duration < min || duration > max)
                    {
                        step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, duration.ToString());
                        step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ng);
                    }
                    else
                    {
                        step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "OK");
                        step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ok);
                    }
                }
            }
        }

        private void DLY(Step step)
        {
            bool SetOK = false;
            int dlyTime = 0;
            if (int.TryParse(step.Oper, out int delayTime))
            {
                dlyTime = delayTime;
                SetOK = true;
                if (delayTime > 100)
                {
                    int delay = 0;
                    while (delay + 100 <= delayTime)
                    {
                        Task.Delay(90).Wait();
                        delay += 100;
                        step.ValueGet1 = delay.ToString();
                        step.ValueGet2 = delay.ToString();
                        step.ValueGet3 = delay.ToString();
                        step.ValueGet4 = delay.ToString();
                    }
                }
                else
                {
                    Task.Delay(delayTime).Wait();
                }
            }

            for (int board_idx = 0; board_idx < Boards.Count; board_idx++)
            {
                var Board = Boards[board_idx];

                if (!Board.Skip)
                {
                    if (!SetOK)
                    {
                        step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "NG");
                        step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ng);
                    }
                    else
                    {
                        step.GetType().GetProperty($"ValueGet{board_idx + 1}").SetValue(step, "OK");
                        step.GetType().GetProperty($"Result{board_idx + 1}").SetValue(step, Step.Ok);
                    }
                }
            }
        }

        private void PWR(Step step)
        {
            bool IsON = step.Oper == "ON";
            bool Is220V = step.Condition1 == "220VAC";
            bool Is110V = step.Condition1 == "110VAC";

            if (!System.System_Board.SerialPort.Port.IsOpen)
            {
                FunctionsParameterError("Sys", step);
                return;
            }

            switch (Boards.Count)
            {
                case 1:
                    if (!Boards[0].Skip)
                    {
                        if (Is110V)
                            System.System_Board.MachineIO.AC110 = IsON;
                        if (Is220V)
                            System.System_Board.MachineIO.AC220 = IsON;
                        System.System_Board.SendControl();

                        step.ValueGet1 = step.Oper.ToString();
                        step.Result1 = Step.Ok;
                    }
                    break;

                case 2:
                    if (!Boards[0].Skip)
                    {
                        if (Is110V)
                            System.System_Board.MachineIO.AC110 = IsON;
                        if (Is220V)
                            System.System_Board.MachineIO.AC220 = IsON;

                        step.ValueGet1 = step.Oper.ToString();
                        step.Result1 = Step.Ok;
                    }

                    if (!Boards[1].Skip)
                    {
                        if (Is110V)
                            System.System_Board.MachineIO.BC110 = IsON;
                        if (Is220V)
                            System.System_Board.MachineIO.BC220 = IsON;

                        step.ValueGet2 = step.Oper.ToString();
                        step.Result2 = Step.Ok;
                    }

                    System.System_Board.SendControl();
                    break;

                case 3:
                    if (!Boards[0].Skip)
                    {
                        if (Is110V)
                            System.System_Board.MachineIO.AC110 = IsON;
                        if (Is220V)
                            System.System_Board.MachineIO.AC220 = IsON;

                        step.ValueGet1 = step.Oper.ToString();
                        step.Result1 = Step.Ok;
                    }

                    if (!Boards[1].Skip)
                    {
                        if (Is110V)
                            System.System_Board.MachineIO.BC110 = IsON;
                        if (Is220V)
                            System.System_Board.MachineIO.BC220 = IsON;

                        step.ValueGet2 = step.Oper.ToString();
                        step.Result2 = Step.Ok;
                    }
                    if (!Boards[2].Skip)
                    {
                        if (Is110V)
                            System.System_Board.MachineIO.AC110 = IsON;
                        if (Is220V)
                            System.System_Board.MachineIO.AC220 = IsON;

                        step.ValueGet3 = step.Oper.ToString();
                        step.Result3 = Step.Ok;
                    }
                    System.System_Board.SendControl();
                    break;

                case 4:
                    if (!Boards[0].Skip)
                    {
                        if (Is110V)
                            System.System_Board.MachineIO.AC110 = IsON;
                        if (Is220V)
                            System.System_Board.MachineIO.AC220 = IsON;

                        step.ValueGet1 = step.Oper.ToString();
                        step.Result1 = Step.Ok;
                    }

                    if (!Boards[1].Skip)
                    {
                        if (Is110V)
                            System.System_Board.MachineIO.BC110 = IsON;
                        if (Is220V)
                            System.System_Board.MachineIO.BC220 = IsON;

                        step.ValueGet2 = step.Oper.ToString();
                        step.Result2 = Step.Ok;
                    }
                    if (!Boards[2].Skip)
                    {
                        if (Is110V)
                            System.System_Board.MachineIO.AC110 = IsON;
                        if (Is220V)
                            System.System_Board.MachineIO.AC220 = IsON;

                        step.ValueGet3 = step.Oper.ToString();
                        step.Result3 = Step.Ok;
                    }

                    if (!Boards[4].Skip)
                    {
                        if (Is110V)
                            System.System_Board.MachineIO.BC110 = IsON;
                        if (Is220V)
                            System.System_Board.MachineIO.BC220 = IsON;

                        step.ValueGet4 = step.Oper.ToString();
                        step.Result4 = Step.Ok;
                    }
                    System.System_Board.SendControl();
                    break;

                default:
                    break;
            }
        }

        private void DIS(Step step)
        {
            if (System.System_Board.SerialPort.Port.IsOpen)
            {
                if (step.Condition1 == "ON")
                {
                    System.System_Board.MachineIO.ADSC = true;
                    System.System_Board.MachineIO.BDSC = true;
                }
                else
                {
                    System.System_Board.MachineIO.ADSC = false;
                    System.System_Board.MachineIO.BDSC = false;
                }

                System.System_Board.SendControl();
                if (Boards.Count >= 1) if (!Boards[0].Skip) step.ValueGet1 = "exe";
                if (Boards.Count >= 2) if (!Boards[1].Skip) step.ValueGet2 = "exe";
                if (Boards.Count >= 3) if (!Boards[2].Skip) step.ValueGet3 = "exe";
                if (Boards.Count >= 4) if (!Boards[3].Skip) step.ValueGet4 = "exe";
            }
            else
            {
                FunctionsParameterError("sys", step);
            }
        }

        public void MOT(Step step)
        {
            //EncoderViewer.LineValues.Clear();

            //for (int i = 0; i < 400; i++)
            //{

            //    if (EncoderViewer.LineValues.Count >= EncoderViewer.WindowSize)
            //    {
            //        EncoderViewer.LineValues.Clear();
            //    }

            //    EncoderViewer.LineValues.Add(900 - 900 * Math.Cos((float)i * 2 * Math.PI / 400));

            //    Task.Delay(10).Wait(); ;  // 100 ms delay between readings
            //}
            //return;

            EncoderViewer.LineValues.Clear();


            if (!CounterTimer.SerialPort.Port.IsOpen)
            {
                FunctionsParameterError("sys", step);
            }

            #region RPM

            if (step.Condition1 == "RPM")
            {
                MotorSpeedCalculator motorSpeedCalculator = new MotorSpeedCalculator(600);

                double minValue = 0;
                double maxValue = 0;

                if (!Double.TryParse(step.Min, out minValue))
                {
                    if (step.Min == "")
                    {
                        minValue = Double.MinValue;
                    }
                    else
                    {
                        FunctionsParameterError("Min", step);
                        return;
                    }
                }

                if (!Double.TryParse(step.Max, out maxValue))
                {
                    if (step.Max == "")
                    {
                        maxValue = Double.MaxValue;
                    }
                    else
                    {
                        FunctionsParameterError("Max", step);
                        return;
                    }
                }

                int channel = 1;
                if (!Int32.TryParse(step.Oper, out channel))
                {
                    FunctionsParameterError("oper", step);
                    return;
                }
                else
                {
                    if (channel > 2)
                    {
                        FunctionsParameterError("oper > 2", step);
                        return;
                    }
                    if (channel <= 0)
                    {
                        FunctionsParameterError("oper <= 0", step);
                        return;
                    }
                }

                string spin_direction_set = step.Condition2;

                if (int.TryParse(step.Spect, out int scanTime))
                {
                    int currentDirection = 0;
                    int previousDirection = 0;
                    bool initialChecking = true;

                    bool ACW_CW_result = false;
                    bool CW_result = false;
                    bool ACW_result = false;


                    motorSpeedCalculator.SpeedHistory.Clear();
                    CounterTimer.Reset();
                    EncoderViewer.LineValues.Clear();

                    DateTime start = DateTime.Now;
                    while (DateTime.Now.Subtract(start).TotalMilliseconds < scanTime)
                    {
                        if (CounterTimer.Read())
                        {

                            // Call the RPM calculation method with current position
                            double rpm = motorSpeedCalculator.CalculateRPM(CounterTimer.Position);
                            motorSpeedCalculator.SpeedHistory.Add(rpm);


                            if (EncoderViewer.LineValues.Count >= EncoderViewer.WindowSize)
                            {
                                EncoderViewer.LineValues.Clear();
                            }

                            EncoderViewer.LineValues.Add(rpm);

                            //if (EncoderViewer.LineValues.Count > EncoderViewer.WindowSize)
                            //{

                            //EncoderViewer.LineValues.RemoveAt(0);
                            //EncoderViewer.AxisMin++;
                            //EncoderViewer.AxisMax++;
                            //}


                            // Display the calculated RPM
                            Console.WriteLine($"Motor Posistion: {CounterTimer.Position} ");

                            Console.WriteLine($"Motor RPM: {rpm:F2} RPM");

                            // Simulate a delay (real-time delay between position readings)

                            if (spin_direction_set == "CW")
                            {

                                step.ValueGet1 = rpm.ToString("F2");
                                if (GetSign(rpm) == 1 && minValue <= rpm && maxValue >= rpm)
                                {
                                    CW_result = true;
                                    // exit executing immediately !!!
                                    break;
                                }
                            }
                            if (spin_direction_set == "ACW")
                            {

                                step.ValueGet1 = rpm.ToString("F2");
                                if (GetSign(rpm) == -1 && minValue <= rpm && maxValue >= rpm)
                                {
                                    ACW_result = true;
                                    // exit executing immediately !!!
                                    break;
                                }
                            }

                            if (spin_direction_set == "CW/ACW")
                            {
                                step.ValueGet1 = rpm.ToString("F2");

                                if (minValue <= Math.Abs(rpm) && maxValue >= Math.Abs(rpm))
                                {
                                    ACW_CW_result = true;
                                    // exit executing immediately !!!
                                    break;
                                }

                                //if (initialChecking)
                                //{
                                //    currentDirection = GetSign(rpm);
                                //    previousDirection = currentDirection;

                                //    if (currentDirection != 0)
                                //    {
                                //        initialChecking = false;
                                //    }
                                //}
                                //else
                                //{
                                //    currentDirection = GetSign(rpm);
                                //    if (currentDirection != previousDirection)
                                //    {
                                //        ACW_CW_result = true;
                                //        // exit executing immediately !!!
                                //        break;
                                //    }
                                //    previousDirection = currentDirection;
                                //}
                            }
                        }
                        else
                        {
                            FunctionsParameterError("sys", step);
                            return;
                        }
                        if (!CounterTimer.Reset())
                        {
                            FunctionsParameterError("sys", step);
                            return;
                        }

                        Task.Delay(100).Wait();  // 100 ms delay between readings



                    }

                    if (motorSpeedCalculator.SpeedHistory.All(item => item == 0))
                    {
                        step.ValueGet1 = "No Rotation";
                        step.Result1 = Step.Ng;
                        return;
                    }
                    if (spin_direction_set == "CW")
                    {
                        if (CW_result)
                        {
                            step.ValueGet1 = "OK";
                            step.Result1 = Step.Ok;
                            return;
                        }
                        else
                        {
                            step.ValueGet1 = "NG";
                            step.Result1 = Step.Ng;
                            return;
                        }
                    }
                    if (spin_direction_set == "ACW")
                    {
                        if (ACW_result)
                        {
                            step.ValueGet1 = "OK";
                            step.Result1 = Step.Ok;
                            return;
                        }
                        else
                        {
                            step.ValueGet1 = "NG";
                            step.Result1 = Step.Ng;
                            return;
                        }
                    }
                    if (spin_direction_set == "CW/ACW")
                    {
                        if (ACW_CW_result)
                        {
                            step.ValueGet1 = "OK";
                            step.Result1 = Step.Ok;
                            return;
                        }
                        else
                        {
                            step.ValueGet1 = "NG";
                            step.Result1 = Step.Ng;
                            return;
                        }
                    }
                }
                else
                {
                    FunctionsParameterError("Spect", step);
                    return;
                }
            }

            #endregion RPM

            #region READ

            if (step.Condition1 == "READ")
            {
                foreach (var powerMettterValueHolder in PowerMetter.ValueHolders)
                {
                    powerMettterValueHolder.ClearValueCollection();
                }
                if (int.TryParse(step.Spect, out int scanTime))
                {
                    DateTime start = DateTime.Now;
                    while (DateTime.Now.Subtract(start).TotalMilliseconds < scanTime)
                    {
                        if (Boards.Count >= 1)
                        {
                            if (!Boards[0].Skip)
                            {
                                if (PowerMetter.Read('A'))
                                {
                                    step.ValueGet1 = "exe";
                                    step.Result1 = Step.Ok;
                                }
                                else
                                {
                                    step.ValueGet1 = "sys";
                                    step.Result1 = Step.Ng;
                                }
                            }
                        }

                        if (Boards.Count >= 2)
                        {
                            if (!Boards[1].Skip)
                            {
                                if (PowerMetter.Read('B'))
                                {
                                    step.ValueGet2 = "exe";
                                    step.Result2 = Step.Ok;
                                }
                                else
                                {
                                    step.ValueGet2 = "sys";
                                    step.Result2 = Step.Ng;
                                }
                            }
                        }

                        //if (Boards.Count >= 3) if (!Boards[2].Skip) if (PowerMetter.Read('C')) step.ValueGet3 = "exe"; else { step.ValueGet3 = "sys"; step.Result3 = Step.Ng; }
                        //if (Boards.Count >= 4) if (!Boards[3].Skip) if (PowerMetter.Read('D')) step.ValueGet4 = "exe"; else { step.ValueGet4 = "sys"; step.Result4 = Step.Ng; }
                    }
                }
            }
            else
            {
                double minValue = 0;
                double maxValue = 0;

                if (!Double.TryParse(step.Min, out minValue))
                {
                    if (step.Min == "")
                    {
                        minValue = Double.MinValue;
                    }
                    else
                    {
                        FunctionsParameterError("Min", step);
                        return;
                    }
                }

                if (!Double.TryParse(step.Max, out maxValue))
                {
                    if (step.Max == "")
                    {
                        maxValue = Double.MaxValue;
                    }
                    else
                    {
                        FunctionsParameterError("Max", step);
                        return;
                    }
                }

                //"READ", "CMP UU", "CMP UW", "CMP UV", "CMP UUW", "CMP UWV", "CMP UVU", "CMP IU", "CMP IW", "CMP IV"
                switch (step.Condition1)
                {
                    case "CMP Voltage U":

                        if (Boards.Count >= 1)
                        {
                            step.ValueGet1 = PowerMetter.ValueHolders.Select(x => x.Voltage_U_Collection).ToArray()[0].Max().ToString("N2");

                            if (CheckStepMinMax(step, PowerMetter.ValueHolders.Select(x => x.Voltage_U_Collection).ToArray()[0], minValue, maxValue))
                            {
                                step.Result1 = Step.Ok;
                            }
                            else
                            {
                                step.Result1 = Step.Ng;
                            }
                        }
                        break;

                    case "CMP Voltage W":
                        if (Boards.Count >= 1)
                        {
                            step.ValueGet1 = PowerMetter.ValueHolders.Select(x => x.Voltage_W_Collection).ToArray()[0].Max().ToString("N2");

                            if (CheckStepMinMax(step, PowerMetter.ValueHolders.Select(x => x.Voltage_W_Collection).ToArray()[0], minValue, maxValue))
                            {
                                step.Result1 = Step.Ok;
                            }
                            else
                            {
                                step.Result1 = Step.Ng;
                            }
                        }

                        break;

                    case "CMP Voltage V":
                        if (Boards.Count >= 1)
                        {
                            step.ValueGet1 = PowerMetter.ValueHolders.Select(x => x.Voltage_V_Collection).ToArray()[0].Max().ToString("N2");

                            if (CheckStepMinMax(step, PowerMetter.ValueHolders.Select(x => x.Voltage_V_Collection).ToArray()[0], minValue, maxValue))
                            {
                                step.Result1 = Step.Ok;
                            }
                            else
                            {
                                step.Result1 = Step.Ng;
                            }
                        }
                        break;

                    case "CMP Voltage UW":
                        if (Boards.Count >= 1)
                        {
                            step.ValueGet1 = PowerMetter.ValueHolders.Select(x => x.Voltage_UW_Collection).ToArray()[0].Max().ToString("N2");
                            if (CheckStepMinMax(step, PowerMetter.ValueHolders.Select(x => x.Voltage_UW_Collection).ToArray()[0], minValue, maxValue))
                            {
                                step.Result1 = Step.Ok;
                            }
                            else
                            {
                                step.Result1 = Step.Ng;
                            }
                        }
                        break;

                    case "CMP Voltage WV":
                        if (Boards.Count >= 1)
                        {
                            step.ValueGet1 = PowerMetter.ValueHolders.Select(x => x.Voltage_WV_Collection).ToArray()[0].Max().ToString("N2");
                            if (CheckStepMinMax(step, PowerMetter.ValueHolders.Select(x => x.Voltage_WV_Collection).ToArray()[0], minValue, maxValue))
                            {
                                step.Result1 = Step.Ok;
                            }
                            else
                            {
                                step.Result1 = Step.Ng;
                            }
                        }
                        break;

                    case "CMP Voltage VU":
                        if (Boards.Count >= 1)
                        {
                            step.ValueGet1 = PowerMetter.ValueHolders.Select(x => x.Voltage_VU_Collection).ToArray()[0].Max().ToString("N2");
                            if (CheckStepMinMax(step, PowerMetter.ValueHolders.Select(x => x.Voltage_VU_Collection).ToArray()[0], minValue, maxValue))
                            {
                                step.Result1 = Step.Ok;
                            }
                            else
                            {
                                step.Result1 = Step.Ng;
                            }
                        }
                        break;

                    case "CMP Current U":
                        if (Boards.Count >= 1)
                        {
                            step.ValueGet1 = PowerMetter.ValueHolders.Select(x => x.Current_U_Collection).ToArray()[0].Max().ToString("N2");
                            if (CheckStepMinMax(step, PowerMetter.ValueHolders.Select(x => x.Current_U_Collection).ToArray()[0], minValue, maxValue))
                            {
                                step.Result1 = Step.Ok;
                            }
                            else
                            {
                                step.Result1 = Step.Ng;
                            }
                        }
                        break;

                    case "CMP Current W":
                        if (Boards.Count >= 1)
                        {
                            step.ValueGet1 = PowerMetter.ValueHolders.Select(x => x.Current_W_Collection).ToArray()[0].Max().ToString("N2");

                            if (CheckStepMinMax(step, PowerMetter.ValueHolders.Select(x => x.Current_W_Collection).ToArray()[0], minValue, maxValue))
                            {
                                step.Result1 = Step.Ok;
                            }
                            else
                            {
                                step.Result1 = Step.Ng;
                            }
                        }
                        break;

                    case "CMP Current V":
                        if (Boards.Count >= 1)
                        {
                            step.ValueGet1 = PowerMetter.ValueHolders.Select(x => x.Current_V_Collection).ToArray()[0].Max().ToString("N2");

                            if (CheckStepMinMax(step, PowerMetter.ValueHolders.Select(x => x.Current_V_Collection).ToArray()[0], minValue, maxValue))
                            {
                                step.Result1 = Step.Ok;
                            }
                            else
                            {
                                step.Result1 = Step.Ng;
                            }
                        }
                        break;

                    default:
                        FunctionsParameterError("condition", step);
                        break;
                }
            }

            #endregion READ
        }
        private int GetSign(double value)
        {
            int sign = 0;

            if (value < 0)
            {
                sign = -1;
            }
            if (value > 0)
            {
                sign = 1;
            }

            return sign;
        }

        private bool CheckStepMinMax(Step step, List<double> values, double minValue, double maxValue)
        {
            bool result = false;

            //step.ValueGet1 = value.ToString("N3");
            if (values.Max() >= minValue && values.Max() <= maxValue)
            {
                result = true;
            }

            return result;
        }

        public void END(Step step)
        {
            System.System_Board.PowerRelease();
            Relay.Card.Release();
            Solenoid.Card.Release();
            MuxCard.Card.ReleaseChannels();
        }

        private void FunctionsParameterError(string nameOfFunc, Step step)
        {
            switch (Boards.Count)
            {
                case 1:
                    if (!Boards[0].Skip)
                    {
                        step.ValueGet1 = nameOfFunc;
                        step.Result1 = Step.Ng;
                    }
                    break;

                case 2:
                    if (!Boards[0].Skip)
                    {
                        step.ValueGet1 = nameOfFunc;
                        step.Result1 = Step.Ng;
                    }

                    if (!Boards[1].Skip)
                    {
                        step.ValueGet2 = nameOfFunc;
                        step.Result2 = Step.Ng;
                    }
                    break;

                case 3:
                    if (!Boards[0].Skip)
                    {
                        step.ValueGet1 = nameOfFunc;
                        step.Result1 = Step.Ng;
                    }

                    if (!Boards[1].Skip)
                    {
                        step.ValueGet2 = nameOfFunc;
                        step.Result2 = Step.Ng;
                    }
                    if (!Boards[2].Skip)
                    {
                        step.ValueGet3 = nameOfFunc;
                        step.Result3 = Step.Ng;
                    }
                    break;

                case 4:
                    if (!Boards[0].Skip)
                    {
                        step.ValueGet1 = nameOfFunc;
                        step.Result1 = Step.Ng;
                    }

                    if (!Boards[1].Skip)
                    {
                        step.ValueGet2 = nameOfFunc;
                        step.Result2 = Step.Ng;
                    }
                    if (!Boards[2].Skip)
                    {
                        step.ValueGet3 = nameOfFunc;
                        step.Result3 = Step.Ng;
                    }
                    if (!Boards[3].Skip)
                    {
                        step.ValueGet4 = nameOfFunc;
                        step.Result4 = Step.Ng;
                    }
                    break;

                default:
                    break;
            }
        }

        #endregion Functions Code

        private bool StepTestResult(Step step)
        {
            bool isOk = true;
            switch (Boards.Count)
            {
                case 1:
                    if (!Boards[0].Skip) isOk = isOk && step.Result1 != Step.Ng;
                    return isOk;

                case 2:
                    if (!Boards[0].Skip) isOk = isOk && step.Result1 != Step.Ng;
                    if (!Boards[1].Skip) isOk = isOk && step.Result2 != Step.Ng;
                    return isOk;

                case 3:
                    if (!Boards[0].Skip) isOk = isOk && step.Result1 != Step.Ng;
                    if (!Boards[1].Skip) isOk = isOk && step.Result2 != Step.Ng;
                    if (!Boards[2].Skip) isOk = isOk && step.Result3 != Step.Ng;
                    return isOk;

                case 4:
                    if (!Boards[0].Skip) isOk = isOk && step.Result1 != Step.Ng;
                    if (!Boards[1].Skip) isOk = isOk && step.Result2 != Step.Ng;
                    if (!Boards[2].Skip) isOk = isOk && step.Result3 != Step.Ng;
                    if (!Boards[3].Skip) isOk = isOk && step.Result4 != Step.Ng;
                    return isOk;

                default:
                    return false;
            }
        }
    }
}