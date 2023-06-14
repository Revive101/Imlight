/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Imlight.Server.Shared.Networking
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