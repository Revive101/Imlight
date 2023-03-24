using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Imlight.Net
{
    public static class NetUtil
    {
        public static async Task<IPAddress> GetExternalIpAddress()
        {
            var externalIpString = (await new HttpClient().GetStringAsync("http://icanhazip.com"))
                .Replace("\\r\\n", "").Replace("\\n", "").Trim();
            return !IPAddress.TryParse(externalIpString, out var ipAddress) ? null : ipAddress;
        }
    }
}