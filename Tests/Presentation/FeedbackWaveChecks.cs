// Standalone reflection probe against the actual compiled presentation assembly; no Unity shim.
// Args: compiled assembly path, WAV output directory. Requires that assembly's Unity dependencies.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

public static class FeedbackWaveChecks
{
    const int SampleRate = 22050;
    static int checks;
    static void Check(bool valid, string reason) { checks++; if (!valid) throw new Exception(reason); }
    static void Wav(string path, float[] samples)
    {
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(SampleRate); writer.Write(SampleRate * 2);
            writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(samples.Length * 2);
            foreach (float value in samples) writer.Write((short)Math.Round(value * .32 * short.MaxValue));
        }
    }
    public static int Main(string[] args)
    {
        try
        {
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var type = assembly.GetType("OneMoreBonk.Rebuild.Presentation.RebuildWorkshop", true);
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var cueType = type.GetNestedType("StrikeCue", BindingFlags.NonPublic);
            var synth = type.GetMethod("SynthesizeStrikeCue", flags);
            var classify = type.GetMethod("StrikeFeedbackCue", flags);
            var resultType = assembly.GetType("OneMoreBonk.Rebuild.RiskStrikeResult", true);
            var outcomeType = assembly.GetType("OneMoreBonk.Rebuild.ForgeOutcome", true);
            Directory.CreateDirectory(args[1]);
            var unique = new HashSet<string>(); var sequence = new List<float>();
            foreach (var cue in Enum.GetValues(cueType))
            {
                var samples = (float[])synth.Invoke(null, new[] { cue });
                var repeat = (float[])synth.Invoke(null, new[] { cue });
                Check(samples.SequenceEqual(repeat), "Waveform depends on mutable random state: " + cue);
                Check(samples.Length >= SampleRate * .06 && samples.Length <= SampleRate * .5, "Unexpected cue duration: " + cue);
                Check(samples.All(x => !float.IsNaN(x) && !float.IsInfinity(x) && Math.Abs(x) <= .800001), "Nonfinite/clipping waveform: " + cue);
                Check(samples.Any(x => Math.Abs(x) > .01), "Silent cue: " + cue);
                Check(samples[0] == 0 && samples[samples.Length - 1] == 0, "Cue edges are discontinuous: " + cue);
                var bytes = new byte[samples.Length * sizeof(float)]; Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
                using (var sha = SHA256.Create()) unique.Add(Convert.ToBase64String(sha.ComputeHash(bytes)));
                Wav(Path.Combine(args[1], cue.ToString().ToLowerInvariant() + ".wav"), samples);
                sequence.AddRange(samples); sequence.AddRange(new float[SampleRate / 3]);
            }
            Check(unique.Count == 6, "Distinct outcome cues share an identical waveform");
            Wav(Path.Combine(args[1], "all-cues.wav"), sequence.ToArray());
            string[][] cases = {
                new[]{"Hold","Hold","","Hold"}, new[]{"Success","Success","","Success"},
                new[]{"Great","Great","","Great"}, new[]{"Damage","Damage","","Damage"},
                new[]{"Break","Break","ward","Protected"}, new[]{"Break","Break","phoenix","Protected"},
                new[]{"Break","Damage","cornerstone","Protected"}, new[]{"Break","Break","","Broken"}
            };
            foreach (var item in cases)
            {
                var result = Activator.CreateInstance(resultType);
                resultType.GetField("Accepted").SetValue(result, true);
                resultType.GetField("RolledOutcome").SetValue(result, Enum.Parse(outcomeType, item[0]));
                resultType.GetField("Outcome").SetValue(result, Enum.Parse(outcomeType, item[1]));
                resultType.GetField("ProtectionId").SetValue(result, item[2]);
                resultType.GetField("Fatal").SetValue(result, item[3] == "Broken");
                var fields = resultType.GetFields(); var before = fields.Select(x => x.GetValue(result)).ToArray();
                Check(classify.Invoke(null, new[] { result }).ToString() == item[3], "Wrong committed result cue: " + item[3]);
                Check(fields.Select(x => x.GetValue(result)).SequenceEqual(before), "Feedback classification mutated result fields");
            }
            Console.WriteLine("{\"status\":\"PASS\",\"assertions\":" + checks + ",\"waves\":6,\"scope\":\"math and result classification only; no audio-device or haptics execution\"}");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
