using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace GoGhost.Duel
{
    // Called on a worker. Only PCM samples cross back to the Unity audio thread.
    internal static class HandlerSpeech
    {
        internal struct Recording { internal float[] Samples; internal int Rate; }
        static readonly string[] Digits = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
        static readonly string[] Intro = { "nice", "to", "meet", "you" };
        static readonly object EngineLock = new object();

        internal static Recording Synthesize(string root, string nickname, CancellationToken token, Action<int> report)
        {
            lock (EngineLock)
            {
                if (token.IsCancellationRequested) return default;
                var lexicon = new List<string>();
                string spokenName = MakeName(root, nickname, lexicon);
                report(20);
                if (spokenName.Length == 0 || token.IsCancellationRequested) return default;
                string temporary = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
                try
                {
                    File.WriteAllLines(temporary, lexicon, new UTF8Encoding(false));
                    return Render(root, temporary, "Nice to meet you, " + spokenName + ".", token, report);
                }
                finally { File.Delete(temporary); }
            }
        }

        static string MakeName(string root, string nickname, List<string> lexicon)
        {
            // Keep Han characters intact and normalize accented Latin names for US English.
            string normalized = Regex.Replace(nickname.Normalize(NormalizationForm.FormD), @"\p{M}", "")
                .ToLowerInvariant();
            var parts = new List<string>();
            var english = new HashSet<string>(Intro);
            var chinese = new HashSet<string>();
            foreach (Match m in Regex.Matches(normalized, @"[a-z]+|[0-9]|[\u3400-\u9fff]"))
            {
                string part = m.Value;
                if (part[0] >= '\u3400') chinese.Add(part);
                else
                {
                    if (char.IsDigit(part[0])) part = Digits[part[0] - '0'];
                    english.Add(part);
                    foreach (char c in part) english.Add(c.ToString());
                }
                parts.Add(part);
            }
            if (parts.Count == 0) return "";
            var phones = new Dictionary<string, string>();
            ReadEntries(root, "lexicon-us-en.txt", english, phones);
            ReadEntries(root, "lexicon-zh.txt", chinese, phones);
            foreach (string word in Intro) lexicon.Add(word + " " + phones[word]);
            var aliases = new List<string>();
            foreach (string part in parts)
            {
                if (phones.TryGetValue(part, out string pronunciation))
                    AddAlias(pronunciation, aliases, lexicon);
                else // Spell unfamiliar Latin handles in the same voice.
                    foreach (char c in part)
                        if (phones.TryGetValue(c.ToString(), out var letter)) AddAlias(letter, aliases, lexicon);
            }
            return string.Join(" ", aliases);
        }

        static void ReadEntries(string root, string file, HashSet<string> wanted, Dictionary<string, string> phones)
        {
            if (wanted.Count == 0) return;
            foreach (var line in File.ReadLines(Path.Combine(root, file)))
            {
                int space = line.IndexOf(' ');
                if (space <= 0) continue;
                string word = line.Substring(0, space);
                if (!wanted.Remove(word)) continue;
                phones[word] = line.Substring(space + 1);
                if (wanted.Count == 0) break;
            }
        }

        // ASCII aliases let the English tokenizer use the supplied name pronunciations,
        // including Chinese names. The transient lexicon never contains the typed nickname.
        static void AddAlias(string pronunciation, List<string> aliases, List<string> lexicon)
        {
            string alias = "name" + (char)('a' + aliases.Count / 26) + (char)('a' + aliases.Count % 26);
            aliases.Add(alias);
            lexicon.Add(alias + " " + pronunciation);
        }

        static Recording Render(string root, string lexicon, string text, CancellationToken token, Action<int> report)
        {
            var config = new HandlerKokoro.Config();
            config.model.num_threads = 2;
            config.model.provider = "cpu";
            config.model.kokoro.model = Path.Combine(root, "model.int8.onnx");
            config.model.kokoro.voices = Path.Combine(root, "voices.bin");
            config.model.kokoro.tokens = Path.Combine(root, "tokens.txt");
            config.model.kokoro.length_scale = 1;
            config.model.kokoro.lexicon = lexicon;
            config.max_num_sentences = 1;
            config.silence_scale = 1;
            IntPtr tts = HandlerKokoro.SherpaOnnxCreateOfflineTts(ref config);
            if (tts == IntPtr.Zero) throw new InvalidOperationException("Could not load the assistant voice.");
            try
            {
                report(65);
                if (token.IsCancellationRequested) return default;
                const int heart = 0; // The shipped bank contains Heart (0) and Michael (1).
                const float handlerSpeed = 1.1f;
                IntPtr result = HandlerKokoro.SherpaOnnxOfflineTtsGenerate(tts, text, heart, handlerSpeed);
                if (result == IntPtr.Zero) return default;
                try
                {
                    report(95);
                    var audio = Marshal.PtrToStructure<HandlerKokoro.Audio>(result);
                    if (audio.samples == IntPtr.Zero || audio.n <= 0 || audio.sample_rate <= 0) return default;
                    var samples = new float[audio.n];
                    Marshal.Copy(audio.samples, samples, 0, samples.Length);
                    if (audio.sample_rate != 24000) throw new InvalidOperationException("Unexpected Happy voice sample rate.");
                    return new Recording { Samples = HappyVoiceMaster.Stereo(samples, root, token), Rate = audio.sample_rate };
                }
                finally { HandlerKokoro.SherpaOnnxDestroyOfflineTtsGeneratedAudio(result); }
            }
            finally { HandlerKokoro.SherpaOnnxDestroyOfflineTts(tts); }
        }
    }
}
