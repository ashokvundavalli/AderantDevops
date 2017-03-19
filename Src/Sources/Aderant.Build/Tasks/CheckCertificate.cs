using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Selectors;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class CheckCertificate : Microsoft.Build.Utilities.Task {
        public override bool Execute() {
            X509Store userStore = GetUserStore();

            try {
                userStore.Open(OpenFlags.ReadWrite);

                X509Certificate2Collection collection = userStore.Certificates.Find(X509FindType.FindByThumbprint, ThumbPrint, true);

                if (collection.Count == 0) {
                    if (!CopyCertificateFromMachineStore(userStore)) {
                        throw new InvalidOperationException(
                            "Cannot locate code signing certificate in the user store. The certificate was expected to be deployed by Active Directory.\r\nPlease contact SCM and the IT help desk.");
                    }

                    userStore.Close();

                    userStore = GetUserStore();

                    // Refresh the store as it should be installed now.
                    collection = userStore.Certificates.Find(X509FindType.FindByThumbprint, ThumbPrint, true);
                }

                Log.LogMessage("Code signing certificate installed.");

                var currentCertificate = collection[0];

                var expirationDate = DateTime.Parse(currentCertificate.GetExpirationDateString());
                var name = currentCertificate.Subject;

                if (expirationDate < DateTime.Now) {
                    throw new IdentityValidationException("Code signing certificate expired. Contact the IT help desk immediately.");
                }

                if ((expirationDate - DateTime.Now).TotalDays <= 30) {
                    string message = string.Format("The certificate: {0} has less than 30 days until it expires. The certificate is validate until: {1}", name, expirationDate);
                    LogCertificateStatusMessage(message, false);
                }

                if ((expirationDate - DateTime.Now).TotalDays <= 7) {
                    string message = string.Format("The certificate: {0} has less than 14 days until it expires. The certificate is validate until: {1}", name, expirationDate);
                    LogCertificateStatusMessage(message, true);
                }

                Log.LogMessage(MessageImportance.High, "Code signing certificate expires in: {0} days", Math.Round((expirationDate - DateTime.Now).TotalDays));
            } catch (Exception ex) {
                Log.LogErrorFromException(ex);
            } finally {
                userStore.Close();
            }

            return !Log.HasLoggedErrors;
        }

        private X509Store GetUserStore() {
            X509Store userStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            userStore.Open(OpenFlags.ReadWrite);

            return userStore;
        }

        private bool CopyCertificateFromMachineStore(X509Store userStore) {
            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
          
            store.Open(OpenFlags.ReadOnly);

            var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, ThumbPrint, true);
            if (certificates.Count > 0) {
                userStore.Add(certificates[0]);
                userStore.Close();

                return true;
            }

            store.Close();

            return false;
        }

        private void LogCertificateStatusMessage(string message, bool error) {
            EventLog eventLog = new EventLog();
            eventLog.Source = "Certificate Expiration Alert";
            eventLog.WriteEntry(message, error ? EventLogEntryType.Error : EventLogEntryType.Warning, 1001);

            if (error) {
                Log.LogError(message);
            } else {
                Log.LogWarning(message);
            }
        }

        [Required]
        public string ThumbPrint { get; private set; }
    }
}