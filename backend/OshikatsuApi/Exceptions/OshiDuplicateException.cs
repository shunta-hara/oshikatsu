namespace OshikatsuApi.Exceptions;

/// <summary>
/// 同一ユーザーが既に同じタレントを推しとして登録済みの場合にスローする例外。
/// </summary>
public class OshiDuplicateException : Exception
{
    public OshiDuplicateException() : base("This talent is already registered as your oshi.") { }
}
