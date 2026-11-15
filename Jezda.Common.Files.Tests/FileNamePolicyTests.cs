using Jezda.Common.Files.Naming;
using Xunit;

namespace Jezda.Common.Files.Tests;

public class FileNamePolicyTests
{
    [Fact]
    public void Normalizes_And_Replaces_Invalid_Chars()
    {
        var policy = new FileNamePolicy
        {
            MaxFileNameLength = 50,
            NormalizeUnicode = true,
            ReplaceInvalidChars = true,
            ReplacementChar = '_'
        };

        var (isValid, normalized) = policy.ValidateAndNormalize("  čudno*ime?.txt  ");
        Assert.True(isValid);
        // Unicode slova su dozvoljena; nedozvoljeni znakovi zamenjeni
        Assert.Equal("čudno_ime_.txt", normalized);
    }

    [Fact]
    public void Prefixes_Reserved_Names()
    {
        var policy = new FileNamePolicy();
        var (isValid, normalized) = policy.ValidateAndNormalize("CON.txt");
        Assert.True(isValid);
        Assert.Equal("_CON.txt", normalized);
    }

    [Fact]
    public void Honors_AllowedPattern_Changed_After_First_Use()
    {
        var policy = new FileNamePolicy { ReplaceInvalidChars = false };
        Assert.True(policy.ValidateAndNormalize("abc.txt").isValid);

        policy.AllowedPattern = "^[a-z]+$";

        Assert.False(policy.ValidateAndNormalize("abc.txt").isValid);
        Assert.True(policy.ValidateAndNormalize("abc").isValid);
    }
}
