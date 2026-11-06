using System;
using System.Runtime.InteropServices;

namespace GoGhost.Duel
{
    // ABI of sherpa-onnx v2.0.1 c-api.h. Inactive model configurations still occupy
    // their declared slots in the native configuration passed by reference.
    internal static class HandlerKokoro
    {
        const string Library = "sherpa-onnx-c-api";

        [StructLayout(LayoutKind.Sequential)]
        internal struct Audio
        {
            internal IntPtr samples;
            internal int n;
            internal int sample_rate;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Vits
        {
            internal IntPtr model;
            internal IntPtr lexicon;
            internal IntPtr tokens;
            internal IntPtr data_dir;
            internal float noise_scale;
            internal float noise_scale_w;
            internal float length_scale;
            internal IntPtr dict_dir;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Matcha
        {
            internal IntPtr acoustic_model;
            internal IntPtr vocoder;
            internal IntPtr lexicon;
            internal IntPtr tokens;
            internal IntPtr data_dir;
            internal float noise_scale;
            internal float length_scale;
            internal IntPtr dict_dir;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Kokoro
        {
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string model;
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string voices;
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string tokens;
            internal IntPtr data_dir;
            internal float length_scale;
            internal IntPtr dict_dir;
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string lexicon;
            internal IntPtr lang;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Kitten
        {
            internal IntPtr model;
            internal IntPtr voices;
            internal IntPtr tokens;
            internal IntPtr data_dir;
            internal float length_scale;
            internal IntPtr lexicon;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Zipvoice
        {
            internal IntPtr tokens;
            internal IntPtr encoder;
            internal IntPtr decoder;
            internal IntPtr vocoder;
            internal IntPtr data_dir;
            internal IntPtr lexicon;
            internal float feat_scale;
            internal float t_shift;
            internal float target_rms;
            internal float guidance_scale;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Pocket
        {
            internal IntPtr lm_flow;
            internal IntPtr lm_main;
            internal IntPtr encoder;
            internal IntPtr decoder;
            internal IntPtr text_conditioner;
            internal IntPtr vocab_json;
            internal IntPtr token_scores_json;
            internal int voice_embedding_cache_capacity;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Supertonic
        {
            internal IntPtr duration_predictor;
            internal IntPtr text_encoder;
            internal IntPtr vector_estimator;
            internal IntPtr vocoder;
            internal IntPtr tts_json;
            internal IntPtr unicode_indexer;
            internal IntPtr voice_style;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Model
        {
            internal Vits vits;
            internal int num_threads;
            internal int debug;
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string provider;
            internal Matcha matcha;
            internal Kokoro kokoro;
            internal Kitten kitten;
            internal Zipvoice zipvoice;
            internal Pocket pocket;
            internal Supertonic supertonic;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Config
        {
            internal Model model;
            internal IntPtr rule_fsts;
            internal int max_num_sentences;
            internal IntPtr rule_fars;
            internal float silence_scale;
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SherpaOnnxCreateOfflineTts(ref Config config);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SherpaOnnxDestroyOfflineTts(IntPtr tts);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SherpaOnnxOfflineTtsGenerate(IntPtr tts, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, int sid, float speed);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SherpaOnnxDestroyOfflineTtsGeneratedAudio(IntPtr audio);
    }
}
