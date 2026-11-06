#if UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace GoGhost.Duel.EditorTools
{
    // Own one PCM buffer and its device handle. The driver updates the header in place.
    internal sealed class StudioWaveOut : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        struct WaveFormat
        {
            public ushort format, channels;
            public uint rate, bytesPerSecond;
            public ushort blockAlign, bits, extra;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WaveHeader
        {
            public IntPtr samples;
            public uint bytes, recorded;
            public UIntPtr user;
            public uint flags, loops;
            public IntPtr next, reserved;
        }

        const uint Done = 1, BeginLoop = 4, EndLoop = 8;
        static readonly uint HeaderSize = (uint)Marshal.SizeOf<WaveHeader>();
        static readonly int FlagsOffset = Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.flags)).ToInt32();
        IntPtr device, header;
        GCHandle samples;
        bool prepared;

        [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr device, uint id, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr device);
        [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr device);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern uint waveOutGetErrorTextW(uint error, StringBuilder message, uint size);

        public bool Finished => prepared && (Marshal.ReadInt32(header, FlagsOffset) & Done) != 0;

        public StudioWaveOut(float[] values, int channels, int rate, bool loop)
        {
            var pcm = new short[values.Length];
            for (int i = 0; i < pcm.Length; i++)
                pcm[i] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(values[i] * 32768.0)));
            var format = new WaveFormat {
                format = 1, channels = (ushort)channels, rate = (uint)rate, bits = 16,
                blockAlign = (ushort)(channels * 2), bytesPerSecond = (uint)(rate * channels * 2)
            };
            try
            {
                Check(waveOutOpen(out device, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0));
                samples = GCHandle.Alloc(pcm, GCHandleType.Pinned);
                header = Marshal.AllocHGlobal((int)HeaderSize);
                var buffer = new WaveHeader {
                    samples = samples.AddrOfPinnedObject(), bytes = (uint)(pcm.Length * 2),
                    loops = loop ? uint.MaxValue : 0
                };
                Marshal.StructureToPtr(buffer, header, false);
                Check(waveOutPrepareHeader(device, header, HeaderSize));
                prepared = true;
                if (loop)
                    Marshal.WriteInt32(header, FlagsOffset, Marshal.ReadInt32(header, FlagsOffset) | (int)(BeginLoop | EndLoop));
                Check(waveOutWrite(device, header, HeaderSize));
            }
            catch { Dispose(); throw; }
        }

        static void Check(uint error)
        {
            if (error == 0) return;
            var message = new StringBuilder(256);
            waveOutGetErrorTextW(error, message, (uint)message.Capacity);
            throw new InvalidOperationException($"Windows audio output ({error}): {message}");
        }

        public void Dispose()
        {
            if (prepared)
            {
                if (!Finished) Check(waveOutReset(device));
                Check(waveOutUnprepareHeader(device, header, HeaderSize));
                prepared = false;
            }
            if (header != IntPtr.Zero) { Marshal.FreeHGlobal(header); header = IntPtr.Zero; }
            if (samples.IsAllocated) samples.Free();
            if (device != IntPtr.Zero) { Check(waveOutClose(device)); device = IntPtr.Zero; }
        }
    }
}
#endif
