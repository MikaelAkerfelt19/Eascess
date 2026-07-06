using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Eascess_Application.Security;

/// <summary>
/// SSRF koruması. Sunucunun kullanıcı tarafından verilen adreslere yaptığı
/// giden isteklerde (WCAG tarayıcı, AI görsel indirme) özel/rezerve IP'lere
/// bağlanmayı engeller.
///
/// Asıl koruma <see cref="SafeConnectAsync"/>'tedir: bağlantı kurulmadan
/// HEMEN önce hedef IP çözümlenip doğrulanır. Bu; yönlendirmeleri (redirect)
/// ve DNS-rebinding'i de kapsar, çünkü her yeni TCP bağlantısı bu geçitten geçer.
/// Böylece "önce doğrula, sonra bağlan" (TOCTOU) açığı oluşmaz — bağlanılan IP
/// doğrulanan IP'nin ta kendisidir.
/// </summary>
public static class PrivateNetworkGuard
{
    /// <summary>IP özel, rezerve, loopback veya link-local mı?</summary>
    public static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 0                                    // 0.0.0.0/8
                || b[0] == 10                                   // 10.0.0.0/8
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)   // 100.64.0.0/10 (CGNAT)
                || (b[0] == 127)                                // 127.0.0.0/8
                || (b[0] == 169 && b[1] == 254)                 // 169.254.0.0/16 (link-local, bulut metadata)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)    // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 168)                 // 192.168.0.0/16
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)      // 192.0.0.0/24 (IETF protokol tahsisi)
                || b[0] >= 224;                                 // 224.0.0.0/4 multicast + 240.0.0.0/4 rezerve
        }

        // IPv6: link-local (fe80::/10), unique-local (fc00::/7), site-local, ::, ::1
        return ip.IsIPv6LinkLocal
            || ip.IsIPv6UniqueLocal
            || ip.IsIPv6SiteLocal
            || ip.Equals(IPAddress.IPv6Any)
            || ip.Equals(IPAddress.IPv6Loopback);
    }

    /// <summary>
    /// <see cref="SocketsHttpHandler.ConnectCallback"/> için güvenli bağlantı kurucu.
    /// Hedef host'u çözümler, yalnızca genel (public) yönlendirilebilir bir IP'ye
    /// bağlanır; özel/rezerve adres için bağlantıyı reddeder.
    /// </summary>
    public static async ValueTask<Stream> SafeConnectAsync(
        SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var endpoint = context.DnsEndPoint;

        IPAddress[] addresses = IPAddress.TryParse(endpoint.Host, out var literal)
            ? new[] { literal }
            : await Dns.GetHostAddressesAsync(endpoint.Host, ct);

        var target = Array.Find(addresses, a => !IsPrivateOrReserved(a))
            ?? throw new HttpRequestException(
                "Hedef adres özel/rezerve bir ağa çözümlendiği için engellendi.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(target, endpoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
