using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Bobbin
{
    [InitializeOnLoad]
    public class BobbinCore
    {
        const int MinimumUrlLength = 5;
        const int DefaultRequestTimeoutSeconds = 30;
        const string AssetsFolderName = "Assets";
        const string GoogleDocsHost = "docs.google.com";
        const string SpreadsheetPathPrefix = "/spreadsheets/d/";
        const string DocumentPathPrefix = "/document/d/";
        const string SpreadsheetExportPath = "/spreadsheets/export";
        const string DocumentExportPath = "/document/export";

        public static BobbinCore Instance;

        public static double lastRefreshTime { get; private set; }
        public static string lastReport { get; private set; }
        public static bool IsRefreshInProgress { get { return refreshInProgress; } }

        static bool refreshInProgress = false;

        static BobbinCore()
        {
            if (Instance == null)
            {
                Instance = new BobbinCore();
            }

            EditorApplication.update -= Instance.OnEditorUpdate;
            EditorApplication.update += Instance.OnEditorUpdate;
        }

        /// <summary>
        /// Allows Bobbin to refresh files even when the settings inspector is closed.
        /// </summary>
        void OnEditorUpdate()
        {
            var settings = BobbinSettings.Instance;
            if (!settings.autoRefresh || refreshInProgress)
            {
                return;
            }

            var interval = Math.Max(5.0, settings.refreshInterval);
            if (EditorApplication.timeSinceStartup > lastRefreshTime + interval)
            {
                StartRefresh();
            }
        }

        [MenuItem("Bobbin/Force Refresh All Files")]
        public static void DoRefresh()
        {
            Instance.StartRefresh();
        }

        [MenuItem("Bobbin/Add URLs and Settings...")]
        public static void OpenSettingsAsset()
        {
            Selection.activeObject = BobbinSettings.Instance;
        }

        public void StartRefresh()
        {
            if (refreshInProgress)
            {
                lastReport = "Bobbin refresh is already running. Please wait for it to finish.";
                return;
            }

            refreshInProgress = true;
            lastRefreshTime = EditorApplication.timeSinceStartup;

            try
            {
                EditorCoroutines.StartCoroutine(RefreshCoroutine(), this);
            }
            catch
            {
                refreshInProgress = false;
                throw;
            }
        }

        /// <summary>
        /// Fetches configured URLs and saves changed responses into the project.
        /// </summary>
        IEnumerator RefreshCoroutine()
        {
            var settings = BobbinSettings.Instance;
            settings.EnsureValidRoot();
            lastReport = "Bobbin started refresh at " + DateTime.Now.ToLongTimeString() + ", log is below:";

            try
            {
                for (int i = 0; i < settings.paths.Count; i++)
                {
                    var pathRefresh = RefreshPath(settings.paths[i], settings);
                    while (pathRefresh.MoveNext())
                    {
                        yield return pathRefresh.Current;
                    }
                }

                EditorUtility.SetDirty(settings);
            }
            finally
            {
                refreshInProgress = false;
            }
        }

        IEnumerator RefreshPath(BobbinPath currentPair, BobbinSettings settings)
        {
            if (currentPair == null || currentPair.depth < 0)
            {
                yield break;
            }

            var itemName = GetItemName(currentPair);
            if (!currentPair.enabled)
            {
                AppendReport("- {0}: DISABLED", itemName);
                yield break;
            }

            var sourceUrl = UnfixURL(currentPair.GetSourceUrl());
            if (string.IsNullOrEmpty(sourceUrl) || sourceUrl.Length < MinimumUrlLength)
            {
                AppendReport("- [ERROR] {0}: no URL defined, nothing to download", itemName);
                yield break;
            }

            currentPair.SetSourceUrl(sourceUrl);

            string assetPath;
            string fullPath;
            string pathError;
            if (!TryResolveAssetPath(currentPair.filePath, out assetPath, out fullPath, out pathError))
            {
                AppendReport("- [ERROR] {0}: {1}", itemName, pathError);
                yield break;
            }

            currentPair.filePath = assetPath;

            var downloadUrl = FixURL(sourceUrl, currentPair.sheetId);
            Uri requestUri;
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out requestUri))
            {
                AppendReport("- [ERROR] {0}: URL is not valid: {1}", itemName, sourceUrl);
                yield break;
            }

            using (var request = UnityWebRequest.Get(downloadUrl))
            {
                request.timeout = GetRequestTimeout(settings);
                yield return request.SendWebRequest();

                if (HasRequestError(request))
                {
                    var error = GetRequestError(request);
                    Debug.LogWarningFormat("Bobbin couldn't retrieve file at <{0}>. {1}", request.url, error);
                    AppendReport("- [ERROR] {0}: {1}", itemName, error);
                    yield break;
                }

                if (DownloadLooksLikeGoogleAccessError(request))
                {
                    Debug.LogWarningFormat("Bobbin couldn't retrieve file at <{0}> because the Google file is not publicly viewable.", request.url);
                    AppendReport("- [ERROR] {0}: Google returned a sign-in page. Enable public view access for this file.", itemName);
                    yield break;
                }

                SaveResponseIfChanged(currentPair, itemName, assetPath, fullPath, request.downloadHandler.data);
            }
        }

        static void SaveResponseIfChanged(BobbinPath currentPair, string itemName, string assetPath, string fullPath, byte[] data)
        {
            var checksum = Md5Sum(data);
            var fileExists = File.Exists(fullPath);
            var fileChanged = !fileExists || !string.Equals(currentPair.lastFileHash, checksum, StringComparison.OrdinalIgnoreCase);

            if (fileChanged)
            {
                var directoryPath = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.WriteAllBytes(fullPath, data);
                AssetDatabase.ImportAsset(assetPath);
                currentPair.lastFileHash = checksum;
                currentPair.assetReference = AssetDatabase.LoadAssetAtPath(assetPath, typeof(UnityEngine.Object));
                AppendReport("- {0}: UPDATED {1}", itemName, assetPath);
                return;
            }

            if (currentPair.assetReference == null)
            {
                AssetDatabase.ImportAsset(assetPath);
                currentPair.assetReference = AssetDatabase.LoadAssetAtPath(assetPath, typeof(UnityEngine.Object));
            }

            AppendReport("- {0}: UNCHANGED", itemName);
        }

        static bool TryResolveAssetPath(string value, out string assetPath, out string fullPath, out string error)
        {
            assetPath = null;
            fullPath = null;
            error = null;

            if (string.IsNullOrEmpty(value) || value.Trim().Length < MinimumUrlLength)
            {
                error = "no asset file path defined, nowhere to save";
                return false;
            }

            assetPath = value.Trim().Replace('\\', '/').TrimStart('/');
            if (!assetPath.StartsWith(AssetsFolderName + "/", StringComparison.Ordinal) &&
                !string.Equals(assetPath, AssetsFolderName, StringComparison.Ordinal))
            {
                error = "asset file path must be inside the Assets folder";
                return false;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            var assetsRoot = Path.GetFullPath(Application.dataPath);

            if (string.Equals(NormalizePath(fullPath), NormalizePath(assetsRoot), StringComparison.OrdinalIgnoreCase) ||
                !NormalizePath(fullPath).StartsWith(NormalizePath(assetsRoot) + "/", StringComparison.OrdinalIgnoreCase))
            {
                error = "asset file path resolves outside the Assets folder";
                return false;
            }

            return true;
        }

        static int GetRequestTimeout(BobbinSettings settings)
        {
            if (settings.requestTimeoutSeconds <= 0)
            {
                return DefaultRequestTimeoutSeconds;
            }

            return Mathf.Clamp(settings.requestTimeoutSeconds, 1, 600);
        }

        static bool HasRequestError(UnityWebRequest request)
        {
#if UNITY_2020_2_OR_NEWER
            return request.result == UnityWebRequest.Result.ConnectionError ||
                   request.result == UnityWebRequest.Result.ProtocolError ||
                   request.result == UnityWebRequest.Result.DataProcessingError;
#else
            return request.isNetworkError || request.isHttpError;
#endif
        }

        static string GetRequestError(UnityWebRequest request)
        {
            var error = string.IsNullOrEmpty(request.error) ? "unknown request error" : request.error;
            if (request.responseCode > 0)
            {
                return "HTTP " + request.responseCode + ": " + error;
            }

            return error;
        }

        static bool DownloadLooksLikeGoogleAccessError(UnityWebRequest request)
        {
            Uri uri;
            if (!Uri.TryCreate(request.url, UriKind.Absolute, out uri) || !IsGoogleDocsUri(uri))
            {
                return false;
            }

            var contentType = request.GetResponseHeader("Content-Type");
            if (!string.IsNullOrEmpty(contentType) &&
                contentType.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            var text = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
            return text.IndexOf("google-site-verification", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("ServiceLogin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (text.IndexOf("Sign in", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    text.IndexOf("Google", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static void AppendReport(string format, params object[] args)
        {
            lastReport += "\n" + string.Format(format, args);
        }

        static string GetItemName(BobbinPath path)
        {
            return string.IsNullOrEmpty(path.name) ? "Unnamed item" : path.name;
        }

        static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
        }

        /// <summary>
        /// Calculates file checksums so Bobbin can avoid unnecessary ImportAsset operations.
        /// </summary>
        public static string Md5Sum(byte[] bytes)
        {
            using (var md5 = MD5.Create())
            {
                var hashBytes = md5.ComputeHash(bytes);
                var hashString = new StringBuilder(hashBytes.Length * 2);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    hashString.Append(hashBytes[i].ToString("x2"));
                }

                return hashString.ToString();
            }
        }

        public static string FixURL(string url, string sheetNameOrId = "")
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            var trimmedUrl = url.Trim();
            Uri uri;
            if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out uri) || !IsGoogleDocsUri(uri))
            {
                return trimmedUrl;
            }

            string documentId;
            if (TryGetGoogleIdFromPath(uri.AbsolutePath, DocumentPathPrefix, out documentId))
            {
                return BuildDocumentExportUrl(documentId);
            }

            if (IsDocumentExportUrl(uri) && TryGetQueryParameter(uri.Query, "id", out documentId))
            {
                return BuildDocumentExportUrl(documentId);
            }

            string spreadsheetId;
            if (TryGetGoogleIdFromPath(uri.AbsolutePath, SpreadsheetPathPrefix, out spreadsheetId))
            {
                return BuildSpreadsheetExportUrl(spreadsheetId, GetSheetGid(sheetNameOrId, uri));
            }

            if (IsSpreadsheetExportUrl(uri) && TryGetQueryParameter(uri.Query, "id", out spreadsheetId))
            {
                return BuildSpreadsheetExportUrl(spreadsheetId, GetSheetGid(sheetNameOrId, uri));
            }

            return trimmedUrl;
        }

        public static string UnfixURL(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            var trimmedUrl = url.Trim();
            Uri uri;
            if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out uri) || !IsGoogleDocsUri(uri))
            {
                return trimmedUrl;
            }

            string documentId;
            if (IsDocumentExportUrl(uri) && TryGetQueryParameter(uri.Query, "id", out documentId))
            {
                return string.Format("https://docs.google.com/document/d/{0}/edit", documentId);
            }

            string spreadsheetId;
            if (IsSpreadsheetExportUrl(uri) && TryGetQueryParameter(uri.Query, "id", out spreadsheetId))
            {
                string gid;
                var editUrl = string.Format("https://docs.google.com/spreadsheets/d/{0}/edit", spreadsheetId);
                if (TryGetQueryParameter(uri.Query, "gid", out gid) && !string.IsNullOrEmpty(gid))
                {
                    editUrl += "#gid=" + UnityWebRequest.EscapeURL(gid);
                }

                return editUrl;
            }

            return trimmedUrl;
        }

        static bool IsGoogleDocsUri(Uri uri)
        {
            return string.Equals(uri.Host, GoogleDocsHost, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsDocumentExportUrl(Uri uri)
        {
            return IsGoogleDocsUri(uri) &&
                   string.Equals(uri.AbsolutePath, DocumentExportPath, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsSpreadsheetExportUrl(Uri uri)
        {
            return IsGoogleDocsUri(uri) &&
                   string.Equals(uri.AbsolutePath, SpreadsheetExportPath, StringComparison.OrdinalIgnoreCase);
        }

        static bool TryGetGoogleIdFromPath(string path, string prefix, out string id)
        {
            id = null;

            var start = path.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return false;
            }

            start += prefix.Length;
            if (start >= path.Length)
            {
                return false;
            }

            var end = path.IndexOf("/", start, StringComparison.Ordinal);
            id = end < 0 ? path.Substring(start) : path.Substring(start, end - start);
            if (string.Equals(id, "e", StringComparison.OrdinalIgnoreCase))
            {
                id = null;
                return false;
            }

            return !string.IsNullOrEmpty(id);
        }

        static string BuildDocumentExportUrl(string documentId)
        {
            return string.Format("https://docs.google.com/document/export?format=txt&id={0}", documentId);
        }

        static string BuildSpreadsheetExportUrl(string spreadsheetId, string sheetGid)
        {
            var url = string.Format("https://docs.google.com/spreadsheets/export?format=csv&id={0}", spreadsheetId);
            if (!string.IsNullOrEmpty(sheetGid))
            {
                url += "&gid=" + UnityWebRequest.EscapeURL(sheetGid);
            }

            return url;
        }

        static string GetSheetGid(string sheetNameOrId, Uri uri)
        {
            var gid = NormalizeSheetGid(sheetNameOrId);
            if (!string.IsNullOrEmpty(gid))
            {
                return gid;
            }

            if (TryGetQueryParameter(uri.Fragment, "gid", out gid))
            {
                return gid;
            }

            if (TryGetQueryParameter(uri.Query, "gid", out gid))
            {
                return gid;
            }

            return string.Empty;
        }

        static string NormalizeSheetGid(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var trimmedValue = value.Trim();
            Uri uri;
            string gid;
            if (Uri.TryCreate(trimmedValue, UriKind.Absolute, out uri))
            {
                if (TryGetQueryParameter(uri.Fragment, "gid", out gid))
                {
                    return gid;
                }

                if (TryGetQueryParameter(uri.Query, "gid", out gid))
                {
                    return gid;
                }
            }

            if (TryGetQueryParameter(trimmedValue, "gid", out gid))
            {
                return gid;
            }

            if (trimmedValue.StartsWith("gid=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmedValue.Substring("gid=".Length);
            }

            return trimmedValue.TrimStart('#');
        }

        static bool TryGetQueryParameter(string query, string name, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(query))
            {
                return false;
            }

            var normalizedQuery = query.TrimStart('?', '#');
            var parts = normalizedQuery.Split('&');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                var pair = parts[i].Split(new[] { '=' }, 2);
                var key = DecodeQueryValue(pair[0]);
                if (!string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = pair.Length > 1 ? DecodeQueryValue(pair[1]) : string.Empty;
                return true;
            }

            return false;
        }

        static string DecodeQueryValue(string value)
        {
            return Uri.UnescapeDataString(value.Replace("+", " "));
        }
    }
}
