using System.IO;
using AsmodatStandard.Extensions;

namespace AsmodatStateManager.Model
{
    public static class DiskInfoEx
    {
        public static bool NameEquals(this DiskInfo di, string name)
        {
            var diName = di?.name ?? "";

            if (!diName.ContainsAny("/", "\\", ":")) // in case of linux name is a label
                diName = di.label;

            if (diName.IsNullOrEmpty() || name.IsNullOrEmpty())
                return false;

            if (diName == name)
                return true;

            var s1 = name.ToLower().ReplaceMany((" ", ""), (":", ""), ("/", ""), ("\\", ""), ("\"", ""));
            var s2 = diName?.ToLower().ReplaceMany((" ", ""), (":", ""), ("/", ""), ("\\", ""), ("\"", ""));
            return s1 == s2;
        }
    }
    public class DiskInfo
    {
        public string name { get; set; }
        public string type { get; set; }
        public bool ready { get; set; }

        public string label { get; set; }
        public string format { get; set; }
        public long size { get; set; }
        /// <summary>
        /// Free Space %
        /// </summary>
        public float free { get; set; }
    }
}
