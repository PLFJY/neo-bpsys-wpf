using System.Net;
using System.Net.Sockets;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 对模型数据驱动的远程图片/下载地址做基础 SSRF 防护。
/// </summary>
internal static class RemoteUriSafety
{
    /// <summary>
    /// 校验 URI 为无用户信息的绝对 HTTP/HTTPS 地址，且不直接指向本机/内网字面量。
    /// </summary>
    /// <param name="uri">待校验 URI。</param>
    /// <returns>通过基础校验时返回 <see langword="true"/>。</returns>
    internal static bool IsSafeRemoteHttpUri(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri
            || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.UserInfo)
            || string.IsNullOrWhiteSpace(uri.Host)
            || string.Equals(uri.Host.TrimEnd('.'), "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IPAddress.TryParse(uri.DnsSafeHost, out var address))
        {
            return true;
        }

        return IsPublicAddress(address);
    }

    /// <summary>
    /// 解析远程主机并拒绝任一解析结果落在本机/内网地址的情况。
    /// </summary>
    /// <param name="uri">远程 URI。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>所有解析地址均为公网地址时返回 <see langword="true"/>。</returns>
    internal static async Task<bool> IsPublicHostAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!IsSafeRemoteHttpUri(uri))
        {
            return false;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken)
                .ConfigureAwait(false);
            return addresses.Length > 0 && addresses.All(IsPublicAddress);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 判断 IP 是否属于公网地址范围。
    /// </summary>
    /// <param name="address">IP 地址。</param>
    /// <returns>不是本机、回环、链路本地、私网、组播或保留地址时返回 <see langword="true"/>。</returns>
    internal static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.IsIPv6LinkLocal
            || address.IsIPv6Multicast
            || address.IsIPv6SiteLocal)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            // 拒绝唯一本地地址 fc00::/7 与 IPv4 映射之外的保留范围。
            return (bytes[0] & 0xfe) != 0xfc;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var value = address.GetAddressBytes();
        // Clash TUN 常把公网域名映射到 RFC 2544 基准测试网段；该地址最终仍由系统路由/代理处理。
        if (value[0] == 198 && value[1] is 18 or 19)
        {
            return true;
        }

        return value[0] != 0
               && value[0] != 10
               && value[0] != 127
               && !(value[0] == 100 && value[1] is >= 64 and <= 127)
               && !(value[0] == 169 && value[1] == 254)
               && !(value[0] == 172 && value[1] is >= 16 and <= 31)
               && !(value[0] == 192 && value[1] == 168)
               && !(value[0] == 192 && value[1] == 0)
               && !(value[0] == 198 && value[1] == 51 && value[2] == 100)
               && !(value[0] == 203 && value[1] == 0 && value[2] == 113)
               && value[0] < 224;
    }
}
