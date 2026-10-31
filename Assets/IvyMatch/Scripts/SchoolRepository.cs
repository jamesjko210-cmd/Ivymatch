using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace IvyMatch
{
    /// <summary>
    /// Task wrapper for UnityWebRequestAsyncOperation:
    ///   await request.SendWebRequest().AsTask();
    /// Explicit AsTask() avoids clashing with Unity 6's built-in
    /// AsyncOperation.GetAwaiter() (Awaitable) extension.
    /// Continuations resume on the Unity main thread (UnitySynchronizationContext).
    /// </summary>
    public static class UnityWebRequestAwaitExtensions
    {
        public static Task<UnityWebRequest> AsTask(this UnityWebRequestAsyncOperation op)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            if (op.isDone)
            {
                tcs.TrySetResult(op.webRequest);
            }
            else
            {
                op.completed += _ => tcs.TrySetResult(op.webRequest);
            }
            return tcs.Task;
        }
    }

    /// <summary>
    /// Loads schools.json: persistentDataPath copy (remote refresh) first, StreamingAssets fallback.
    /// Never throws from LoadAsync / TryRefreshAsync; check LastError.
    /// </summary>
    public class SchoolRepository
    {
        public const string FileName = "schools.json";
        const int RequestTimeoutSeconds = 15;

        /// <summary>Last error message, or null/empty if the last operation succeeded.</summary>
        public string LastError { get; private set; }

        /// <summary>Where the last LoadAsync got its data: "persistent", "streaming" or "none".</summary>
        public string LastSource { get; private set; } = "none";

        public static string PersistentPath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static string StreamingPath
        {
            get { return Path.Combine(Application.streamingAssetsPath, FileName); }
        }

        /// <summary>
        /// Loads the database. Order: persistentDataPath/schools.json (if present and valid),
        /// then StreamingAssets/schools.json. If both fail, returns an EMPTY database
        /// (schools.Count == 0, never null) and sets LastError.
        /// </summary>
        public async Task<SchoolDatabase> LoadAsync()
        {
            LastError = null;
            LastSource = "none";

            // 1) Remote-refreshed copy.
            try
            {
                string p = PersistentPath;
                if (File.Exists(p))
                {
                    string json = File.ReadAllText(p, Encoding.UTF8);
                    SchoolDatabase db = Parse(json);
                    LastSource = "persistent";
                    return db;
                }
            }
            catch (Exception e)
            {
                // Corrupt cache: fall through to bundled data.
                LastError = "persistent copy unusable: " + e.Message;
            }

            // 2) Bundled copy.
            try
            {
                string json = await ReadStreamingAssetAsync();
                SchoolDatabase db = Parse(json);
                LastSource = "streaming";
                return db;
            }
            catch (Exception e)
            {
                LastError = "streaming load failed: " + e.Message;
                Debug.LogWarning("[IvyMatch] " + LastError);
                return new SchoolDatabase();
            }
        }

        /// <summary>
        /// Downloads url, validates (schools non-empty), saves to persistentDataPath/schools.json.
        /// Returns true on success; any failure returns false (never throws), existing data untouched.
        /// </summary>
        public async Task<bool> TryRefreshAsync(string url)
        {
            LastError = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                LastError = "remote url is empty";
                return false;
            }

            try
            {
                string body;
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = RequestTimeoutSeconds;
                    await req.SendWebRequest().AsTask();
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        LastError = "download failed: " + req.error;
                        return false;
                    }
                    body = req.downloadHandler.text;
                }

                Parse(body); // throws if invalid or empty

                string path = PersistentPath;
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, body, new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                LastError = "refresh failed: " + e.Message;
                return false;
            }
        }

        /// <summary>
        /// Parses and validates. Throws InvalidDataException if the JSON is empty/invalid or has no schools.
        /// Normalizes null lists/objects so UI code can iterate safely.
        /// </summary>
        public static SchoolDatabase Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("empty json");

            SchoolDatabase db = JsonConvert.DeserializeObject<SchoolDatabase>(json);
            if (db == null || db.schools == null || db.schools.Count == 0)
                throw new InvalidDataException("no schools in json");

            db.schools.RemoveAll(s => s == null || string.IsNullOrEmpty(s.id));
            if (db.schools.Count == 0)
                throw new InvalidDataException("no valid schools in json");

            foreach (School s in db.schools)
            {
                if (s.stats == null) s.stats = new Stats();
                if (s.admissions == null) s.admissions = new Admissions();
                if (s.admissions.rounds == null) s.admissions.rounds = new List<Round>();
                s.admissions.rounds.RemoveAll(r => r == null || string.IsNullOrEmpty(r.type));
                if (s.cost == null) s.cost = new Cost();
                if (s.intl == null) s.intl = new Intl();
                if (s.majors != null)
                {
                    if (s.majors.bachelor == null) s.majors.bachelor = new List<MajorShare>();
                    s.majors.bachelor.RemoveAll(m => m == null || string.IsNullOrEmpty(m.cip));
                }
                // v1 files only have intl.needBlind; v2 reads cost.needBlindIntl. Keep both in sync.
                if (s.cost.needBlindIntl == null) s.cost.needBlindIntl = s.intl.needBlind;
                if (s.intl.needBlind == null) s.intl.needBlind = s.cost.needBlindIntl;
                if (s.tags == null) s.tags = new List<string>();
                if (s.strategy == null) s.strategy = new List<string>();
                if (s.lookFor == null) s.lookFor = new List<string>();
                if (s.compare == null) s.compare = new List<string>();
                if (s.sources == null) s.sources = new List<SourceRef>();
                if (string.IsNullOrEmpty(s.status)) s.status = "unverified";
            }
            return db;
        }

        static async Task<string> ReadStreamingAssetAsync()
        {
            string path = StreamingPath;

            // Android (jar:file://...) and WebGL are URLs: File APIs cannot read them.
            if (path.Contains("://"))
            {
                using (UnityWebRequest req = UnityWebRequest.Get(path))
                {
                    await req.SendWebRequest().AsTask();
                    if (req.result != UnityWebRequest.Result.Success)
                        throw new IOException(req.error);
                    return req.downloadHandler.text;
                }
            }
            return File.ReadAllText(path, Encoding.UTF8);
        }
    }
}
