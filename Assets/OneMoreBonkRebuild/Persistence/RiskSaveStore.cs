using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace OneMoreBonk.Rebuild
{
    // The Session publishes a candidate only after this complete payload is durable.
    public interface IRiskSaveStore
    {
        bool TryRead(out string payload, out bool found, out string reason);
        bool TryWrite(string payload, out string reason);
    }

    public sealed class RiskFileSaveStore : IRiskSaveStore
    {
        [Serializable]
        sealed class Envelope
        {
            public int Format = 1;
            public string Payload;
            public string Sha256;
        }

        readonly string path;
        public string Path => path;

        public RiskFileSaveStore(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("A dedicated rebuild save path is required.", nameof(savePath));
            path = System.IO.Path.GetFullPath(savePath);
        }

        static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        static bool ReadOne(string file, out string payload, out string reason)
        {
            payload = null;
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(file, Encoding.UTF8));
                if (envelope == null || envelope.Format != 1 || string.IsNullOrEmpty(envelope.Payload) ||
                    !string.Equals(envelope.Sha256, Hash(envelope.Payload), StringComparison.Ordinal))
                    throw new InvalidDataException("The save checksum or envelope format is invalid.");
                payload = envelope.Payload;
                reason = null;
                return true;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        public bool TryRead(out string payload, out bool found, out string reason)
        {
            payload = null;
            found = File.Exists(path) || File.Exists(path + ".bak");
            if (!found) { reason = null; return true; }
            if (File.Exists(path) && ReadOne(path, out payload, out reason)) return true;
            if (File.Exists(path + ".bak") && ReadOne(path + ".bak", out payload, out reason))
            { reason = "Recovered the previous verified backup; the latest primary save was unavailable."; return true; }
            reason = "Neither the primary save nor its backup could be verified. Existing files were kept.";
            return false;
        }

        public bool TryWrite(string payload, out string reason)
        {
            if (string.IsNullOrEmpty(payload)) { reason = "Refusing to write an empty player state."; return false; }
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                string envelope = JsonUtility.ToJson(new Envelope { Payload = payload, Sha256 = Hash(payload) });
                byte[] bytes = Encoding.UTF8.GetBytes(envelope);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                // Never delete the primary file to emulate replacement: a failed replace leaves it usable.
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
                reason = null;
                return true;
            }
            catch (Exception error)
            {
                reason = "Save failed: " + error.Message;
                return false;
            }
        }
    }

    public static class RiskStateCodec
    {
        public static string Encode(RiskState value)
        {
            value.HasRun = value.Run != null;
            value.HasLastResult = value.LastResult != null;
            value.HasPendingReward = value.PendingReward != null;
            if (value.Run != null) value.Run.HasPendingBreak = value.Run.PendingBreak != null;
            return JsonUtility.ToJson(value);
        }

        public static RiskState Decode(string value)
        {
            var state = JsonUtility.FromJson<RiskState>(value);
            if (state == null) throw new InvalidDataException("Player state is missing.");
            // Unity serializes inline custom classes by value. Explicit presence bits distinguish
            // a real record from the default object a native JSON round-trip can materialize.
            if (!state.HasRun) state.Run = null;
            if (!state.HasLastResult) state.LastResult = null;
            if (!state.HasPendingReward) state.PendingReward = null;
            if (state.Run != null && !state.Run.HasPendingBreak) state.Run.PendingBreak = null;
            return state;
        }
        public static RiskState Copy(RiskState value) => Decode(Encode(value));
    }

    public sealed partial class RiskState
    {
        public bool HasRun;
        public bool HasLastResult;
        public bool HasPendingReward;
    }

    public sealed partial class RiskRun
    {
        public bool HasPendingBreak;
    }
}
