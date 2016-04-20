using Microsoft.Build.Framework;
using System;

namespace Baseclass.Contrib.Nuget.Output.Build
{
    public static class ITaskExtensions
    {
        public static void LogError(this ITask task, string message, params object[] messageArgs)
        {
            var buildEngine = task.BuildEngine;
            buildEngine.LogErrorEvent(new BuildErrorEventArgs(null, null, buildEngine.ProjectFileOfTaskNode, buildEngine.LineNumberOfTaskNode, buildEngine.ColumnNumberOfTaskNode, 0, 0, string.Format(message, messageArgs), null, task.GetType().Name));
        }

        public static void LogMessage(this ITask task,string message, params object[] messageArgs)
        {
            var buildEngine = task.BuildEngine;
            
            BuildMessageEventArgs e = new BuildMessageEventArgs(string.Format(message, messageArgs), (string)null, task.GetType().Name, MessageImportance.Normal);
            buildEngine.LogMessageEvent(e);
        }
    }
}
