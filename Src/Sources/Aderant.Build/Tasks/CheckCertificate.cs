using System;
using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Selectors;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class CheckCertificate : Task {
        private const string helpBlurb = "Please contact devops.ap@aderant.com and helpdesk@aderant.com.";

        [Required]
        public string ThumbPrint { get; private set; }

        public override bool Execute() {
            try {
                X509Certificate2Collection collection;

                using (X509Store store = GetUserStore(OpenFlags.ReadWrite)) {
                    collection = store.Certificates.Find(X509FindType.FindByThumbprint, ThumbPrint, true);

                    if (collection.Count == 0) {
                        if (!CopyCertificateFromMachineStore(store)) {
                            throw new InvalidOperationException(
                                "Cannot locate code signing certificate in the user store. The certificate was expected to be deployed by System Center. " + helpBlurb);
                        }
                    }
                }

                using (var store = GetUserStore(OpenFlags.ReadOnly)) {
                    collection = store.Certificates.Find(X509FindType.FindByThumbprint, ThumbPrint, true);

                    Log.LogMessage("Code signing certificate installed.");

                    var currentCertificate = collection[0];

                    // GetExpirationDateString returns the date format in the current culture
                    var expirationDate = DateTime.Parse(currentCertificate.GetExpirationDateString(), CultureInfo.CurrentCulture);
                    var name = currentCertificate.Subject;

                    if (expirationDate < DateTime.Now) {
                        throw new IdentityValidationException("Code signing certificate expired!. " + helpBlurb);
                    }

                    if ((expirationDate - DateTime.Now).TotalDays <= 30) {
                        string message = string.Format("The certificate: {0} has less than 30 days until it expires. The certificate is valid until: {1}", name, expirationDate);
                        LogCertificateStatusMessage(message, false);
                    }

                    if ((expirationDate - DateTime.Now).TotalDays <= 7) {
                        string message = string.Format("The certificate: {0} has less than 7 days until it expires. The certificate is valid until: {1}", name, expirationDate);
                        LogCertificateStatusMessage(message, true);
                    }

                    Log.LogMessage(MessageImportance.High, "Code signing certificate expires in: {0} days", Math.Round((expirationDate - DateTime.Now).TotalDays));
                }
            } catch (Exception ex) {
                Log.LogErrorFromException(ex);
            }

            return !Log.HasLoggedErrors;
        }

        private X509Store GetUserStore(OpenFlags readOnly) {
            X509Store userStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            userStore.Open(readOnly);

            return userStore;
        }

        private bool CopyCertificateFromMachineStore(X509Store userStore) {
            using (X509Store machineStore = new X509Store(StoreName.My, StoreLocation.LocalMachine)) {
                machineStore.Open(OpenFlags.ReadOnly);

                var certificates = machineStore.Certificates.Find(X509FindType.FindByThumbprint, ThumbPrint, true);
                if (certificates.Count > 0) {
                    userStore.Add(certificates[0]);
                    return true;
                }
            }

            return false;
        }

        private void LogCertificateStatusMessage(string message, bool error) {
            using (EventLog eventLog = new EventLog("Application")) {
                eventLog.Source = "Aderant Certificate Expiration Alert";
                eventLog.WriteEntry(message, error ? EventLogEntryType.Error : EventLogEntryType.Warning, 1001);

                if (error) {
                    Log.LogError(message);
                } else {
                    Log.LogWarning(message);
                }
            }
        }
    }
}