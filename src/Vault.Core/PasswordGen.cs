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

namespace 密码库.核心;

/// <summary>密码生成器。随机数用 <see cref="RandomNumberGenerator"/>，不是 Random。</summary>
public static class 密码生成
{
    public const string 小写表 = "abcdefghijklmnopqrstuvwxyz";
    public const string 大写表 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string 数字表 = "0123456789";
    public const string 符号表 = "!@#$%^&*()-_=+[]{};:,.?";

    /// <summary>容易看错、抄错的字符：大写 I O、小写 l o、数字 0 1。</summary>
    private const string 易混表 = "IOl0o1";

    public sealed class 选项
    {
        public int 长度 { get; set; } = 20;
        public bool 要小写 { get; set; } = true;
        public bool 要大写 { get; set; } = true;
        public bool 要数字 { get; set; } = true;
        public bool 要符号 { get; set; } = true;
        public bool 避开易混 { get; set; }
    }

    public static string 生成(选项 设置)
    {
        if (设置.长度 < 4) throw new ArgumentException("长度至少 4 位。");

        var 各组 = new List<string>();
        if (设置.要小写) 各组.Add(过滤(小写表, 设置.避开易混));
        if (设置.要大写) 各组.Add(过滤(大写表, 设置.避开易混));
        if (设置.要数字) 各组.Add(过滤(数字表, 设置.避开易混));
        if (设置.要符号) 各组.Add(符号表);
        if (各组.Count == 0) throw new ArgumentException("至少要选一类字符。");

        var 全池 = string.Concat(各组);
        var 结果 = new char[设置.长度];

        // 先保证每一类都至少出现一次，剩下的从全池里取
        int i = 0;
        foreach (var 组 in 各组) 结果[i++] = 取一个(组);
        for (; i < 设置.长度; i++) 结果[i] = 取一个(全池);

        洗牌(结果);
        return new string(结果);
    }

    private static string 过滤(string 表, bool 避开易混)
        => 避开易混 ? new string(表.Where(c => !易混表.Contains(c)).ToArray()) : 表;

    private static char 取一个(string 池) => 池[RandomNumberGenerator.GetInt32(池.Length)];

    /// <summary>Fisher–Yates，随机源同样用 CSPRNG。</summary>
    private static void 洗牌(char[] 数组)
    {
        for (int i = 数组.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (数组[i], 数组[j]) = (数组[j], 数组[i]);
        }
    }

    /// <summary>
    /// 粗略估个强度，单位是 bit。算法是 长度 × log2(字符池大小)，
    /// 只对"随机生成的"密码成立——手打的密码没这么强，别拿这个数字骗自己。
    /// </summary>
    public static double 估算熵(string 密码)
    {
        if (string.IsNullOrEmpty(密码)) return 0;
        int 池 = 0;
        if (密码.Any(char.IsLower)) 池 += 26;
        if (密码.Any(char.IsUpper)) 池 += 26;
        if (密码.Any(char.IsDigit)) 池 += 10;
        if (密码.Any(c => !char.IsLetterOrDigit(c))) 池 += 符号表.Length;
        return 池 == 0 ? 0 : 密码.Length * Math.Log2(池);
    }

    public static string 强度说法(double 熵)
        => 熵 switch
        {
            < 45 => "弱",
            < 65 => "一般",
            < 90 => "强",
            _ => "很强",
        };
}
