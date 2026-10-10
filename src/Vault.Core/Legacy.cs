// 密码库 —— 本地密码管理器
// Copyright (C) 2026 ZhangGe
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your
// option) any later version. This program is distributed WITHOUT ANY
// WARRANTY; see the GNU General Public License for more details.
// <https://www.gnu.org/licenses/>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace 密码库.核心;

/// <summary>
/// 读 v1 库（PowerShell 版建的那种）并转成 v2。
///
/// v1 的两个区别：条目是**逐条**加密的；名称和备注是**明文**。
/// 转过来之后整库一起加密，明文元数据就没有了。
/// </summary>
public static class 旧版
{
    public static bool 像是旧版(string 路径)
    {
        try
        {
            using var 文档 = JsonDocument.Parse(File.ReadAllText(路径, Encoding.UTF8));
            var 根 = 文档.RootElement;
            // v1 没有 format 字段，但有中文的「盐」「校验」「条目」
            return !根.TryGetProperty("format", out _)
                && 根.TryGetProperty("盐", out _)
                && 根.TryGetProperty("校验", out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>把 v1 库读成一组条目。密码不对会抛 <see cref="打不开异常"/>。</summary>
    public static List<条目> 读出(string 路径, string 主密码)
    {
        using var 文档 = JsonDocument.Parse(File.ReadAllText(路径, Encoding.UTF8));
        var 根 = 文档.RootElement;

        var 盐 = Convert.FromBase64String(根.GetProperty("盐").GetString()!);
        var 迭代 = 根.TryGetProperty("迭代次数", out var 迭代元素) ? 迭代元素.GetInt32() : 加密.默认迭代次数;
        var 密钥 = 加密.派生密钥(主密码, 盐, 迭代);

        try
        {
            // v1 用一个固定串做校验块
            var 校验 = 根.GetProperty("校验");
            string 校验明文;
            try
            {
                校验明文 = Encoding.UTF8.GetString(解一块(密钥, 校验));
            }
            catch (CryptographicException)
            {
                throw new 打不开异常("主密码不对，或者这个旧库被改动过。");
            }
            if (校验明文 != "vault-ok") throw new 打不开异常("旧库的校验块异常，文件可能已损坏。");

            var 结果 = new List<条目>();
            if (!根.TryGetProperty("条目", out var 条目数组) || 条目数组.ValueKind != JsonValueKind.Array)
                return 结果;

            foreach (var 项 in 条目数组.EnumerateArray())
            {
                if (项.ValueKind != JsonValueKind.Object) continue;
                var 名称 = 取字符串(项, "名称");
                if (string.IsNullOrWhiteSpace(名称)) continue;

                string 值;
                try
                {
                    值 = Encoding.UTF8.GetString(解一块(密钥, 项));
                }
                catch (CryptographicException)
                {
                    throw new 打不开异常($"旧库里的「{名称}」解不开，这条被改动过或已损坏。");
                }

                var 条 = new 条目
                {
                    标题 = 名称,
                    备注 = 取字符串(项, "备注"),
                };

                // v1 一条只有一个值。多行的（PEM 私钥）放「附加」，单行的当密码。
                if (值.Contains('\n'))
                    条.附加 = 值.Replace("\r\n", "\n");
                else
                    条.密码 = 值;

                if (DateTimeOffset.TryParse(取字符串(项, "更新"), out var 更新时间))
                {
                    条.创建 = 更新时间;
                    条.更新 = 更新时间;
                }
                结果.Add(条);
            }
            return 结果;
        }
        finally
        {
            加密.抹掉(密钥);
        }
    }

    private static byte[] 解一块(byte[] 密钥, JsonElement 块)
        => 加密.解密数据(
            密钥,
            Convert.FromBase64String(块.GetProperty("nonce").GetString()!),
            Convert.FromBase64String(块.GetProperty("密文").GetString()!),
            Convert.FromBase64String(块.GetProperty("标签").GetString()!));

    private static string 取字符串(JsonElement 元素, string 名)
        => 元素.TryGetProperty(名, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
