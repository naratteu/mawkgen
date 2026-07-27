```bash
dotnet ./lmawk.cs -- --help # libmawk 1.0.5

echo hello > input.txt
echo world >> input.txt

dotnet ./lmawk.cs -- "{ print $0  $0 }" input.txt
# hello hello
# world world
# 개행이 LF여야함.
```