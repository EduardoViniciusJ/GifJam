using System.Security.Cryptography;
using System.Text;

namespace GifJam.Api.Tests.Auth;

internal static class AuthTestHash
{
    public static string Create(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
