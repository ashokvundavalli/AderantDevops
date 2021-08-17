using System;
using System.Security.Cryptography;
using System.Text;

namespace Aderant.Build.Utilities {
    internal static class StringExtensions {

        /// <summary>
        /// Case insensitive version of String.Replace().
        /// </summary>
        /// <param name="s">String that contains patterns to replace</param>
        /// <param name="oldValue">Pattern to find</param>
        /// <param name="newValue">New pattern to replaces old</param>
        /// <param name="comparisonType">String comparison type</param>
        public static string Replace(this string s, string oldValue, string newValue, StringComparison comparisonType) {
            if (s == null) {
                return null;
            }

            if (string.IsNullOrEmpty(oldValue)) {
                return s;
            }

            StringBuilder result = new StringBuilder();
            int pos = 0;

            while (true) {
                int i = s.IndexOf(oldValue, pos, comparisonType);
                if (i < 0) {
                    break;
                }

                result.Append(s, pos, i - pos);
                result.Append(newValue);

                pos = i + oldValue.Length;
            }

            result.Append(s, pos, s.Length - pos);

            return result.ToString();
        }

        public static string[] ToStringArray(this string str) {
            return new[] { str };
        }

        /// <summary>
        /// Generates a deterministic GUID based off the provided path.
        /// </summary>
        internal static Guid NewGuidFromPath(this string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(path));
            }

            using (MD5 md5 = MD5.Create()) {
                byte[] hash = md5.ComputeHash(Encoding.Default.GetBytes(path.ToUpperInvariant()));
                return new Guid(hash);
            }
        }

        /// <summary>
        /// Generates a a SHA-1 hash of the input
        /// </summary>
        public static string ComputeSha1Hash(this string hashInput) {
            using (var sha1 = SHA1.Create()) {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(hashInput.ToString()));
                var hashResult = new StringBuilder(hash.Length * 2);

                foreach (byte b in hash) {
                    hashResult.Append(b.ToString("x2"));
                }

                return hashResult.ToString();
            }
        }
    }
}