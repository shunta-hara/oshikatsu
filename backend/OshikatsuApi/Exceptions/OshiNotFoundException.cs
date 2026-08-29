namespace OshikatsuApi.Exceptions;

/// <summary>指定した ID の推しが存在しない場合にスローされる</summary>
public class OshiNotFoundException : Exception
{
    public OshiNotFoundException()
        : base("The specified oshi was not found.")
    {
    }
}
