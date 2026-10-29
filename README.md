# mawkgen

[![nuget](https://img.shields.io/nuget/v/mawkgen)](https://www.nuget.org/packages/mawkgen)

mawk 소스제너레이터. 붙여넣으면 바로 돕니다.
(Roslyn 4.8 이상이 필요합니다 — .NET 10 · 9 SDK 에서 확인했습니다)

```bash
dotnet run - <<EOF
#:package mawkgen@0.0.4
using mawkgen;

Temp.Hello();

[Mawk("""
BEGIN {
    print "public static void Hello() => Console.WriteLine(\"Hello, Mawk!\");"
}
""")]
static partial class Temp{}
EOF

dotnet run - <<EOF
#:package mawkgen@0.0.4
using mawkgen;

Console.WriteLine(string.Join(", ", Temp.Ints()));
Temp.b = 10;
Console.WriteLine(string.Join(", ", Temp.Ints()));

[Mawk("""
/#region Ints$/ { Ints=1; print "public static partial int[] Ints() => [" ; next }
/#endregion Ints$/ { Ints=0; print "];"; next }
Ints { print "  " \$4 "," }
""")]
static partial class Temp
{
    #region Ints
    public static int a = 1;
    public static int b = 2;
    public static int c = 3;
    #endregion Ints

    public static partial int[] Ints();
}
EOF
```

출력은 각각 이렇습니다.

```
Hello, Mawk!
```
```
1, 2, 3
1, 10, 3
```
