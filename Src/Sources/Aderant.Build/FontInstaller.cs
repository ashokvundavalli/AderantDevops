using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;

namespace Aderant.Build {
    internal sealed class FontInstaller {
        internal static ICollection<FontFamily> InstallFonts() {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string[] names = assembly.GetManifestResourceNames();

            string temporaryDirectory = null;
            temporaryDirectory = Path.Combine(Path.GetTempPath(), assembly.GetName().Name);

            foreach (string name in names) {
                if (name.EndsWith("otf", StringComparison.OrdinalIgnoreCase)) {
                    using (Stream resource = assembly.GetManifestResourceStream(name))
                        if (resource != null) {
                            string plainFileName = Path.GetFileNameWithoutExtension(name);
                            string extension = Path.GetExtension(name);

                            if (plainFileName != null) {
                                plainFileName = plainFileName.Split('.').Last();
                            }
                            string fileName = Path.ChangeExtension(plainFileName, extension);
                            fileName = Path.Combine(temporaryDirectory, fileName);

                            if (!File.Exists(fileName)) {
                                if (!Directory.Exists(temporaryDirectory)) {
                                    Directory.CreateDirectory(temporaryDirectory);
                                }

                                using (var fileStream = new FileStream(fileName, FileMode.Create)) {
                                    resource.CopyTo(fileStream);
                                }
                            }
                        }
                }
            }

            // Could not get this to work with pack URIs so the font has to come from disk.
            return Fonts.GetFontFamilies(temporaryDirectory + "/");
        }
    }
}