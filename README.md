# mawkgen
mawk 소스제너레이터

```bash
dotnet run - <<EOF
#:package Microsoft.Net.Compilers.Toolset@5.3.0
#:package mawkgen@0.0.2
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
#:package Microsoft.Net.Compilers.Toolset@5.3.0
#:package mawkgen@0.0.2
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