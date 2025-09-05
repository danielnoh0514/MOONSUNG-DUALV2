using HVT.VTM.Base;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;

namespace HVT.VTM.Program
{
    public class FolderMap
    {
        private static string rootFolder;

        public static string RootFolder
        {
            get { return rootFolder; }
            set
            {
                rootFolder = value;
            }
        }

        public const string SettingFolder = @"\Setting";
        public const string ModelFolder = @"\Model";
        public string HistoryFolder = @"\History\" + DateTime.Now.ToString(@"yyyy\\MM");
        public const string MESFolder = @"\MES";
        public const string PCBFolder = @"\PCB";
        public const string logFolder = "log";

        public const string DefaultModelFileExt = ".vmdl";
        public const string DefaultTxFileExt = ".vtx";
        public const string DefaultRxFileExt = ".vrx";
        public const string DefaultQrFileExt = ".vqr";
        public const string DefaultLogFileExt = ".vlog";

        public string LogDir = "";
        public string NgLogDir = "";

        public void TryCreatFolderMap()
        {
            RootFolder = "C:\\";
            if (!Directory.Exists(RootFolder)) Directory.CreateDirectory(RootFolder);
            if (!Directory.Exists(RootFolder + logFolder)) Directory.CreateDirectory(RootFolder + logFolder);
            //if (!Directory.Exists(RootFolder + ModelFolder)) Directory.CreateDirectory(RootFolder + ModelFolder);
            if (!Directory.Exists(RootFolder + HistoryFolder)) Directory.CreateDirectory(RootFolder + HistoryFolder);
            //if (!Directory.Exists(RootFolder + MESFolder)) Directory.CreateDirectory(RootFolder + MESFolder);
            //if (!Directory.Exists(RootFolder + PCBFolder)) Directory.CreateDirectory(RootFolder + PCBFolder);
            Console.WriteLine(String.Format("{0}\\{1}", HistoryFolder, DateTime.Now.Date.ToString("dd") + ".vtmh"));
        }

        public static List<ModelLoaded> ModelLoadeds = new List<ModelLoaded>();

        public static void GetListModelsLoaded()
        {
            if (File.Exists(RootFolder + SettingFolder + "\\models.ld"))
            {
                ModelLoadeds.Clear();
                var strModel = File.ReadAllLines(RootFolder + SettingFolder + "\\models.ld");
                foreach (var item in strModel)
                {
                    ModelLoadeds.Add(new ModelLoaded() { Path = item });
                }
            }
        }

        public static void SaveListModelLoaded()
        {
            if (File.Exists(RootFolder + SettingFolder + "\\models.ld")) File.Delete(RootFolder + SettingFolder + "\\models.ld");
            for (int i = 0; i < 10; i++)
            {
                if (i < ModelLoadeds.Count)
                {
                    using (StreamWriter writer = File.AppendText(RootFolder + SettingFolder + "\\models.ld"))
                    {
                        writer.WriteLine(ModelLoadeds[i].Path);
                    }
                }
            }
        }

        public void SaveHistory(object HistoryObject)
        {
            Console.WriteLine(String.Format("{0}\\{1}", RootFolder + HistoryFolder, DateTime.Now.Date.ToString("dd") + ".vtmh"));
            File.AppendAllText(String.Format("{0}\\{1}", RootFolder + HistoryFolder, DateTime.Now.Date.ToString("dd") + ".vtmh"), HVT.Utility.Extensions.ConvertToJson(HistoryObject) + Environment.NewLine);
        }


        public void SaveLogFile(bool is_final_result_fail, object HistoryObject, int stepTesting, bool pass)
        {
            try
            {
                var item = (HistoryObject as Board);
                // != 0 ? fail : ok
                int stepNum = 1;

                if (stepTesting != 0)
                {
                    stepNum = item.TestStep.Count - stepTesting != 0 ? stepTesting + 1 : item.TestStep.Count;
                }

                string dateToday = DateTime.Now.ToString("yyyyMMdd").ToString();
                string baseDir = "";
                if (pass)
                {
                    baseDir = LogDir;
                }
                else
                {
                    baseDir = NgLogDir;
                }
                string fileName = item.Barcode + "_" + item.StartTest.ToString("yyyy/MM/dd HH:mm:ss");
                fileName = fileName.Replace("-", "").Replace(":", "").Replace(" ", "").Replace("/", "");
                string dir = baseDir + "\\" + fileName + ".txt";

                List<string> DatasExport = new List<string>();

                if (!Directory.Exists(baseDir))
                {
                    Directory.CreateDirectory(baseDir);
                }

                for (int i = 0; i < stepNum; i++)
                {
                    if (item.TestStep[i].Skip)
                    {
                        continue;
                    }

                    string condition1 = item.TestStep[i].Condition1;
                    string oper = item.TestStep[i].Oper;
                    string measure = "";
                    string min = "";
                    string max = "";
                    string spec = item.TestStep[i].Spect;

                    if (condition1 != null)
                    {
                        condition1 = condition1.Replace("/", ",");
                    }

                    if (item.TestStep[i].CMD == CMDs.DLY.ToString())
                    {
                        spec = "OK";
                        min = "OK";
                        max = "OK";
                        measure = "UNIT";
                    }
                    if (item.TestStep[i].CMD == CMDs.KEY.ToString() || item.TestStep[i].CMD == CMDs.PWR.ToString())
                    {
                        measure = "UNIT";
                        spec = oper;
                        min = spec;
                        max = spec;
                    }

                    if (item.TestStep[i].CMD == CMDs.STL.ToString() || item.TestStep[i].CMD == CMDs.EDL.ToString() || item.TestStep[i].CMD == CMDs.RLY.ToString())
                    {
                        measure = "UNIT";
                        spec = "exe";
                        min = "exe";
                        max = "exe";
                    }

                    if (item.TestStep[i].CMD == CMDs.LCD.ToString() || item.TestStep[i].CMD == CMDs.LED.ToString() || item.TestStep[i].CMD == CMDs.GLED.ToString() || item.TestStep[i].CMD == CMDs.FND.ToString())
                    {
                        spec = item.TestStep[i].Oper;
                        min = item.TestStep[i].Oper;
                        max = item.TestStep[i].Oper;
                        measure = "UNIT";
                    }

                    if (item.TestStep[i].CMD == CMDs.LEC.ToString() || item.TestStep[i].CMD == CMDs.LCC.ToString() || item.TestStep[i].CMD == CMDs.LSQ.ToString() || item.TestStep[i].CMD == CMDs.LTM.ToString())
                    {
                        spec = "OK";
                        min = "OK";
                        max = "OK";
                        measure = "UNIT";
                    }

                    if (item.TestStep[i].CMD == CMDs.CAM.ToString())
                    {
                        if (item.TestStep[i].ValueGet1 == "condition" || item.TestStep[i].ValueGet1 == "Oper")
                        {
                            item.TestStep[i].ValueGet1 = "OFF";
                            item.TestStep[i].Result1 = "NG";
                        }
                        else
                        {
                            item.TestStep[i].ValueGet1 = "ON";
                            item.TestStep[i].Result1 = "OK";
                        }
                    }

                    if (item.TestStep[i].CMD == CMDs.LED.ToString())
                    {
                        if (item.TestStep[i].Result1 == string.Empty)
                        {
                            item.TestStep[i].ValueGet1 = "";
                            item.TestStep[i].Result1 = "NG";
                        }
                    }

                    if (item.TestStep[i].CMD == CMDs.BUZ.ToString())
                    {
                        if (item.TestStep[i].Oper == "READ")
                        {
                            min = item.TestStep[i].Min;
                            max = item.TestStep[i].Max;
                            spec = GetAverageValue(min, max);
                            measure = "RAW";
                        }
                        if (item.TestStep[i].Oper == "START")
                        {
                            spec = "exe";
                            max = "exe";
                            min = "exe";
                            measure = "UNIT";
                        }
                    }


                    if (item.TestStep[i].CMD == CMDs.MIC.ToString())
                    {
                        if (item.TestStep[i].Oper == "READ")
                        {
                            min = item.TestStep[i].Min;
                            max = item.TestStep[i].Max;
                            spec = GetAverageValue(min, max);
                            measure = "RAW";
                        }
                        if (item.TestStep[i].Oper == "START")
                        {
                            spec = "exe";
                            max = "exe";
                            min = "exe";
                            measure = "UNIT";
                        }
                    }


                    if (item.TestStep[i].CMD == CMDs.MOT.ToString())
                    {
                        if (item.TestStep[i].Condition1 == "READ")
                        {
                            measure = "UNIT";
                            spec = "exe";
                            min = "exe";
                            max = "exe";
                        }
                        else
                        {
                            measure = "V";
                            min = item.TestStep[i].Min;
                            max = item.TestStep[i].Max;
                            spec = GetAverageValue(min, max);
                        }
                    }

                if (item.TestStep[i].CMD == CMDs.SEV.ToString())
                    {
                        if (condition1 == "RPM")
                        {
                            measure = "RPM";
                            min = item.TestStep[i].Min;
                            max = item.TestStep[i].Max;
                            spec = GetAverageValue(min, max);
                        }
                        else
                        {
                            if (item.TestStep[i].Condition1 == "Icon")
                            {
                                spec = "11";
                                min = spec;
                                max = spec;
                                measure = "UNIT";
                            }
                            else
                            {
                                measure = "UNIT";
                                min = spec;
                                max = spec;
                            }
                        }
                    }

                    string exportData = String.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}|{16}|{17}",
                        item.TestStep[i].No.ToString("D4"),
                        item.Barcode,
                        "DP FCT1",
                        item.StartTest.ToString("yyyy-MM-dd HH:mm:ss"),
                        item.TestStep[i].CMD,
                        item.TestStep[i].ValueGet1,
                        measure,
                        spec,
                        measure,
                        max,
                        measure,
                        min,
                        measure,
                        "",
                        "",
                        "",
                        item.TestStep[i].Result1,
                        is_final_result_fail ? "NG" : "OK"
                        );

                    DatasExport.Add(exportData);
                }

                File.AppendAllLines(dir, DatasExport.ToArray());
            }
            catch (Exception e)
            {
                MessageBox.Show("Crash" + e.Message, "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetAverageValue(string min, string max)
        {
            int minInt = 0;
            int maxInt = 0;
            int avg = 0;

            if (int.TryParse(min, out minInt) && int.TryParse(max, out maxInt))
            {
                avg = (minInt + maxInt) / 2;
            }
            else
            {
                return "";
            }

            return avg.ToString();
        }
    }
}