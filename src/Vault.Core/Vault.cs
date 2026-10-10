// 密码库 —— 本地密码管理器
// Copyright (C) 2026 ZhangGe
//
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your
// option) any later version. This program is distributed WITHOUT ANY
// WARRANTY; see the GNU General Public License for more details.
// <https://www.gnu.org/licenses/>

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace 密码库.核心;

/// <summary>主密码不对，或者文件被人动过。两种情况分不开，这是 GCM 的设计使然。</summary>
public sealed class 打不开异常 : Exception
{
    public 打不开异常(string 说明) : base(说明) { }
}

/// <summary>磁盘上那个文件的外壳。字段名用英文，纯粹是机器读的，跟界面语言无关。</summary>
internal sealed class 文件外壳
{
    [JsonPropertyName("format")] public string 格式 { get; set; } = 库.格式标记;
    [JsonPropertyName("version")] public int 版本 { get; set; } = 库.当前版本;
    [JsonPropertyName("kdf")] public string 派生算法 { get; set; } = "PBKDF2-SHA256";
    [JsonPropertyName("iterations")] public int 迭代次数 { get; set; } = 加密.默认迭代次数;
    [JsonPropertyName("salt")] public string 盐 { get; set; } = "";
    [JsonPropertyName("cipher")] public string 加密算法 { get; set; } = "AES-256-GCM";
    [JsonPropertyName("nonce")] public string Nonce { get; set; } = "";
    [JsonPropertyName("data")] public string 密文 { get; set; } = "";
    [JsonPropertyName("tag")] public string 标签 { get; set; } = "";
}

/// <summary>
/// 一个密码库。
///
/// 和 v1（PowerShell 版）最大的区别：**整个条目表一起加密**，
/// 文件里明文可见的只有版本号、迭代次数和盐——这些不是秘密，
/// 但少了它们就没法派生密钥。不解锁看不到任何条目名。
///
/// 这个类不碰任何 Windows 专有 API，Linux / macOS 上照样跑，
/// 以后换 Avalonia 界面不用动它。
/// </summary>
public sealed class 库 : IDisposable
{
    public const string 格式标记 = "mimaku";
    public const int 当前版本 = 2;
    public const int 保留备份数 = 10;

    private static readonly JsonSerializerOptions 序列化选项 = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions 外壳选项 = new()
    {
        WriteIndented = true,
    };

    private byte[] _密钥;
    private int _迭代次数;
    private byte[] _盐;
    private bool _已释放;

    public string 路径 { get; }
    public 库内容 内容 { get; private set; }
    public List<条目> 条目表 => 内容.条目表;

    private 库(string 路径, byte[] 密钥, byte[] 盐, int 迭代次数, 库内容 内容)
    {
        this.路径 = 路径;
        _密钥 = 密钥;
        _盐 = 盐;
        _迭代次数 = 迭代次数;
        this.内容 = 内容;
    }

    // ---------- 建 / 开 ----------

    public static bool 存在(string 路径) => File.Exists(路径);

    public static 库 新建(string 路径, string 主密码)
    {
        if (File.Exists(路径)) throw new IOException($"文件已经存在：{路径}");
        var 盐 = 加密.随机字节(加密.盐字节数);
        var 密钥 = 加密.派生密钥(主密码, 盐, 加密.默认迭代次数);
        var 库对象 = new 库(路径, 密钥, 盐, 加密.默认迭代次数, new 库内容());
        库对象.保存();
        return 库对象;
    }

    public static 库 打开(string 路径, string 主密码)
    {
        文件外壳 壳;
        try
        {
            壳 = JsonSerializer.Deserialize<文件外壳>(File.ReadAllText(路径, Encoding.UTF8))
                 ?? throw new 打不开异常("文件是空的。");
        }
        catch (JsonException)
        {
            throw new 打不开异常("这个文件不是密码库，或者已经损坏。");
        }

        if (壳.格式 != 格式标记) throw new 打不开异常("这个文件不是本程序的密码库。");
        if (壳.版本 > 当前版本)
            throw new 打不开异常($"这个库是更高版本（v{壳.版本}）建的，当前程序只认到 v{当前版本}，请升级程序。");

        var 盐 = Convert.FromBase64String(壳.盐);
        var 密钥 = 加密.派生密钥(主密码, 盐, 壳.迭代次数);
        byte[] 明文;
        try
        {
            明文 = 加密.解密数据(
                密钥,
                Convert.FromBase64String(壳.Nonce),
                Convert.FromBase64String(壳.密文),
                Convert.FromBase64String(壳.标签),
                算附加数据(壳));
        }
        catch (CryptographicException)
        {
            加密.抹掉(密钥);
            throw new 打不开异常("主密码不对，或者文件被改动过。");
        }

        库内容 内容;
        try
        {
            内容 = JsonSerializer.Deserialize<库内容>(明文, 序列化选项) ?? new 库内容();
        }
        finally
        {
            加密.抹掉(明文);
        }

        return new 库(路径, 密钥, 盐, 壳.迭代次数, 内容);
    }

    /// <summary>
    /// 文件头参与认证但不加密。这样有人把 iterations 从 60 万改成 1，
    /// 解密会直接失败，而不是让暴力破解变得容易 100 万倍。
    /// </summary>
    private static byte[] 算附加数据(文件外壳 壳)
        => Encoding.UTF8.GetBytes($"{壳.格式}|{壳.版本}|{壳.派生算法}|{壳.迭代次数}|{壳.盐}|{壳.加密算法}");

    // ---------- 存 ----------

    public void 保存()
    {
        检查未释放();
        内容.条目表.Sort((甲, 乙) => string.Compare(甲.标题, 乙.标题, StringComparison.CurrentCulture));

        var 壳 = new 文件外壳
        {
            迭代次数 = _迭代次数,
            盐 = Convert.ToBase64String(_盐),
        };

        var 明文 = JsonSerializer.SerializeToUtf8Bytes(内容, 序列化选项);
        try
        {
            var (nonce, 密文, 标签) = 加密.加密数据(_密钥, 明文, 算附加数据(壳));
            壳.Nonce = Convert.ToBase64String(nonce);
            壳.密文 = Convert.ToBase64String(密文);
            壳.标签 = Convert.ToBase64String(标签);
        }
        finally
        {
            加密.抹掉(明文);
        }

        备份一份();

        // 先写临时文件再改名。中途断电只会留下一个 .tmp，不会把好库写成半截。
        var 临时 = 路径 + ".tmp";
        File.WriteAllText(临时, JsonSerializer.Serialize(壳, 外壳选项), new UTF8Encoding(false));
        收紧权限(临时);
        File.Move(临时, 路径, overwrite: true);
        收紧权限(路径);
    }

    /// <summary>
    /// 每次保存前把当前文件留一份到 备份\，保留最近若干份。
    /// 这东西以后装的是全部密码，一次误删就够受的。
    /// </summary>
    private void 备份一份()
    {
        if (!File.Exists(路径)) return;
        try
        {
            var 目录 = Path.Combine(Path.GetDirectoryName(路径) ?? ".", "备份");
            Directory.CreateDirectory(目录);
            var 名 = Path.GetFileNameWithoutExtension(路径);
            var 备份路径 = Path.Combine(目录, $"{名}-{DateTime.Now:yyyyMMdd-HHmmss}{Path.GetExtension(路径)}");
            if (!File.Exists(备份路径))
            {
                File.Copy(路径, 备份路径);
                收紧权限(备份路径);
            }

            var 旧的 = new DirectoryInfo(目录)
                .GetFiles($"{名}-*{Path.GetExtension(路径)}")
                .OrderByDescending(f => f.Name)
                .Skip(保留备份数)
                .ToList();
            foreach (var f in 旧的)
            {
                try { f.Delete(); } catch { /* 删不掉就算了，不能因为清理失败而存不了库 */ }
            }
        }
        catch
        {
            // 备份失败不该挡住保存本身
        }
    }

    /// <summary>把文件权限收成只有当前用户。Windows 走 icacls，其它平台走 chmod 600。</summary>
    public static void 收紧权限(string 文件)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var 用户 = Environment.UserName;
                var 进程 = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "icacls",
                    Arguments = $"\"{文件}\" /inheritance:r /grant:r \"{用户}:(F)\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
                进程?.WaitForExit(5000);
            }
            else
            {
                File.SetUnixFileMode(文件, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch
        {
            // 收不紧权限也不该让保存失败，顶多是安全性差一点
        }
    }

    // ---------- 条目 ----------

    public 条目? 按编号找(string 编号) => 条目表.FirstOrDefault(e => e.编号 == 编号);

    public void 放入(条目 条)
    {
        检查未释放();
        var 旧 = 按编号找(条.编号);
        条.更新 = DateTimeOffset.Now;
        if (旧 == null)
        {
            条目表.Add(条);
        }
        else
        {
            var 位置 = 条目表.IndexOf(旧);
            条目表[位置] = 条;
        }
    }

    public bool 删除(string 编号)
    {
        检查未释放();
        var 条 = 按编号找(编号);
        return 条 != null && 条目表.Remove(条);
    }

    public IEnumerable<string> 全部分类()
        => 条目表.Select(e => e.分类)
                 .Where(g => !string.IsNullOrWhiteSpace(g))
                 .Distinct(StringComparer.CurrentCultureIgnoreCase)
                 .OrderBy(g => g, StringComparer.CurrentCulture);

    // ---------- 主密码 ----------

    public void 换主密码(string 新主密码)
    {
        检查未释放();
        var 新盐 = 加密.随机字节(加密.盐字节数);
        var 新密钥 = 加密.派生密钥(新主密码, 新盐, 加密.默认迭代次数);
        var 旧密钥 = _密钥;
        _密钥 = 新密钥;
        _盐 = 新盐;
        _迭代次数 = 加密.默认迭代次数;
        加密.抹掉(旧密钥);
        保存();
    }

    /// <summary>验证一个密码是不是当前主密码。改主密码前要用。</summary>
    public bool 主密码对吗(string 待验)
    {
        检查未释放();
        var 试 = 加密.派生密钥(待验, _盐, _迭代次数);
        try
        {
            return CryptographicOperations.FixedTimeEquals(试, _密钥);
        }
        finally
        {
            加密.抹掉(试);
        }
    }

    private void 检查未释放()
    {
        ObjectDisposedException.ThrowIf(_已释放, this);
    }

    public void Dispose()
    {
        if (_已释放) return;
        加密.抹掉(_密钥);
        _已释放 = true;
    }
}
