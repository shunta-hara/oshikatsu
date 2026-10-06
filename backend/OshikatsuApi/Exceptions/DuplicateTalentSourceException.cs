namespace OshikatsuApi.Exceptions;

/// <summary>同一 (talentId, sourceType, sourceId) のソースが既に登録済みの場合にスローされる例外。</summary>
public class DuplicateTalentSourceException : Exception
{
    public DuplicateTalentSourceException() : base("同一のソースが既に登録済みです。") { }
}
