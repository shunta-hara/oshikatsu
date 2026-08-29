namespace OshikatsuApi.Exceptions;

/// <summary>他のユーザーの推しに対してアクセスを試みた場合にスローされる</summary>
public class OshiAccessDeniedException : Exception
{
    public OshiAccessDeniedException()
        : base("You do not have permission to access this oshi.")
    {
    }
}
