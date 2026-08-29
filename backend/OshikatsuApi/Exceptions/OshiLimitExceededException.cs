namespace OshikatsuApi.Exceptions;

/// <summary>1ユーザーあたりの推し登録上限（20件）を超えた場合にスローされる</summary>
public class OshiLimitExceededException : Exception
{
    public OshiLimitExceededException()
        : base("Maximum number of oshis (20) has been reached.")
    {
    }
}
