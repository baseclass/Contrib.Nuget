using Microsoft.Build.Framework;
using System;

namespace Baseclass.Contrib.Nuget.Output.Build
{
    public static class ITaskExtensions
    {
        public static void LogError(this ITask task, string message, params object[] messageArgs)
        {
            var buildEngine = task.BuildEngine;
            buildEngine.LogErrorEvent(new BuildErrorEventArgs(null, null, buildEngine.ProjectFileOfTaskNode, buildEngine.LineNumberOfTaskNode, buildEngine.ColumnNumberOfTaskNode, 0, 0, message, null, task.GetType().Name, DateTime.UtcNow, messageArgs));
        }

        public static void LogMessage(this ITask task,string message, params object[] messageArgs)
        {
            var buildEngine = task.BuildEngine;
            BuildMessageEventArgs e = new BuildMessageEventArgs(message, (string)null, task.GetType().Name, MessageImportance.Normal, DateTime.UtcNow, messageArgs);
            buildEngine.LogMessageEvent(e);
        }
    }
}
