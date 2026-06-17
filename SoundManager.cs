using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace HeartRateMonitor
{
    public static class SoundManager
    {
        private static string _soundsDir = string.Empty;
        private static SoundPlayer? _heartbeatPlayer;
        private static SoundPlayer? _acceleratePlayer;
        private static SoundPlayer? _deceleratePlayer;

        public static void Initialize()
        {
            try
            {
                _soundsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds");
                Directory.CreateDirectory(_soundsDir);

                string heartbeatPath = Path.Combine(_soundsDir, "heartbeat.wav");
                string acceleratePath = Path.Combine(_soundsDir, "accelerate.wav");
                string deceleratePath = Path.Combine(_soundsDir, "decelerate.wav");

                if (!File.Exists(heartbeatPath))
                {
                    GenerateBeepWav(heartbeatPath, new[] { 950.0 }, 0.05, 0.6);
                }

                if (!File.Exists(acceleratePath))
                {
                    GenerateBeepWav(acceleratePath, new[] { 600.0, 800.0, 1000.0 }, 0.3, 0.7);
                }

                if (!File.Exists(deceleratePath))
                {
                    GenerateBeepWav(deceleratePath, new[] { 1000.0, 800.0, 600.0 }, 0.3, 0.7);
                }

                // Cache SoundPlayers
                _heartbeatPlayer = new SoundPlayer(heartbeatPath);
                _heartbeatPlayer.Load();

                _acceleratePlayer = new SoundPlayer(acceleratePath);
                _acceleratePlayer.Load();

                _deceleratePlayer = new SoundPlayer(deceleratePath);
                _deceleratePlayer.Load();

                AppLogger.Info("SoundManager", "Initialized sounds successfully.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("SoundManager", "Failed to initialize SoundManager", ex);
            }
        }

        public static void PlayHeartbeat()
        {
            Task.Run(() =>
            {
                try
                {
                    _heartbeatPlayer?.Play();
                }
                catch (Exception ex)
                {
                    AppLogger.Debug("SoundManager", $"Failed to play heartbeat sound: {ex.Message}");
                }
            });
        }

        public static void PlayAccelerate()
        {
            Task.Run(() =>
            {
                try
                {
                    AppLogger.Info("SoundManager", "Playing 'Accelerate' alert");
                    _acceleratePlayer?.Play();
                }
                catch (Exception ex)
                {
                    AppLogger.Error("SoundManager", "Failed to play accelerate sound", ex);
                }
            });
        }

        public static void PlayDecelerate()
        {
            Task.Run(() =>
            {
                try
                {
                    AppLogger.Info("SoundManager", "Playing 'Decelerate' alert");
                    _deceleratePlayer?.Play();
                }
                catch (Exception ex)
                {
                    AppLogger.Error("SoundManager", "Failed to play decelerate sound", ex);
                }
            });
        }

        private static void GenerateBeepWav(string filePath, double[] frequencies, double durationSeconds, double volume)
        {
            int sampleRate = 8000;
            short bitsPerSample = 16;
            int samplesCount = (int)(sampleRate * durationSeconds);
            int dataLength = samplesCount * (bitsPerSample / 8);

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + dataLength);
                writer.Write("WAVE".ToCharArray());
                writer.Write("fmt ".ToCharArray());
                writer.Write(16); // Subchunk1Size
                writer.Write((short)1); // AudioFormat (PCM)
                writer.Write((short)1); // NumChannels (Mono)
                writer.Write(sampleRate);
                writer.Write(sampleRate * (bitsPerSample / 8)); // ByteRate
                writer.Write((short)(bitsPerSample / 8)); // BlockAlign
                writer.Write(bitsPerSample);
                writer.Write("data".ToCharArray());
                writer.Write(dataLength);

                int notesCount = frequencies.Length;
                int samplesPerNote = samplesCount / notesCount;

                for (int i = 0; i < samplesCount; i++)
                {
                    int noteIndex = Math.Min(i / samplesPerNote, notesCount - 1);
                    double freq = frequencies[noteIndex];

                    double t = (double)i / sampleRate;
                    short sample = (short)(Math.Sin(2 * Math.PI * freq * t) * volume * short.MaxValue);
                    writer.Write(sample);
                }
            }
        }
    }
}
