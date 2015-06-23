using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Aderant.Build.Tasks {
    public sealed class SourceIndexStream {

        private static char[] splitArray = Environment.NewLine.ToCharArray();

        internal static string ModifySourceIndexStream(string streamText) {
            var lines = streamText.Split(splitArray, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];

                if (line.StartsWith("TFS_EXTRACT_TARGET=")) {
                    lines[i] = ReplaceExtractionPath(line);
                }

                if (line.StartsWith("SRCSRV: source files -")) {
                    line = lines[i++];

                    while (!line.StartsWith("SRCSRV: end -")) {
                        line = lines[i];

                        if (!string.IsNullOrEmpty(line)) {
                            var result = AddShortFilePathVariable(line);

                            if (!string.IsNullOrEmpty(result)) {
                                lines[i] = result;
                            }
                        }

                        i++;
                    }
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string AddShortFilePathVariable(string line) {
            string[] parts = line.Split('*');

            if (parts.Length >= 2) {
                // The location of the file inside tfs
                string tfsPath = parts[2];

                string fileName = Path.GetFileName(tfsPath);

                if (fileName != null) {
                    int pos = tfsPath.IndexOf(fileName, StringComparison.CurrentCulture);
                    tfsPath = tfsPath.Substring(0, pos);
                }

                // By using a hash we end up with a fixed length string that can uniquely represent a location within TFS
                string pathHash = CreatePathHash(tfsPath);

                // Now append the new SrcSrv variable
                return string.Concat(line, "*", pathHash);
               
            }

            return null;
        }

        private static string CreatePathHash(string input) {
            using (MD5 md5Hash = MD5.Create()) {
                // Convert the input string to a byte array and compute the hash. 
                byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

                StringBuilder sBuilder = new StringBuilder();
                for (int i = 0; i < data.Length; i++) {
                    sBuilder.Append(data[i].ToString("x2"));
                }
                return sBuilder.ToString();
            }
        }

        internal static string ReplaceExtractionPath(string line) {
            /*
             * SrcSrv works as follows.
             * First, the client calls SrcSrvInit with the target path to be used as a base for all source file extractions. This path is stored in the special variable TARG.
             * When DbgHelp loads a module's .pdb file, it extracts the SrcSrv stream from the .pdb file and passes this data block to SrcSrv by calling SrcSrvLoadModule.
             *
             * We want to change the TARG variable as for some paths from TFS it will be very long. By default Visual Studio puts the files here
             * C:\Users\%username%\AppData\Local\SourceServer\VSTFSSERVER\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.Framework.Presentation\Scripting\Controllers\WindowEventArgs.cs\252821\WindowEventArgs.cs 
             * 
             * This path is too long as the SrcSrv client will just return an error.
             * Some experimentation has indicated if we write the files to C:\Temp it will work as the path is then short enough to satisfy SrcSrv.
             * 
             * The default TFS_EXTRACT_TARGET variable is assigned 
             * 
             * %targ%\%var2%%fnbksl%(%var3%)\%var4%\%fnfile%(%var5%)
             * 
             * %fnbksl% and %fnfile% are function calls (although they look the same as variables); 
             * 
             * %fnbksl% replaces forward slashes with backward slashes 
             * %fnfile% which fetches the file name from the file name from the input string (which is a fill path) 
             * 
             * Given this is the expanded path
             * 
             * C:\Temp\VSTFSSERVER\ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.Framework.Presentation\SmartFormLayoutDesigner\Controllers\BasicLayoutDataTemplateSelector.cs\252821\BasicLayoutDataTemplateSelector.cs
             * 
             * %targ% = C:\Users\%username%\AppData\Local\SourceServer\
             * %var2% = VSTFSSERVER
             * %fnbksl%(%var3%) = ExpertSuite\Dev\Framework\Modules\Libraries.Presentation\Src\Aderant.Framework.Presentation\SmartFormLayoutDesigner\Controllers\CurrencyToDecimalConverter.cs
             * %var4% = 252821
             * %fnfile%(%var5%) = CurrencyToDecimalConverter.cs
             * 
             * This is simply too long so we will replace the var3 argument with our hash of the path (fixed length) 
             * 
             */
            return line = @"TFS_EXTRACT_TARGET=%targ%\%var2%\%var6%\%var4%\%fnfile%(%var5%)";
        }
    }
}