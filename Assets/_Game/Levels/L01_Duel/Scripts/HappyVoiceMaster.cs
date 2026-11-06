using System;
using System.IO;
using System.Numerics;
using System.Threading;

namespace GoGhost.Duel
{
    // Worker-thread version of Tools/render_happy_voice.py for variable-length names.
    internal static class HappyVoiceMaster
    {
        const int Rate = 24000, Window = 1024, Hop = 128, Bands = 24, Bins = Window / 2 + 1;

        static void Transform(Complex[] data, bool inverse)
        {
            int n = data.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                while ((j & bit) != 0) { j ^= bit; bit >>= 1; }
                j ^= bit;
                if (i < j) { var tmp = data[i]; data[i] = data[j]; data[j] = tmp; }
            }
            for (int size = 2; size <= n; size <<= 1)
            {
                var step = Complex.FromPolarCoordinates(1, (inverse ? 2 : -2) * Math.PI / size);
                int half = size / 2;
                for (int start = 0; start < n; start += size)
                {
                    var phase = Complex.One;
                    for (int i = start; i < start + half; i++)
                    {
                        var a = data[i];
                        var b = data[i + half] * phase;
                        data[i] = a + b;
                        data[i + half] = a - b;
                        phase *= step;
                    }
                }
            }
            if (inverse) for (int i = 0; i < n; i++) data[i] /= n;
        }

        static double Power(Complex c) => c.Real * c.Real + c.Imaginary * c.Imaginary;
        static double Rms(double[] a)
        {
            double sum = 0;
            foreach (double v in a) sum += v*v;
            return Math.Sqrt(sum / a.Length);
        }
        static double Match(double[] a, double target)
        {
            double peak = 0;
            foreach (double value in a) peak = Math.Max(peak, Math.Abs(value));
            return peak == 0 ? 0 : Math.Min(target/Rms(a), .91/peak);
        }

        static double[] Electronic(double[] dry, CancellationToken token)
        {
            var carrier = new double[12000]; // 182 Hz repeats after 12000 samples at 24 kHz.
            for (int k = 1; k <= 56; k++)
            {
                double gain = Math.Pow(k,-.38), step = 2*Math.PI*k*182/Rate;
                for (int i=0; i<carrier.Length; i++) carrier[i] += gain*Math.Sin(step*i+.15*k*k);
            }
            var window = new double[Window];
            for (int i=0; i<Window; i++) window[i] = Math.Sqrt(.5-.5*Math.Cos(2*Math.PI*i/(Window-1)));
            var triangles = new double[Bands,Bins];
            var first = new int[Bands];
            var last = new int[Bands];
            var sums = new double[Bins];
            double ratio = Math.Pow(10500.0/85,1.0/25);
            for (int b=0; b<Bands; b++)
            {
                double low=85*Math.Pow(ratio,b), middle=low*ratio, high=middle*ratio;
                first[b]=(int)Math.Ceiling(low*Window/Rate);
                last[b]=(int)Math.Floor(high*Window/Rate);
                for (int j=first[b]; j<=last[b]; j++)
                {
                    double hz=(double)j*Rate/Window;
                    triangles[b,j]=Math.Min((hz-low)/(middle-low),(high-hz)/(high-middle));
                    sums[j]+=triangles[b,j];
                }
            }
            var signal = new Complex[Window];
            var excite = new Complex[Window];
            var gains = new double[Bins];
            var output = new double[dry.Length];
            var norm = new double[dry.Length];
            for (int start=-Window; start<=dry.Length; start+=Hop)
            {
                token.ThrowIfCancellationRequested();
                for (int k=0;k<Window;k++)
                {
                    int index=start+k;
                    signal[k]=index>=0 && index<dry.Length ? window[k]*dry[index] : 0;
                    excite[k]=window[k]*carrier[(index+Window)%carrier.Length];
                }
                Transform(signal,false);
                Transform(excite,false);
                Array.Clear(gains,0,Bins);
                for (int b=0;b<Bands;b++)
                {
                    double speech=0, energy=1e-7;
                    for (int j=first[b];j<=last[b];j++)
                    {
                        speech+=triangles[b,j]*Power(signal[j]);
                        energy+=triangles[b,j]*Power(excite[j]);
                    }
                    double gain=Math.Sqrt(speech/energy);
                    for (int j=first[b];j<=last[b];j++) gains[j]+=gain*triangles[b,j];
                }
                for (int j=0;j<Bins;j++)
                {
                    double treble=Math.Max(0,Math.Min(1,((double)j*Rate/Window-3300)/1700));
                    signal[j]=treble*signal[j]+(1-treble)*excite[j]*gains[j]/Math.Max(.02,sums[j]);
                    if (j>0 && j<Window/2) signal[Window-j]=Complex.Conjugate(signal[j]);
                }
                Transform(signal,true);
                for (int k=Math.Max(-start,0);k<Math.Min(Window,dry.Length-start);k++)
                {
                    output[start+k]+=window[k]*signal[k].Real;
                    norm[start+k]+=window[k]*window[k];
                }
            }
            for (int i=0;i<output.Length;i++) output[i]/=Math.Max(1e-6,norm[i]);
            return output;
        }

        internal static void ApplyBody(float[] stereo, double sub, double drive)
        {
            if (drive <= 0 || (Math.Abs(sub - .42) < 1e-6 && Math.Abs(drive - 1.9) < 1e-6)) return;
            double pole = Math.Exp(-2 * Math.PI * 175 / Rate), shelf = Math.Pow(10, sub - .42) - 1;
            var low = new double[2];
            double originalEnergy = 0, shapedEnergy = 0, peak = 1e-12, shapedPeak = 1e-12;
            for (int i = 0; i < stereo.Length; i++)
            {
                double sample = stereo[i];
                originalEnergy += sample * sample;
                int channel = i % 2;
                low[channel] = pole * low[channel] + (1 - pole) * sample;
                stereo[i] = (float)(sample + shelf * low[channel]);
                peak = Math.Max(peak, Math.Abs(stereo[i]));
            }
            for (int i = 0; i < stereo.Length; i++)
            {
                double v = .95 * stereo[i] / peak;
                double shaped = Math.Tanh(drive / 1.9 * .5 * Math.Log((1 + v) / (1 - v)));
                stereo[i] = (float)shaped;
                shapedEnergy += shaped * shaped;
                shapedPeak = Math.Max(shapedPeak, Math.Abs(shaped));
            }
            float gain = (float)Math.Min(Math.Sqrt(originalEnergy / Math.Max(1e-24, shapedEnergy)), .999 / shapedPeak);
            for (int i = 0; i < stereo.Length; i++) stereo[i] *= gain;
        }

        internal static float[] Stereo(float[] samples, string root, CancellationToken token,
            double electronic = .15, double nearDb = 0, double spaceDb = 0)
        {
            var dry = Array.ConvertAll(samples, s => (double)s);
            double level = Rms(dry);
            var direct = new double[dry.Length+15600];
            var stereo = new float[direct.Length*2];
            if (level==0) return stereo;
            var layer = Electronic(dry,token);
            double gain=Match(layer,level);
            for (int i=0;i<dry.Length;i++) layer[i]=.40*dry[i]+.65*gain*layer[i];
            gain=Match(layer,level);
            for (int i=0;i<dry.Length;i++)
            {
                layer[i]=Math.Round(layer[i]*gain*32767)/32768;
                direct[i]=electronic*layer[i]+(1-electronic)*dry[i];
            }
            int n=1;
            while (n<direct.Length+16383) n<<=1;
            var spectrum=new Complex[n];
            for (int i=0;i<direct.Length;i++) spectrum[i]=direct[i];
            Transform(spectrum,false);
            var mid=(double[])direct.Clone();
            var side=new double[direct.Length];
            double directLevel=Rms(direct);
            var kernel=new Complex[n];
            using (var reader=new BinaryReader(File.OpenRead(Path.Combine(root,"happy-v08-kernels.bytes"))))
            {
                double[] decibels={-12.5,-13,-9,-6.5};
                for (int k=0;k<4;k++)
                {
                    token.ThrowIfCancellationRequested();
                    Array.Clear(kernel,0,n);
                    int length=k<2 ? 16384 : 1536;
                    for (int j=0;j<length;j++) kernel[j]=reader.ReadSingle();
                    Transform(kernel,false);
                    for (int j=0;j<n;j++) kernel[j]*=spectrum[j];
                    Transform(kernel,true);
                    double sum=0;
                    for (int j=0;j<direct.Length;j++) sum+=kernel[j].Real*kernel[j].Real;
                    gain=directLevel*Math.Pow(10,(decibels[k]+(k<2 ? spaceDb : nearDb))/20)/Math.Max(1e-15,Math.Sqrt(sum/direct.Length));
                    var channel=k%2==0 ? mid : side;
                    for (int j=0;j<direct.Length;j++) channel[j]+=gain*kernel[j].Real;
                }
            }
            double energy=0,peak=0;
            for (int i=0;i<direct.Length;i++)
            {
                stereo[2*i]=(float)(mid[i]+side[i]);
                stereo[2*i+1]=(float)(mid[i]-side[i]);
                peak=Math.Max(peak,Math.Abs(mid[i])+Math.Abs(side[i]));
                if (i<dry.Length) energy+=mid[i]*mid[i]+side[i]*side[i];
            }
            gain=Math.Min(Rms(layer)/Math.Sqrt(energy/dry.Length),.98/peak);
            for (int i=0;i<stereo.Length;i++) stereo[i]*=(float)gain;
            return stereo;
        }
    }
}
