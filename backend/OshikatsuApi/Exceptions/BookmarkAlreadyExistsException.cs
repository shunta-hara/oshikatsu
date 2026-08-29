namespace OshikatsuApi.Exceptions;

/// <summary>対象のニュースアイテムがすでにブックマーク済みの場合にスローされる</summary>
public class BookmarkAlreadyExistsException : Exception
{
    public BookmarkAlreadyExistsException()
        : base("The news item is already bookmarked.")
    {
    }
}
