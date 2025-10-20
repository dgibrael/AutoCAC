using Microsoft.AspNetCore.Components;

namespace AutoCAC.Extensions
{
    public static class NavigationExtensions
    {
        public static string GetPath(this NavigationManager navigationManager)
        {
            var uri = new Uri(navigationManager.Uri);
            var path = uri.AbsolutePath.TrimEnd('/');
            return path == string.Empty ? "/" : path + "/";
        }

        public static string GetPathWith(this NavigationManager navigationManager, string suffix)
        {
            var current = navigationManager.GetPath(); // already normalized
            suffix = suffix?.Trim('/') ?? string.Empty;

            if (string.IsNullOrEmpty(suffix))
                return current;

            return current + suffix + "/";
        }

        public static string GetParent(this NavigationManager navigationManager)
        {
            var uri = new Uri(navigationManager.Uri);
            var path = uri.AbsolutePath.TrimEnd('/');

            if (string.IsNullOrEmpty(path) || path == "/")
                return "/";

            var index = path.LastIndexOf('/');
            return index > 0 ? path[..index] : "/";
        }
        public static string GetParentWith(this NavigationManager navigationManager, string suffix)
        {
            if (string.IsNullOrWhiteSpace(suffix))
                return navigationManager.GetParent();

            var parent = navigationManager.GetParent();
            return parent.TrimEnd('/') + "/" + suffix.TrimStart('/');
        }
        public static void NavigateToRelative(this NavigationManager navigationManager, string suffix = null, bool fromParent = false)
        {
            var newUrl = fromParent ? navigationManager.GetParentWith(suffix) : navigationManager.GetPathWith(suffix);
            navigationManager.NavigateTo(newUrl);
        }
    }
}
