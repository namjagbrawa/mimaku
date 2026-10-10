// 密码库 —— 本地密码管理器
// Copyright (C) 2026 ZhangGe
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your
// option) any later version. This program is distributed WITHOUT ANY
// WARRANTY; see the GNU General Public License for more details.
// <https://www.gnu.org/licenses/>

using System.Windows.Forms;

namespace 密码库.界面;

/// <summary>
/// 剪贴板。复制密码之后过一会儿自动清掉——但**只在剪贴板里还是我们放进去的那份时才清**，
/// 否则会把用户自己后来复制的东西一起抹了。
/// </summary>
internal static class 剪贴板
{
    private static string? _放过什么;
    private static System.Windows.Forms.Timer? _计时;

    public static int 几秒后清 { get; set; } = 30;

    /// <summary>返回给状态栏用的一句话。</summary>
    public static string 放上去(string 文, string 叫什么 = "内容")
    {
        try
        {
            Clipboard.SetText(文);
        }
        catch (Exception 错)
        {
            return $"复制失败：{错.Message}";
        }

        _放过什么 = 文;
        _计时?.Stop();
        _计时?.Dispose();
        _计时 = new System.Windows.Forms.Timer { Interval = Math.Max(5, 几秒后清) * 1000 };
        _计时.Tick += (_, _) => 清掉();
        _计时.Start();
        return $"{叫什么}已复制，{几秒后清} 秒后自动清空剪贴板。";
    }

    public static void 清掉()
    {
        _计时?.Stop();
        try
        {
            if (_放过什么 != null && Clipboard.ContainsText() &&
                string.Equals(Clipboard.GetText(), _放过什么, StringComparison.Ordinal))
            {
                Clipboard.Clear();
            }
        }
        catch
        {
            // 剪贴板被别的程序占着，清不了就算了
        }
        _放过什么 = null;
    }
}
