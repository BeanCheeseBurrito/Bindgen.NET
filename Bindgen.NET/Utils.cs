using System.Text;

namespace Bindgen.NET;

public static class Utils
{
    public static string Concat(int count, string sep, Func<int, string> func)
    {
        StringBuilder stringBuilder = new();

        for (int i = 0; i < count; i++)
        {
            if (i != count - 1)
            {
                stringBuilder.Append(func(i) + sep);
                continue;
            }

            stringBuilder.Append(func(i));
        }

        return stringBuilder.ToString();
    }

    public static string ConcatLines(int count, string sep, Func<int, string> func)
    {
        StringBuilder stringBuilder = new();

        for (int i = 0; i < count; i++)
        {
            if (i != count - 1)
            {
                stringBuilder.AppendLine(func(i) + sep);
                continue;
            }

            stringBuilder.AppendLine(func(i));
        }

        return stringBuilder.ToString();
    }
}
