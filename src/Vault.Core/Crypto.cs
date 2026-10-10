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

/// <summary>
/// 加密原语。全部来自 .NET 基础类库，没有一行自创的密码学。
///
///   主密码 --PBKDF2-SHA256（60 万次）--> 256 位密钥 --AES-256-GCM--> 密文 + 认证标签
///
/// 和 PowerShell 版用的是同一套参数，所以两边的库文件可以互相打开。
/// </summary>
public static class 加密
{
    /// <summary>OWASP 对 PBKDF2-SHA256 的推荐值。改大了旧库照样能开（迭代次数存在文件里）。</summary>
    public const int 默认迭代次数 = 600_000;

    public const int 密钥字节数 = 32;   // AES-256
    public const int 盐字节数 = 16;
    public const int Nonce字节数 = 12;  // GCM 标准 nonce 长度
    public const int 标签字节数 = 16;

    public static byte[] 随机字节(int 长度)
    {
        var b = new byte[长度];
        RandomNumberGenerator.Fill(b);
        return b;
    }

    public static byte[] 派生密钥(string 主密码, byte[] 盐, int 迭代次数)
    {
        if (string.IsNullOrEmpty(主密码)) throw new ArgumentException("主密码不能为空。", nameof(主密码));
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(主密码), 盐, 迭代次数, HashAlgorithmName.SHA256, 密钥字节数);
    }

    /// <param name="附加数据">
    /// 参与认证但不加密的数据。这里传的是文件头（版本、迭代次数、盐），
    /// 这样有人把迭代次数偷偷改小，解密会直接失败而不是悄悄变弱。
    /// </param>
    public static (byte[] Nonce, byte[] 密文, byte[] 标签) 加密数据(
        byte[] 密钥, byte[] 明文, byte[]? 附加数据 = null)
    {
        var nonce = 随机字节(Nonce字节数);
        var 密文 = new byte[明文.Length];
        var 标签 = new byte[标签字节数];
        using var aes = new AesGcm(密钥, 标签字节数);
        aes.Encrypt(nonce, 明文, 密文, 标签, 附加数据);
        return (nonce, 密文, 标签);
    }

    /// <summary>解不开就抛 <see cref="CryptographicException"/>——主密码错、或者文件被改过一个字节。</summary>
    public static byte[] 解密数据(
        byte[] 密钥, byte[] nonce, byte[] 密文, byte[] 标签, byte[]? 附加数据 = null)
    {
        var 明文 = new byte[密文.Length];
        using var aes = new AesGcm(密钥, 标签字节数);
        aes.Decrypt(nonce, 密文, 标签, 明文, 附加数据);
        return 明文;
    }

    /// <summary>
    /// 把内存里的密钥清零。不是万无一失（GC 会搬内存、可能被换页到磁盘），
    /// 但用完就抹总比一直留着强。
    /// </summary>
    public static void 抹掉(byte[]? 字节)
    {
        if (字节 != null) CryptographicOperations.ZeroMemory(字节);
    }
}
