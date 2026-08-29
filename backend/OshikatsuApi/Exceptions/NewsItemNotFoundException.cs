namespace OshikatsuApi.Exceptions;

/// <summary>指定した ID のニュースアイテムが存在しない場合にスローされる</summary>
public class NewsItemNotFoundException : Exception
{
    public NewsItemNotFoundException()
        : base("The specified news item was not found.")
    {
    }
}
