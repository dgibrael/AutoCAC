using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using System.Web;
namespace AutoCAC.Extensions
{
    public static class NavigationExtensions
    {
        public static string GetRelativeUrl(this NavigationManager navigationManager)
        {
            var rel = navigationManager.ToBaseRelativePath(navigationManager.Uri) ?? string.Empty;

            // strip hash if any
            var hashIdx = rel.IndexOf('#');
            if (hashIdx >= 0) rel = rel.Substring(0, hashIdx);

            // strip query if any
            var qIdx = rel.IndexOf('?');
            if (qIdx >= 0) rel = rel.Substring(0, qIdx);

            var path = "/" + rel.Trim('/');
            return path.Length == 0 ? "/" : path;
        }

        private static string GetPathRelative(this NavigationManager navigationManager)
        {
            var uri = new Uri(navigationManager.Uri);
            return uri.AbsolutePath.TrimEnd('/');
        }

        public static string GetQueryString(this NavigationManager navigationManager)
        {
            var qry = navigationManager.ToAbsoluteUri(navigationManager.Uri).Query.TrimStart('?');
            return !string.IsNullOrWhiteSpace(qry) ? qry : "";
        }

        public static Dictionary<string, string> GetQueryDictionary(this NavigationManager navigationManager)
        {
            var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);
            var parsed = QueryHelpers.ParseQuery(uri.Query);

            // Convert QueryHelpers' StringValues → string
            return parsed.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);
        }

        public static string GetPath(this NavigationManager navigationManager, string suffix = "", bool includeExistingQry = true
            , bool fromParent = false
            , Dictionary<string, string> query = null)
        {
            var current = fromParent ? navigationManager.GetParent() : navigationManager.GetPathRelative();
            suffix = suffix?.Trim('/') ?? string.Empty;

            var url = string.IsNullOrEmpty(suffix)
                ? current.TrimEnd('/')
                : $"{current.TrimEnd('/')}/{suffix}";

            // Merge existing query params if requested
            Dictionary<string, string> merged = null;

            if (includeExistingQry)
            {
                var existing = navigationManager.GetQueryDictionary();
                merged = new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);

                if (query != null)
                {
                    foreach (var kvp in query)
                        merged[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                merged = query;
            }

            // No query at all?
            if (merged == null || merged.Count == 0)
                return url;

            // Build query string
            var qs = string.Join("&",
                merged.Select(kvp =>
                    $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"
                )
            );

            return $"{url}?{qs}";
        }

        public static string GetParent(this NavigationManager navigationManager)
        {
            var path = navigationManager.GetPathRelative();

            if (string.IsNullOrEmpty(path) || path == "/")
                return "";

            var index = path.LastIndexOf('/');
            return index > 0 ? path[..index] : "";
        }

        public static void NavigateToRelative(this NavigationManager navigationManager, string suffix = null, bool includeExistingQry = true
            , bool fromParent = false, Dictionary<string, string> query = null)
        {
            var newUrl = navigationManager.GetPath(suffix, includeExistingQry, fromParent, query);
            navigationManager.NavigateTo(newUrl);
        }
        public static void ClearQryFromUrl(this NavigationManager navigationManager)
        {
            navigationManager.NavigateToRelative(null, false, false, null);
        }
        public static void RemoveQryParamFromUrl(this NavigationManager navigationManager, string key)
        {
            var qry = navigationManager.GetQueryDictionary();
            qry.Remove(key);
            navigationManager.NavigateToRelative(null, false, false, qry);
        }
        public static void PushGridTemplateToUrl(this NavigationManager navigationManager, int? templateId)
        {
            string keyName = "dataGridTemplateId";
            var qryDict = navigationManager.GetQueryDictionary();
            qryDict.TryGetValue(keyName, out var existingValue);
            string newValue = templateId?.ToString();
            if (existingValue == newValue) return;
            if (newValue == null)
            {
                navigationManager.RemoveQryParamFromUrl(keyName);
                return;
            }
            var query = new Dictionary<string, string> { [keyName] = templateId.ToString() };
            navigationManager.NavigateToRelative(null, false, false, query);
        }

        public static void RefreshPage(this NavigationManager navigationManager)
        {
            navigationManager.NavigateTo(navigationManager.Uri);
        }
    }
}
