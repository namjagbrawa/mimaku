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

namespace 密码库.核心;

/// <summary>
/// 导出。**导出的文件是明文的**，这是它的用途（迁移、备份、换软件），
/// 也是它的危险。界面上必须显著警告，写完的文件权限要收紧。
///
/// 一个密码库如果出不去，就是把自己锁死在某个软件里。这个能力必须有。
/// </summary>
public static class 导出
{
    public static void 存成JSON(IEnumerable<条目> 条目们, string 路径)
    {
        var 文本 = JsonSerializer.Serialize(条目们.ToList(), new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        File.WriteAllText(路径, 文本, new UTF8Encoding(true));
        库.收紧权限(路径);
    }

    /// <summary>CSV 用 KeePass / Bitwarden 都认得的常见列名，方便以后搬家。</summary>
    public static void 存成CSV(IEnumerable<条目> 条目们, string 路径)
    {
        var sb = new StringBuilder();
        sb.AppendLine("group,title,username,password,url,notes,extra");
        foreach (var e in 条目们)
        {
            sb.Append(转义(e.分类)).Append(',')
              .Append(转义(e.标题)).Append(',')
              .Append(转义(e.用户名)).Append(',')
              .Append(转义(e.密码)).Append(',')
              .Append(转义(e.网址)).Append(',')
              .Append(转义(e.备注)).Append(',')
              .Append(转义(e.附加)).AppendLine();
        }
        // 带 BOM，Excel 打开中文才不乱码
        File.WriteAllText(路径, sb.ToString(), new UTF8Encoding(true));
        库.收紧权限(路径);
    }

    private static string 转义(string 值)
    {
        if (string.IsNullOrEmpty(值)) return "";
        var 需要引号 = 值.Contains(',') || 值.Contains('"') || 值.Contains('\n') || 值.Contains('\r');
        var 处理后 = 值.Replace("\"", "\"\"");
        return 需要引号 ? $"\"{处理后}\"" : 处理后;
    }

    /// <summary>从导出的 JSON 读回来。换主密码、合并两个库、或者从备份恢复时用。</summary>
    public static List<条目> 从JSON读入(string 路径)
    {
        var 文本 = File.ReadAllText(路径, Encoding.UTF8);
        var 结果 = JsonSerializer.Deserialize<List<条目>>(文本) ?? new List<条目>();
        foreach (var e in 结果)
        {
            // 导入的条目一律重新发编号，免得和现有条目撞上互相覆盖
            e.编号 = Guid.NewGuid().ToString("N");
        }
        return 结果;
    }
}
