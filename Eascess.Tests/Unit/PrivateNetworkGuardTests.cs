using System.Net;
using Eascess_Application.Security;

namespace Eascess.Tests.Unit;

/// <summary>
/// SSRF koruması (son güvenlik denetimi): sunucunun giden isteklerinde özel/rezerve
/// IP'lere bağlanması engellenmelidir. Bulut metadata (169.254.169.254), loopback,
/// özel ağlar ve CGNAT bloklanır; genel adresler geçer.
/// </summary>
public class PrivateNetworkGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]        // loopback
    [InlineData("169.254.169.254")]  // bulut metadata (AWS/Azure/GCP)
    [InlineData("10.0.0.5")]         // özel
    [InlineData("172.16.0.1")]       // özel
    [InlineData("172.31.255.255")]   // özel (12'lik bloğun sonu)
    [InlineData("192.168.1.1")]      // özel
    [InlineData("100.64.0.1")]       // CGNAT
    [InlineData("0.0.0.0")]          // "bu ağ"
    [InlineData("224.0.0.1")]        // multicast
    [InlineData("::1")]              // IPv6 loopback
    [InlineData("fe80::1")]          // IPv6 link-local
    [InlineData("fc00::1")]          // IPv6 unique-local
    public void OzelVeRezerveAdresler_Bloklanir(string ip)
    {
        Assert.True(PrivateNetworkGuard.IsPrivateOrReserved(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("8.8.8.8")]          // Google DNS
    [InlineData("1.1.1.1")]          // Cloudflare
    [InlineData("93.184.216.34")]    // example.com
    [InlineData("172.15.0.1")]       // 12'lik özel bloğun HEMEN dışı → genel
    [InlineData("172.32.0.1")]       // 12'lik özel bloğun HEMEN dışı → genel
    [InlineData("2606:4700:4700::1111")] // Cloudflare IPv6
    public void GenelAdresler_Gecer(string ip)
    {
        Assert.False(PrivateNetworkGuard.IsPrivateOrReserved(IPAddress.Parse(ip)));
    }

    [Fact]
    public void IPv4MappedIPv6_LoopbackOlarakCozulur()
    {
        // ::ffff:127.0.0.1 — IPv6'ya eşlenmiş IPv4 loopback de bloklanmalı
        Assert.True(PrivateNetworkGuard.IsPrivateOrReserved(IPAddress.Parse("::ffff:127.0.0.1")));
    }
}
