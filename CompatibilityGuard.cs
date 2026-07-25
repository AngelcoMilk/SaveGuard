using System;
using System.IO;
using System.Security.Cryptography;
using YAPYAP;

namespace SaveGuard;

internal static class CompatibilityGuard
{
    internal static bool Validate(string expectedHash, bool enforce, out string reason)
    {
        try
        {
            string assemblyPath = typeof(GameManager).Assembly.Location;
            using FileStream stream = File.OpenRead(assemblyPath);
            using SHA256 sha = SHA256.Create();
            string actualHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Assembly hash verified.";
                return true;
            }

            reason = $"Expected {expectedHash}, found {actualHash}.";
            if (!enforce)
            {
                Plugin.Log?.LogWarning("YAPYAP build differs from the verified build; continuing because EnforceBuildGuard is disabled. " + reason);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            reason = "Unable to hash Assembly-CSharp.dll: " + ex.Message;
            return !enforce;
        }
    }
}
