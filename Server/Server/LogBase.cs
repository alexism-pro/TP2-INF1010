using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public abstract class LogBase
    {
        protected readonly object lockObj = new object();
        public abstract void Log(string message);
        public abstract void ClearFile();
    }

    public class FileLogger : LogBase
    {
        public string FilePath { get; set; } = "./serverLogs.log";

        public override void Log(string message)
        {
            lock (lockObj)
            {
                using (var streamWriter = new StreamWriter(FilePath, true))
                {
                    var time = DateTime.Now;
                    var formattedTime = time.ToString("yyyy/MM/dd @ hh:mm:ss");
                    var fullMessage = $"{formattedTime}\t{message}";

                    streamWriter.WriteLine(fullMessage);
                    Console.WriteLine(fullMessage);
                    streamWriter.Close();
                }
            }
        }

        public override void ClearFile()
        {
            File.WriteAllText(FilePath, string.Empty);
        }
    }

    public static class LogHelper
    {
        private static LogBase logger = null;
        public static void Log(string message)
        {
            logger = new FileLogger();
            logger.Log(message);
        }

        public static void ClearFile()
        {
            logger = new FileLogger();
            logger.ClearFile();
        }
    }
}

