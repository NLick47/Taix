using System;
using System.Runtime.InteropServices;

namespace Taix.Client.Shared.Librarys;

public static class TimeZoneHelper
{
    /// <summary>
    /// 跨平台获取 IANA 时区 ID
    /// </summary>
    public static string GetIanaTimeZoneId()
    {
        var local = TimeZoneInfo.Local;
        
        // Linux/macOS：直接是 IANA
        if (local.HasIanaId)
            return local.Id;
        
        // Windows：转换 Windows ID → IANA ID
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(local.Id, out string ianaId))
            return ianaId;
        
        // 终极兜底
        return "UTC";
    }
    
    /// <summary>
    /// 获取当前系统平台
    /// </summary>
    public static string GetPlatform() => RuntimeInformation.OSDescription;
}
