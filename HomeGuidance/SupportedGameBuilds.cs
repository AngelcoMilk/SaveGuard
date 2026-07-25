namespace HomeGuidance;

public static class SupportedGameBuilds
{
    public static readonly string[] AllowedHashes = new[]
    {
        "7b6ef048e716ce4cf87bf5c6f190b3c11d39c50aa18a81467770f13ceed3c542"
    };

    public static bool IsAllowed(string sha256)
    {
        if (string.IsNullOrEmpty(sha256)) return false;
        foreach (var h in AllowedHashes)
        {
            if (string.Equals(h, sha256, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
