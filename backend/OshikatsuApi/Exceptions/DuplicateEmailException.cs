namespace OshikatsuApi.Exceptions;

/// <summary>既に登録済みのメールアドレスで登録しようとした場合にスロー</summary>
public class DuplicateEmailException : Exception
{
    public DuplicateEmailException()
        : base("The email address is already registered.") { }
}
