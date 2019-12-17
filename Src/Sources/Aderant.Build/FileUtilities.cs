using System.Globalization;
using System.IO;

namespace Aderant.Build {
    internal static class FileUtilities {

        internal static bool HasExtension(string fileName, string[] allowedExtensions) {
            string extension = Path.GetExtension(fileName);
            for (int i = 0; i < allowedExtensions.Length; i++) {
                string strB = allowedExtensions[i];
                if (string.Compare(extension, strB, true, CultureInfo.CurrentCulture) == 0) {
                    return true;
                }
            }
            return false;
        }

    }
}