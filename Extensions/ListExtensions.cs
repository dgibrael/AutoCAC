using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AutoCAC.Extensions
{
    public static class ListExtensions
    {
        public static T Next<T>(this List<T> list, T current)
        {
            if (list == null || list.Count == 0)
                return default;

            int i = list.IndexOf(current);
            if (i == -1)
                return default;

            // Wrap to first if at end
            int nextIndex = (i + 1) % list.Count;
            return list[nextIndex];
        }

        public static T Previous<T>(this List<T> list, T current)
        {
            if (list == null || list.Count == 0)
                return default;

            int i = list.IndexOf(current);
            if (i == -1)
                return default;

            // Wrap to last if at start
            int prevIndex = (i - 1 + list.Count) % list.Count;
            return list[prevIndex];
        }
    }
}
