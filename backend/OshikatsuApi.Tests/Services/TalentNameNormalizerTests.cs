using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>TalentNameNormalizer の正規化ロジックをテストする</summary>
public class TalentNameNormalizerTests
{
    // ── 半角・全角スペースの除去 ─────────────────────────────────────────────────

    [Fact]
    public void Normalize_HalfWidthSpace_RemovesSpace()
    {
        Assert.Equal("津田健次郎", TalentNameNormalizer.Normalize("津田 健次郎"));
    }

    [Fact]
    public void Normalize_FullWidthSpace_RemovesSpace()
    {
        // U+3000: IDEOGRAPHIC SPACE（全角スペース）
        Assert.Equal("津田健次郎", TalentNameNormalizer.Normalize("津田　健次郎"));
    }

    [Fact]
    public void Normalize_NoSpace_ReturnsUnchanged()
    {
        Assert.Equal("津田健次郎", TalentNameNormalizer.Normalize("津田健次郎"));
    }

    /// <summary>半角スペース・全角スペース・スペースなしがすべて同一 normalized_name になる</summary>
    [Fact]
    public void Normalize_AllSpaceVariants_AreEqual()
    {
        var halfSpace = TalentNameNormalizer.Normalize("津田 健次郎");
        var fullSpace = TalentNameNormalizer.Normalize("津田　健次郎");
        var noSpace = TalentNameNormalizer.Normalize("津田健次郎");

        Assert.Equal(halfSpace, fullSpace);
        Assert.Equal(fullSpace, noSpace);
    }

    // ── 全角英数字 → 半角小文字への変換 ─────────────────────────────────────────

    [Fact]
    public void Normalize_FullWidthUpperAlpha_ConvertsToLowercase()
    {
        // ＬＯＶＥ (U+FF2C, U+FF2F, U+FF36, U+FF25) → love
        Assert.Equal("love", TalentNameNormalizer.Normalize("ＬＯＶＥ"));
    }

    [Fact]
    public void Normalize_FullWidthLowerAlpha_ConvertsToHalfWidth()
    {
        // ａｂｃ (U+FF41-) → abc
        Assert.Equal("abc", TalentNameNormalizer.Normalize("ａｂｃ"));
    }

    [Fact]
    public void Normalize_FullWidthDigit_ConvertsToHalfWidth()
    {
        // １２３ (U+FF11-) → 123
        Assert.Equal("123", TalentNameNormalizer.Normalize("１２３"));
    }

    [Fact]
    public void Normalize_MixedFullWidthAndHalfWidth_SameResult()
    {
        // ＬＯＶＥ と LOVE が同じ normalized_name になる
        var fullWidth = TalentNameNormalizer.Normalize("ＬＯＶＥ");
        var halfWidth = TalentNameNormalizer.Normalize("LOVE");
        Assert.Equal(fullWidth, halfWidth);
    }

    // ── ASCII 大文字 → 小文字への変換 ────────────────────────────────────────────

    [Fact]
    public void Normalize_UpperCaseAscii_ConvertsToLowercase()
    {
        Assert.Equal("love", TalentNameNormalizer.Normalize("LOVE"));
    }

    [Fact]
    public void Normalize_MixedCase_ConvertsToLowercase()
    {
        Assert.Equal("love", TalentNameNormalizer.Normalize("Love"));
    }

    // ── 全角記号の NFKC 変換 ─────────────────────────────────────────────────────

    [Fact]
    public void Normalize_FullWidthSymbol_NfkcConverted()
    {
        // ＨＥＬＬＯ！ → hello! (！ → !)
        Assert.Equal("hello!", TalentNameNormalizer.Normalize("ＨＥＬＬＯ！"));
    }

    // ── 空白文字（スペース以外）の除去 ───────────────────────────────────────────

    [Fact]
    public void Normalize_TabCharacter_RemovesWhitespace()
    {
        Assert.Equal("津田健次郎", TalentNameNormalizer.Normalize("津田\t健次郎"));
    }

    // ── 境界値 ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TalentNameNormalizer.Normalize(string.Empty));
    }

    [Fact]
    public void Normalize_SingleCharacter_ReturnsSingleCharacter()
    {
        Assert.Equal("a", TalentNameNormalizer.Normalize("A"));
    }

    [Fact]
    public void Normalize_OnlySpaces_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TalentNameNormalizer.Normalize("  　  "));
    }

    [Fact]
    public void Normalize_JapaneseName_RemovesAllSpaceVariants()
    {
        // 複数スペース（半角・全角混在）を除去
        var withSpaces = TalentNameNormalizer.Normalize("  津田  健次郎  ");
        var noSpaces = TalentNameNormalizer.Normalize("津田健次郎");
        Assert.Equal(withSpaces, noSpaces);
    }
}
