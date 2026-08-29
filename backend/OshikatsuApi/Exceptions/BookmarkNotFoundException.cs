namespace OshikatsuApi.Exceptions;

/// <summary>指定したブックマークが存在しない場合にスローされる</summary>
public class BookmarkNotFoundException : Exception
{
    public BookmarkNotFoundException()
        : base("The specified bookmark was not found.")
    {
    }
}
