using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace AgyAccountSwarm.Services;

public interface IAudioService
{
    bool IsEnabled { get; set; }
    void PlayLaunch();
    void PlaySuccess();
    void PlayClick();
    void PlayDelete();
    void PlaySync();
    void PlayQuotaAlert();
    void PlayFluffyPurr();
    void PlayWelcomeCalmChime();
}

public class AudioService : IAudioService
{
    public bool IsEnabled { get; set; } = true;

    public void PlayFluffyPurr()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                // Harmonious fluffy arpeggio with soft purr modulation
                PlayPurrChord(new[] { 523.25, 659.25, 783.99, 1046.50 }, 650);
            }
            catch
            {
                if (OperatingSystem.IsWindows())
                {
                    SystemSounds.Asterisk.Play();
                }
            }
        });
    }

    public void PlayQuotaAlert()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                // Minor warning descent: 493Hz (B4) then 370Hz (F#4)
                PlayTone(493, 110);
                PlayTone(370, 160);
            }
            catch
            {
                if (OperatingSystem.IsWindows())
                {
                    SystemSounds.Exclamation.Play();
                }
            }
        });
    }

    public void PlaySync()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                // Refresh droplet arpeggio: 740Hz then 1174Hz
                PlayTone(740, 50);
                PlayTone(1174, 75);
            }
            catch
            {
                if (OperatingSystem.IsWindows())
                {
                    SystemSounds.Beep.Play();
                }
            }
        });
    }

    public void PlayLaunch()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                // Upward tone: 550Hz then 880Hz
                PlayTone(550, 60);
                PlayTone(880, 80);
            }
            catch
            {
                if (OperatingSystem.IsWindows())
                {
                    SystemSounds.Asterisk.Play();
                }
            }
        });
    }

    public void PlaySuccess()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                // Cheerful chime: 659Hz (E5) then 987Hz (B5)
                PlayTone(659, 70);
                PlayTone(987, 100);
            }
            catch
            {
                if (OperatingSystem.IsWindows())
                {
                    SystemSounds.Beep.Play();
                }
            }
        });
    }

    public void PlayClick()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                PlayTone(1200, 25);
            }
            catch
            {
                if (OperatingSystem.IsWindows())
                {
                    SystemSounds.Asterisk.Play();
                }
            }
        });
    }

    public void PlayDelete()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                PlayTone(380, 90);
                PlayTone(260, 110);
            }
            catch
            {
                if (OperatingSystem.IsWindows())
                {
                    SystemSounds.Hand.Play();
                }
            }
        });
    }

    private static void PlayTone(int frequency, int durationMs)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var sampleRate = 8000;
            var numSamples = (sampleRate * durationMs) / 1000;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // WAV header
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + numSamples);
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16); // Subchunk1Size
            writer.Write((short)1); // AudioFormat PCM
            writer.Write((short)1); // NumChannels Mono
            writer.Write(sampleRate);
            writer.Write(sampleRate); // ByteRate
            writer.Write((short)1); // BlockAlign
            writer.Write((short)8); // BitsPerSample
            writer.Write("data"u8.ToArray());
            writer.Write(numSamples);

            // Generate sine wave samples with gentle attack and decay
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                double envelope = 1.0;
                if (i < 80) envelope = (double)i / 80;
                else if (i > numSamples - 120) envelope = (double)(numSamples - i) / 120;

                double angle = 2.0 * Math.PI * frequency * t;
                byte sample = (byte)(128 + 60 * Math.Sin(angle) * envelope);
                writer.Write(sample);
            }

            stream.Position = 0;
            using var player = new SoundPlayer(stream);
            player.PlaySync();
        }
        catch
        {
            // Silently fail if audio device is unavailable
        }
    }

    private static void PlayPurrChord(double[] frequencies, int durationMs)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var sampleRate = 8000;
            var numSamples = (sampleRate * durationMs) / 1000;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // WAV header
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + numSamples);
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16); // Subchunk1Size
            writer.Write((short)1); // AudioFormat PCM
            writer.Write((short)1); // NumChannels Mono
            writer.Write(sampleRate);
            writer.Write(sampleRate); // ByteRate
            writer.Write((short)1); // BlockAlign
            writer.Write((short)8); // BitsPerSample
            writer.Write("data"u8.ToArray());
            writer.Write(numSamples);

            // Polyphonic sine mix with gentle purr amplitude tremolo
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                
                // Envelope: Smooth fade-in, sustained body, gentle fade-out
                double envelope = 1.0;
                if (i < 300) envelope = (double)i / 300;
                else if (i > numSamples - 600) envelope = (double)(numSamples - i) / 600;

                // Subtle purr tremolo at 24Hz
                double purrMod = 0.82 + 0.18 * Math.Sin(2.0 * Math.PI * 24.0 * t);

                double waveSum = 0;
                for (int f = 0; f < frequencies.Length; f++)
                {
                    double freq = frequencies[f];
                    // Stagger arpeggio start times slightly for a rich fluid chord
                    double noteOffset = f * 0.045;
                    if (t >= noteOffset)
                    {
                        double noteT = t - noteOffset;
                        waveSum += Math.Sin(2.0 * Math.PI * freq * noteT) / frequencies.Length;
                    }
                }

                double sampleVal = 128.0 + 55.0 * waveSum * envelope * purrMod;
                sampleVal = Math.Clamp(sampleVal, 0.0, 255.0);
                writer.Write((byte)sampleVal);
            }

            stream.Position = 0;
            using var player = new SoundPlayer(stream);
            player.PlaySync();
        }
        catch
        {
            // Silently fail if audio device is unavailable
        }
    }

    public void PlayWelcomeCalmChime()
    {
        if (!IsEnabled) return;
        Task.Run(() =>
        {
            try
            {
                PlayCalmAmbientChime(new[] { 329.63, 415.30, 493.88, 622.25, 739.99 }, 3000);
            }
            catch
            {
                // Silently fail if audio device is unavailable
            }
        });
    }

    private static void PlayCalmAmbientChime(double[] frequencies, int durationMs)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var sampleRate = 8000;
            var numSamples = (sampleRate * durationMs) / 1000;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // WAV header
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + numSamples);
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16); // Subchunk1Size
            writer.Write((short)1); // AudioFormat PCM
            writer.Write((short)1); // NumChannels Mono
            writer.Write(sampleRate);
            writer.Write(sampleRate); // ByteRate
            writer.Write((short)1); // BlockAlign
            writer.Write((short)8); // BitsPerSample
            writer.Write("data"u8.ToArray());
            writer.Write(numSamples);

            // Polyphonic ambient swell and slow crystalline decay
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;

                // Soft attack (500ms) then smooth slow decay over remaining 2.5s
                double attackDuration = 0.5;
                double envelope;
                if (t < attackDuration)
                {
                    envelope = Math.Sin((t / attackDuration) * (Math.PI / 2.0));
                }
                else
                {
                    double decayT = (t - attackDuration) / (3.0 - attackDuration);
                    envelope = Math.Max(0.0, Math.Pow(1.0 - decayT, 1.6));
                }

                // Ambient shimmer / soft chorus modulation (3.5Hz)
                double shimmer = 0.92 + 0.08 * Math.Sin(2.0 * Math.PI * 3.5 * t);

                double waveSum = 0;
                for (int f = 0; f < frequencies.Length; f++)
                {
                    double freq = frequencies[f];
                    // Gentle staggered chime entry (70ms between notes)
                    double noteOffset = f * 0.070;
                    if (t >= noteOffset)
                    {
                        double noteT = t - noteOffset;
                        // Harmonic warmth with fundamental + soft octave overtone
                        double harmonic = Math.Sin(2.0 * Math.PI * freq * noteT) + 0.3 * Math.Sin(4.0 * Math.PI * freq * noteT);
                        waveSum += harmonic / (frequencies.Length * 1.3);
                    }
                }

                double sampleVal = 128.0 + 50.0 * waveSum * envelope * shimmer;
                sampleVal = Math.Clamp(sampleVal, 0.0, 255.0);
                writer.Write((byte)sampleVal);
            }

            stream.Position = 0;
            using var player = new SoundPlayer(stream);
            player.PlaySync();
        }
        catch
        {
            // Silently fail if audio device is unavailable
        }
    }
}
