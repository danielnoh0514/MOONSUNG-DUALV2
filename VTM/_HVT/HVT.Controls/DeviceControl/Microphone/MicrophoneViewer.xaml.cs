using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media;
using NAudio.Wave;
using Python.Runtime;



namespace HVT.Controls
{
    public partial class MicrophoneViewer : UserControl
    {
        public AudioTester AudioTester;

        private WaveInEvent waveIn;
        private List<float> audioSamples = new List<float>();
        private bool isRecording = false;
        private const int SampleRate = 44100; // 44.1kHz
        private const int ChannelCount = 1; // Mono

        // F0 Detection Parameters
        private const int F0AnalysisWindow = 4096; // Increased window for low frequency detection
        private const double MinFrequency = 50;    // Minimum detectable frequency (Hz)
        private const double MaxFrequency = 10000; // Giới hạn tần số tối đa
        private readonly Queue<float> f0Buffer = new Queue<float>(); // Smoothing buffer

        public List<float> F0Saving = new List<float>();
        public List<float> AmpSaving = new List<float>();


        public float CurrentF0 { get; set; } = 0;
        public float CurrentAmplitude { get; set; } = 0;

        string binPath = AppContext.BaseDirectory;

        public MicrophoneViewer()
        {
            InitializeComponent();
            txtInfo.Text = "Ready to record";
            AudioTester = new AudioTester(System.IO.Path.Combine(binPath, "aicore"));


        }

        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            StartRecording();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            StopRecording();
        }

        const float duration = 1.0f;
        public void StartRecording()
        {

            if (isRecording) return;

            AmpSaving.Clear();

            try
            {
                waveIn = new WaveInEvent();
                waveIn.WaveFormat = new WaveFormat(SampleRate, ChannelCount);
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.RecordingStopped += WaveIn_RecordingStopped;

                audioSamples.Clear();
                for (int i = 0; i < SampleRate* duration; i += 1)
                {
                    
                    audioSamples.Add(0);
                }



                waveformCanvas.Children.Clear();

                waveIn.StartRecording();
                isRecording = true;

                btnRecord.IsEnabled = false;
                btnStop.IsEnabled = true;
                txtInfo.Text = "Recording...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Recording error: {ex.Message}");
            }
        }

        public void StopRecording()
        {
            if (!isRecording) return;

            if (waveIn != null)
            {
                waveIn.StopRecording();
                isRecording = false;
                btnRecord.IsEnabled = true;
                btnStop.IsEnabled = false;
                txtInfo.Text = "Recording stopped";
            }
        }

 
        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                CurrentAmplitude = 0;
                float[] currentChunk = new float[e.BytesRecorded / 2];

                // Process audio samples
                for (int i = 0; i < e.BytesRecorded; i += 2)
                {
                    short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
                    float normalizedSample = sample / 32768f; // Normalize to [-1, 1]
                    currentChunk[i / 2] = normalizedSample;
                    CurrentAmplitude = Math.Max(CurrentAmplitude, Math.Abs(normalizedSample));
                    audioSamples.Add(normalizedSample);
                    AmpSaving.Add(normalizedSample);
                }



                // Limit buffer size
                if (audioSamples.Count > SampleRate * duration) // Keep n seconds of data
                {
                    audioSamples.RemoveRange(0, audioSamples.Count - (int)((float)SampleRate * duration));

                }



                // Update UI
                Dispatcher.Invoke(() =>
                {
                    // Update amplitude display
                    pbAmplitude.Value = CurrentAmplitude;
                    txtCurrentAmplitude.Text = $"Amplitude: {CurrentAmplitude:F2}";
                    txtCurrentAmplitude.Foreground = CurrentAmplitude > 0.8 ? Brushes.Red : Brushes.Black;

                    DrawWaveform();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Recording error: {ex.Message}");

            }

        }

        private void WaveIn_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (waveIn != null)
            {
                waveIn.Dispose();
                waveIn = null;
            }
        }



        private void DrawWaveform()
        {
            if (audioSamples.Count == 0) return;

            waveformCanvas.Children.Clear();
            double canvasWidth = waveformCanvas.ActualWidth;
            double canvasHeight = waveformCanvas.ActualHeight;
            double middle = canvasHeight / 2;

            int samplesToDisplay = Math.Min(1000, audioSamples.Count);
            int step = audioSamples.Count / samplesToDisplay;

            Polyline polyline = new Polyline
            {
                Stroke = Brushes.DarkGreen,
                StrokeThickness = 1
            };

            for (int i = 0; i < samplesToDisplay; i++)
            {
                int index = i * step;
                if (index >= audioSamples.Count) break;

                double x = (i * canvasWidth) / samplesToDisplay;
                double y = middle - (audioSamples[index] * middle * 0.8);
                polyline.Points.Add(new Point(x, y));
            }

            waveformCanvas.Children.Add(polyline);
        }

        protected void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (isRecording)
            {
                StopRecording();
            }
        }
    }
}