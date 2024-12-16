using Infrastructure.Servicers;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Win
{
    public class WinSystemInfrastructure : ISystemInfrastructure
    {
        public (string ostype, string version) GetOSVersionName()
        {
            return (string.Empty, string.Empty);
        }

        public bool SetStartup(bool startup = true)
        {
            string TaskName = "Taix task";
            var logonUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            string tai = Path.Combine(
                  AppDomain.CurrentDomain.BaseDirectory,
                   "Taix.exe");
            string taskDescription = "Taix开机自启服务";
            using (var taskService = new TaskService())
            {
                var tasks = taskService.RootFolder.GetTasks(new System.Text.RegularExpressions.Regex(TaskName));
                foreach (var t in tasks)
                {
                    taskService.RootFolder.DeleteTask(t.Name);
                }

                if (startup)
                {
                    var task = taskService.NewTask();
                    task.RegistrationInfo.Description = taskDescription;
                    task.Triggers.Add(new LogonTrigger { UserId = logonUser });
                    task.Principal.RunLevel = TaskRunLevel.Highest;
                    task.Actions.Add(new ExecAction(tai, "--selfStart", AppDomain.CurrentDomain.BaseDirectory));
                    task.Settings.StopIfGoingOnBatteries = false;
                    task.Settings.DisallowStartIfOnBatteries = false;
                    taskService.RootFolder.RegisterTaskDefinition(TaskName, task);
                }
            }
            return false;
        }
    }
}
