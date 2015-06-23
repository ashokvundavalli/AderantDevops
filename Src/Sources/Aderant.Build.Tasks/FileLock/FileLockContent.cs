/* 
 * License
 * file-lock is licensed under Apache V2.0 - see License for details.
 * https://github.com/markedup-mobi/file-lock
*/

using System.Runtime.Serialization;

namespace Aderant.Build.Tasks.FileLock {
    /// <summary>
    /// Class which gets serialized into the file lock - responsible for letting conflicting
    /// processes know which process owns this lock, when the lock was acquired, and the process name.
    /// </summary>
    [DataContract]
    public class FileLockContent
    {
        /// <summary>
        /// The process ID
        /// </summary>
        [DataMember]
        public long PID { get; set; }

        /// <summary>
        /// The timestamp (DateTime.Now.Ticks)
        /// </summary>
        [DataMember]
        public long Timestamp { get; set; }

        /// <summary>
        /// The name of the process
        /// </summary>
        [DataMember]
        public string ProcessName { get; set; }
    }
}