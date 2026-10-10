// 密码库 —— 本地密码管理器
// Copyright (C) 2026 ZhangGe
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your
// option) any later version. This program is distributed WITHOUT ANY
// WARRANTY; see the GNU General Public License for more details.
// <https://www.gnu.org/licenses/>

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace 密码库.界面;

/// <summary>
/// 程序自己的设置。**这里绝对不放任何凭据**——只有库文件在哪、窗口多大这类东西。
/// 放在 %APPDATA%\密码库\ 下面。
/// </summary>
public sealed class 设置
{
    [JsonPropertyName("vaultPath")] public string 库路径 { get; set; } = "";
    [JsonPropertyName("autoLockMinutes")] public int 自动锁定分钟 { get; set; } = 10;
    [JsonPropertyName("clipboardClearSeconds")] public int 剪贴板清除秒 { get; set; } = 30;
    [JsonPropertyName("windowWidth")] public int 窗宽 { get; set; }
    [JsonPropertyName("windowHeight")] public int 窗高 { get; set; }

    public static string 配置目录 =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "密码库");

    public static string 配置文件 => Path.Combine(配置目录, "settings.json");

    /// <summary>没设置过时，库放这儿。</summary>
    public static string 默认库路径 => Path.Combine(配置目录, "密码库.vault");

    public static 设置 读取()
    {
        try
        {
            if (File.Exists(配置文件))
            {
                var s = JsonSerializer.Deserialize<设置>(File.ReadAllText(配置文件, Encoding.UTF8));
                if (s != null) return s;
            }
        }
        catch
        {
            // 设置文件坏了不该让程序打不开，用默认值就是了
        }
        return new 设置();
    }

    public void 保存()
    {
        try
        {
            Directory.CreateDirectory(配置目录);
            File.WriteAllText(配置文件,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
        }
        catch
        {
            // 存不下设置也不影响用
        }
    }
}
