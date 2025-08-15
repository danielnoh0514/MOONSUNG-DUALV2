using NAudio.Wave;
using MathNet.Numerics.IntegralTransforms;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiveCharts.Wpf;
using LiveCharts;

namespace HVT.Controls
{
    public class Microphone
    {
        public const int SampleRate = 44100;


        //public WaveInEvent WaveIn;


        private List<float> audioBuffer = new List<float>();

        public List<float> AudioBuffer { get { return audioBuffer; } }


        public Microphone() {

            //WaveIn = new WaveInEvent();

            //WaveIn.WaveFormat = new WaveFormat(SampleRate, 1); // Sample rate 44100 Hz, Mono

            // Event triggered whenever audio data is available

        }
        //public void StartRecording() {

        //    audioBuffer.Clear();
        //    WaveIn.StartRecording();

        //}
        //public void StopRecording()
        //{
        //    WaveIn.StopRecording();
        //}

    }
}
