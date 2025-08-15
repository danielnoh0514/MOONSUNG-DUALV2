using LiveCharts.Wpf;
using LiveCharts;
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
using MathNet.Numerics.IntegralTransforms;
using NAudio.Wave;
using System.Windows.Threading;

namespace HVT.Controls
{
    public partial class MicrophoneViewer : UserControl
    {
        //public Microphone Microphone;
        private double threshold = 0.01; // Frequency threshold for display
        private int bufferSize = 1024; // Number of samples to display on the chart

        public MicrophoneViewer()
        {
            InitializeComponent();
            this.DataContext = this;
            //Microphone = new Microphone();
            //Microphone.WaveIn.DataAvailable += OnDataAvailable;

            //DispatcherTimer timer = new DispatcherTimer();
            //timer.Interval = TimeSpan.FromMilliseconds(100); // Update every 100 ms (adjust as needed)
            //timer.Tick += (s, args) =>
            //{
            //    var values = new ChartValues<float>(Microphone.AudioBuffer);

            //    // Update the chart
            //    TimeDomainChart.Series.Clear();
            //    TimeDomainChart.Series.Add(new LineSeries
            //    {
            //        Values = values,
            //        PointGeometry = null,
            //        Title = "Time-Domain Waveform"
            //    });
            //};
            //timer.Start();


        }
        private async Task StartRecording()
        {
            //await Task.Run(() =>
            //{
            //    if (Microphone != null)
            //    {
            //        Microphone.StartRecording();
            //    }
            //});
        }
        private async Task StopRecording()
        {
            //await Task.Run(() =>
            //{
            //    if (Microphone != null)
            //    {
            //        Microphone.StopRecording();
            //    }
            //});

        }

        private void UpdateCharts()
        {
            // Run in a background task to avoid UI freezing
          
                // Update time-domain waveform
                DisplayTimeDomainWaveform();

                // Update frequency spectrum
                //DisplayFrequencySpectrum();
        }
        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            // Convert byte buffer to float buffer (PCM 16-bit)
            //for (int i = 0; i < e.BytesRecorded; i += 2)
            //{
            //    short sample = BitConverter.ToInt16(e.Buffer, i);
            //    Microphone.AudioBuffer.Add(sample / 32768f); // Normalize to -1.0 to 1.0

            //    // Limit buffer size for display purposes
            //    if (Microphone.AudioBuffer.Count > bufferSize)
            //    {
            //        Microphone.AudioBuffer.RemoveAt(0);
            //    }

            //}
            UpdateCharts();
        }


        private static List<double> GetFrequenciesAboveThreshold(float[] audioData, int sampleRate, double threshold)
        {
            int n = audioData.Length;
            var complexSamples = audioData.Select(x => new System.Numerics.Complex(x, 0)).ToArray();
            Fourier.Forward(complexSamples, FourierOptions.Matlab);

            // Get magnitudes of each frequency bin
            double[] magnitudes = complexSamples.Take(n / 2).Select(c => c.Magnitude).ToArray();

            List<double> frequenciesAboveThreshold = new List<double>();

            // Iterate over magnitudes to find frequencies above threshold
            for (int i = 0; i < magnitudes.Length; i++)
            {
                if (magnitudes[i] > threshold)
                {
                    double frequency = i * (double)sampleRate / n;
                    frequenciesAboveThreshold.Add(frequency);
                }
            }

            return frequenciesAboveThreshold;
        }
        private void DisplayTimeDomainWaveform()
        {
            //try
            //{
            //    var values = new ChartValues<float>(Microphone.AudioBuffer);

            //    // Update the UI on the main thread
            //    this.Dispatcher.Invoke(() =>
            //    {
            //        TimeDomainChart.Series.Clear();
            //        TimeDomainChart.Series.Add(new LineSeries
            //        {
            //            Values = values,
            //            PointGeometry = null,
            //            Title = "Time-Domain Waveform"
            //        });


            //    });
            //}
            //catch
            //{
            //    Console.WriteLine("EEEEEEE");
            //}
        }

        private void DisplayFrequencySpectrum()
        {
            //Dispatcher.Invoke(new System.Action(() =>
            //{
            //    var (frequencies, magnitudes) = GetFrequenciesAndMagnitudes(Microphone.AudioBuffer.ToArray(), Microphone.SampleRate, threshold);

            //    var values = new ChartValues<double>(magnitudes);

            //    // Set up LiveCharts for frequency-domain data
            //    FrequencyDomainChart.Series.Clear();
            //    FrequencyDomainChart.Series.Add(new LineSeries
            //    {
            //        Values = values,
            //        PointGeometry = null,
            //        Title = "Frequency Spectrum"
            //    });

            //    // Set frequency labels on X-axis
            //    FrequencyDomainChart.AxisX[0].Labels = frequencies.Select(f => f.ToString("F0")).ToArray();
            //}));
        }

        private (List<double>, List<double>) GetFrequenciesAndMagnitudes(float[] audioData, int sampleRate, double threshold)
        {
            int n = audioData.Length;
            var complexSamples = audioData.Select(x => new System.Numerics.Complex(x, 0)).ToArray();
            Fourier.Forward(complexSamples, FourierOptions.Matlab);

            List<double> frequencies = new List<double>();
            List<double> magnitudes = new List<double>();

            for (int i = 0; i < n / 2; i++) // Only consider positive frequencies
            {
                double magnitude = complexSamples[i].Magnitude;
                if (magnitude > threshold)
                {
                    double frequency = i * (double)sampleRate / n;
                    frequencies.Add(frequency);
                    magnitudes.Add(magnitude);
                }
            }

            return (frequencies, magnitudes);
        }

        private void MICBtnStart(object sender, RoutedEventArgs e)
        {
            StartRecording();
        }

        private void MICBtnStop(object sender, RoutedEventArgs e)
        {
            StopRecording();
        }
    }
}
