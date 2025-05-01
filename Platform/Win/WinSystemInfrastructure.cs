using SharedLibrary.Servicers;
using Microsoft.Win32.TaskScheduler;
using SharedLibrary.Librarys;

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
            string taskName = "Taix";
#if DEBUG
            taskName += " debug";
#endif
            taskName += " task";
            try
            {
                var logonUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                string tai = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Taix.exe");
                
                if (!File.Exists(tai))
                {
                    Logger.Error("SetStartup Taix.exe not found");
                    return false;
                }

                string taskDescription = "Taix开机自启服务";

                using var taskService = new TaskService();
                
                bool deletionSuccess = true;
                try
                {
                    var tasks = taskService.RootFolder.GetTasks(new System.Text.RegularExpressions.Regex(taskName));
                    foreach (var t in tasks)
                    {
                        taskService.RootFolder.DeleteTask(t.Name);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString());
                    deletionSuccess = false;
                }

                if (startup)
                {
                    try
                    {
                        var task = taskService.NewTask();
                        task.RegistrationInfo.Description = taskDescription;
                        task.Triggers.Add(new LogonTrigger { UserId = logonUser });
                        task.Principal.RunLevel = TaskRunLevel.Highest;
                        task.Actions.Add(new ExecAction(tai, "--selfStart", AppDomain.CurrentDomain.BaseDirectory));
                        task.Settings.StopIfGoingOnBatteries = false;
                        task.Settings.DisallowStartIfOnBatteries = false;
                        taskService.RootFolder.RegisterTaskDefinition(taskName, task);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex.ToString());
                        return false;
                    }
                }
                
                return deletionSuccess;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                return false;
            }
        }
    }
}