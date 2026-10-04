namespace OshikatsuApi.Exceptions;

/// <summary>指定した talentId に対応するタレントが存在しない場合にスローする例外</summary>
public class TalentNotFoundException : Exception
{
    public TalentNotFoundException() : base("指定した talentId が見つかりません。") { }
}
