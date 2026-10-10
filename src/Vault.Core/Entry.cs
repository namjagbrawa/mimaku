// 密码库 —— 本地密码管理器
// Copyright (C) 2026 ZhangGe
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your
// option) any later version. This program is distributed WITHOUT ANY
// WARRANTY; see the GNU General Public License for more details.
// <https://www.gnu.org/licenses/>

using System.Text.Json.Serialization;

namespace 密码库.核心;

/// <summary>
/// 一条记录。**整条都在加密区里**，包括标题和备注——
/// 不解锁连"你有哪些账号"都看不到，这是当初设计 v1 时没做到的地方。
/// </summary>
public sealed class 条目
{
    /// <summary>改名也不变的身份。用来做去重和以后的同步。</summary>
    [JsonPropertyName("id")]
    public string 编号 { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("title")]
    public string 标题 { get; set; } = "";

    /// <summary>随手分组用，比如「服务器」「银行」「网站」。空着也行。</summary>
    [JsonPropertyName("group")]
    public string 分类 { get; set; } = "";

    [JsonPropertyName("username")]
    public string 用户名 { get; set; } = "";

    [JsonPropertyName("password")]
    public string 密码 { get; set; } = "";

    [JsonPropertyName("url")]
    public string 网址 { get; set; } = "";

    [JsonPropertyName("notes")]
    public string 备注 { get; set; } = "";

    /// <summary>多行的东西：PEM 私钥、恢复码、一堆 token。换行一律存 LF。</summary>
    [JsonPropertyName("extra")]
    public string 附加 { get; set; } = "";

    [JsonPropertyName("created")]
    public DateTimeOffset 创建 { get; set; } = DateTimeOffset.Now;

    [JsonPropertyName("updated")]
    public DateTimeOffset 更新 { get; set; } = DateTimeOffset.Now;

    /// <summary>密码上次改动的时间，用来提示"这条太久没换了"。</summary>
    [JsonPropertyName("passwordChanged")]
    public DateTimeOffset? 密码更新 { get; set; }

    public 条目 复制()
    {
        return new 条目
        {
            编号 = 编号,
            标题 = 标题,
            分类 = 分类,
            用户名 = 用户名,
            密码 = 密码,
            网址 = 网址,
            备注 = 备注,
            附加 = 附加,
            创建 = 创建,
            更新 = 更新,
            密码更新 = 密码更新,
        };
    }

    /// <summary>搜索框用的匹配。<b>不匹配密码字段</b>——搜索不该成为翻密码的旁路。</summary>
    public bool 命中(string 关键词)
    {
        if (string.IsNullOrWhiteSpace(关键词)) return true;
        foreach (var 词 in 关键词.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var 有 =
                标题.Contains(词, StringComparison.OrdinalIgnoreCase) ||
                分类.Contains(词, StringComparison.OrdinalIgnoreCase) ||
                用户名.Contains(词, StringComparison.OrdinalIgnoreCase) ||
                网址.Contains(词, StringComparison.OrdinalIgnoreCase) ||
                备注.Contains(词, StringComparison.OrdinalIgnoreCase);
            if (!有) return false;   // 多个词是「都要满足」
        }
        return true;
    }
}

/// <summary>加密区里装的东西。以后要加字段（附件、TOTP）就往这儿加。</summary>
public sealed class 库内容
{
    [JsonPropertyName("entries")]
    public List<条目> 条目表 { get; set; } = new();
}
