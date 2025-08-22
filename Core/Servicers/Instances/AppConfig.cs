using System.Reflection;
using Core.Event;
using Core.Models.Config;
using Core.Models.Config.Link;
using Core.Servicers.Interfaces;
using Newtonsoft.Json;
using SharedLibrary.Librarys;

namespace Core.Servicers.Instances;

public class AppConfig : IAppConfig
{
    private readonly string fileName;
    private ConfigModel config;
    private ConfigModel oldConfig;

    public AppConfig()
    {
        fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "Data",
            "AppConfig.json");
    }

    public event AppConfigEventHandler ConfigChanged;

    public void Load()
    {
        var configText = string.Empty;
        try
        {
            if (File.Exists(fileName))
            {
                configText = File.ReadAllText(fileName);
                config = JsonConvert.DeserializeObject<ConfigModel>(configText);
            }
            else
            {
                //  创建基础配置
                CreateDefaultConfig();

                Save();
            }

            CopyToOldConfig();

            CheckOption(config);
        }
        catch (JsonSerializationException ex)
        {
            HandleLoadFail(ex.Message, configText);
        }
        catch (JsonReaderException ex)
        {
            HandleLoadFail(ex.Message, configText);
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
        }
    }

    public ConfigModel GetConfig()
    {
        return config;
    }


    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(fileName, JsonConvert.SerializeObject(config));

            ConfigChanged?.Invoke(oldConfig ?? config, config);

            CopyToOldConfig();
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
        }
    }

    private void CopyToOldConfig()
    {
        var configStr = JsonConvert.SerializeObject(config);
        oldConfig = JsonConvert.DeserializeObject<ConfigModel>(configStr);
    }

    private void HandleLoadFail(string err, string configText)
    {
        //  创建基础配置
        CreateDefaultConfig();

        Save();

        Logger.Error(err + "\r\nConfig content: " + configText);
    }

    private void CreateDefaultConfig()
    {
        config = new ConfigModel();
        config.Links = new List<LinkModel>();

        config.General = new GeneralModel();
        config.General.IsStartatboot = false;

        config.Behavior = new BehaviorModel();
    }

    private void CheckOption(object obj)
    {
        PropertyInfo[] properties = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

        foreach (var item in properties)
        {
            var name = item.Name;
            var value = item.GetValue(obj, null);
            if (value == null)
            {
                //配置项不存在时创建
                Type[] types = new Type[0];
                object[] objs = new object[0];

                var ctor = item.PropertyType.GetConstructor(types);
                if (ctor != null)
                {
                    var instance = ctor.Invoke(objs);
                    item.SetValue(obj, instance);
                }
            }
        }
    }
}