using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace GoGhost.Duel.EditorTools
{
    // Read original PCM, including stereo, rather than re-encoding Unity's imported preview.
    internal sealed class StudioTake
    {
        public float[] samples;
        public int channels, rate;
        public float Seconds => (float)samples.Length / (rate * channels);
        public static StudioTake Read(string path)
        {
            using var f = new BinaryReader(File.OpenRead(path));
            if (new string(f.ReadChars(4)) != "RIFF") throw new IOException("Expected a RIFF WAV.");
            f.ReadUInt32();
            if (new string(f.ReadChars(4)) != "WAVE") throw new IOException("Expected WAVE.");
            var take = new StudioTake();
            while (f.BaseStream.Position + 8 <= f.BaseStream.Length)
            {
                string tag = new string(f.ReadChars(4));
                int length = f.ReadInt32();
                long next = f.BaseStream.Position + length + (length & 1);
                if (length < 0 || next > f.BaseStream.Length + 1) throw new IOException("Invalid WAV chunk.");
                if (tag == "fmt ")
                {
                    if (f.ReadUInt16() != 1) throw new IOException("Expected PCM.");
                    take.channels = f.ReadUInt16();
                    take.rate = f.ReadInt32();
                    f.ReadInt32(); f.ReadUInt16();
                    if (f.ReadUInt16() != 16) throw new IOException("Expected PCM16.");
                }
                if (tag == "data")
                {
                    if (take.channels < 1 || take.rate < 1 || length < 2) throw new IOException("Invalid WAV format.");
                    take.samples = new float[length / 2];
                    for (int i = 0; i < take.samples.Length; i++) take.samples[i] = f.ReadInt16() / 32768f;
                    return take;
                }
                f.BaseStream.Position = next;
            }
            throw new IOException("Missing WAV samples.");
        }

        public float[] Selection(float from, float to, float gain)
        {
            int frames = samples.Length / channels;
            int start = Mathf.Clamp(Mathf.RoundToInt(from * rate), 0, frames - 1);
            int count = Mathf.Clamp(Mathf.RoundToInt(to * rate) - start, 1, frames - start);
            var values = new float[count * channels];
            for (int i = 0; i < values.Length; i++) values[i] = Mathf.Clamp(samples[start * channels + i] * gain, -1, 1);
            return values;
        }

        public AudioClip Clip(float from, float to, float gain)
        {
            var values = Selection(from, to, gain);
            var clip = AudioClip.Create("Voice Studio audition", values.Length / channels, channels, rate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(values, 0);
            return clip;
        }

        public float ActiveRms()
        {
            double sum = 0;
            int n = 0;
            foreach (float v in samples)
                if (Mathf.Abs(v) > .015f) { sum += v*v; n++; }
            return n > 0 ? (float)Math.Sqrt(sum/n) : 0;
        }
    }

    internal sealed class StudioPlayer : IDisposable
    {
        static readonly Type AudioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        AudioClip clip;
        AudioSource monitor;
#if UNITY_EDITOR_WIN
        StudioWaveOut output;
        public bool ReleaseFinished()
        {
            if (output == null || !output.Finished) return false;
            Dispose();
            return true;
        }
#endif
        public void Play(StudioTake take, float from, float to, bool loop, float gain, HandlerVoice game, float delay)
        {
            Dispose();
#if UNITY_EDITOR_WIN
            if (game == null)
            {
                output = new StudioWaveOut(take.Selection(from, to, gain), take.channels, take.rate, loop);
                return;
            }
#endif
            clip = take.Clip(from, to, gain);
            if (game != null)
            {
                monitor = game.CreateStudioMonitor();
                monitor.clip = clip;
                monitor.loop = loop;
                monitor.PlayDelayed(delay);
            }
            else
            {
                var play = AudioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] {typeof(AudioClip), typeof(int), typeof(bool)}, null);
                if (play == null) throw new MissingMethodException("Unity audio preview is unavailable.");
                play.Invoke(null, new object[] {clip, 0, loop});
            }
        }

        public void Dispose()
        {
#if UNITY_EDITOR_WIN
            output?.Dispose();
            output = null;
#endif
            if (monitor != null)
            {
                monitor.Stop();
                UnityEngine.Object.DestroyImmediate(monitor.gameObject);
            }
            else if (clip != null)
                AudioUtil?.GetMethod("StopAllPreviewClips",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)?.Invoke(null,null);
            if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
        }
    }
}
