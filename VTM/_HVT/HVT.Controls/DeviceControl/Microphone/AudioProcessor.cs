using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MathNet.Numerics; // Thư viện rất mạnh cho xử lý tín hiệu số. Cài qua NuGet: `Install-Package MathNet.Numerics`
using MathNet.Numerics.IntegralTransforms; // Cho FFT
using NAudio.Wave;

using System.Numerics;
using LiveCharts;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Python.Runtime;
using System.Linq.Expressions;

namespace HVT.Controls
{


    public class AudioTester
    {

        private dynamic model;
        private dynamic tf;
        private dynamic np;
        private dynamic aiCore;

        public  bool ResultPredict =false;


        IntPtr pythonThread;

        public void InitializePython(string version)
        {
            string pythonScriptDir = Path.Combine(AppContext.BaseDirectory, "aicore");
            string pythonHomePath = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Programs\\Python\\Python{version}"; 
            string[] dirs = { $"{pythonHomePath}\\DLLs", $"{pythonHomePath}\\Lib", $"{pythonHomePath}\\Lib\\site-packages", pythonScriptDir };
            var pythonPaths = string.Join(";", dirs);
            string pythonDll = Path.Combine(pythonHomePath, $"python{version}.dll");

            Runtime.PythonDLL = pythonDll;
            PythonEngine.PythonHome = pythonHomePath;
            //Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", pythonDll);
            //Environment.SetEnvironmentVariable("PYTHONHOME", pythonHomePath);
            PythonEngine.PythonPath = pythonPaths;
            //Environment.SetEnvironmentVariable("PYTHONPATH", pythonPaths);

            PythonEngine.Initialize();
            pythonThread = PythonEngine.BeginAllowThreads();

        }
        private void PythonExit()
        {
            PythonEngine.EndAllowThreads(pythonThread);
            PythonEngine.Shutdown();
        }

        public AudioTester(string workingDir)
        { 

            try
            {
                InitializePython("313");

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            try
            {
                LoadModules();
                LoadModel(Path.Combine(workingDir, "model.keras"));


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

         

        }

        public void LoadModules()
        {
            using (Py.GIL())
            {
                np = Py.Import("numpy");
                aiCore = Py.Import("aicore");
                tf = Py.Import("tensorflow");
            }
        }

        public void LoadModel(string path)
        {
            using (Py.GIL())
            {
                model = tf.keras.models.load_model(path);
                model.summary();
            }
        }

        public bool Predict(List<float> floatList)
        {
            using (Py.GIL()) // Acquire the Global Interpreter Lock
            {
                dynamic segment = np.array(floatList);
                dynamic resultPredict = aiCore.predict(segment, model);

                return (String)resultPredict == "OK";
            }
        }
    }


}
