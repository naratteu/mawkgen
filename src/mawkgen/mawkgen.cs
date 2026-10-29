namespace mawkgen;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using static app.Globals;

[Generator]
public class MawkGen : IIncrementalGenerator
{
    /// <summary>껍데기 안에 mawk 출력을 끼워넣을 자리. 이 자리를 Split 으로 찾아 가른다.</summary>
    const string Hole = "inner // <inner />";

    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            //ctx.AddEmbeddedAttributeDefinition();//todo: sdk net10 분석기 4.14.0~ 부터 사용가능
            ctx.AddSource("mawkgen.g.cs", /*lang=C#*/"""
                namespace mawkgen;
                //[Microsoft.CodeAnalysis.Embedded]//todo: sdk net10 분석기 4.14.0~ 부터 사용가능
                class MawkAttribute(string awk) : System.Attribute { public string Awk { get; } = awk; }
                """);
        });
        var attr = context.SyntaxProvider.ForAttributeWithMetadataName("mawkgen.MawkAttribute", (_, _) => true, (c, _) => c);
        context.RegisterSourceOutput(attr, (spc, source) =>
        {
            foreach (var (v, i) in source.Attributes.Select((v, i) => (v, i)))
            {
                if (v.ConstructorArguments is [{ Value: string awk }])
                {
                    // SyntaxList<T>/SyntaxTokenList 에는 컬렉션 표현식을 쓰지 않는다.
                    // Roslyn 5 부터 [CollectionBuilder] 가 붙어서 [x] 가 동작하지만,
                    // 4.x 에는 없어서 비변경 Add 의 반환값이 버려지며 '조용히 빈 목록'이 된다.
                    // 그러면 partial 수정자와 멤버가 통째로 사라져 아래 Split 이 깨진다.
                    var ps = Exts.ParentStack(
                        source.TargetNode.Parent,
                        (source.TargetNode as TypeDeclarationSyntax)?
                            .WithAttributeLists(default)
                            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                            .WithMembers(SyntaxFactory.SingletonList(SyntaxFactory.ParseMemberDeclaration(Hole)!))
                        !).NormalizeWhitespace().ToFullString().Split([Hole], default) is [var a, var b] ? (a, b)
                        : throw new InvalidOperationException($"'{source.TargetSymbol}' 를 감쌀 껍데기를 만들지 못했습니다. 자리표시자 \"{Hole}\" 가 정규화 결과에 없습니다.");
                    byte[] buffer = [
                        ..a.ToUtf8LfNullTerminated(),
                        .."\n"u8,
                        ..Exts.Run(awk, source.SemanticModel.SyntaxTree.ToString()),
                        ..b.ToUtf8LfNullTerminated(),
                    ];
                    // canBeEmbedded 를 켜지 않으면 EmbedUntrackedSources(SDK 기본값) 가 생성 소스를
                    // PDB 에 묻으려 할 때 Roslyn 4.x 에서 컴파일러가 죽는다.
                    //   System.ArgumentException: SourceText cannot be embedded.
                    spc.AddSource($"{source.TargetSymbol}{i}.g.cs", SourceText.From(buffer, buffer.Length, Encoding.UTF8, canBeEmbedded: true));
                }
            }
        });
    }
}

file static class Exts
{
    public static MemberDeclarationSyntax ParentStack(SyntaxNode? p, MemberDeclarationSyntax m) => p switch
    {
        BaseNamespaceDeclarationSyntax n
            => ParentStack(n.Parent, n.WithMembers(SyntaxFactory.SingletonList(m))),
        TypeDeclarationSyntax t
            => ParentStack(t.Parent, t.WithMembers(SyntaxFactory.SingletonList(m))),
        _ => m
    };

    public unsafe static IEnumerable<byte> Run(string script, string input)
    {
        fixed (byte*
            app = "lmawk\0"u8,
            sc = (byte[])[.. script.ToUtf8LfNullTerminated(), 0],
            i = (byte[])[.. input.ToUtf8LfNullTerminated(), 0])
        {
            List<byte> output = [];
            var m = libmawk_initialize_stage1();
            try
            {
                libmawk_initialize_stdio(m, 0, 0, 1);
                const int pLen = 2;
                var p = stackalloc byte*[pLen] { app, sc };
                libmawk_initialize_stage2(m, pLen, p);
                libmawk_initialize_stage3(m);
                libmawk_append_input(m, i);
                libmawk_close_input(m);
                libmawk_run_main(m);

                var fnode_stdout = m->StructPointer118;
                var vf = fnode_stdout->StructPointer3;
                for (byte buf; mawk_vio_fifo_read_app(m, vf, &buf, 1) > 0;)
                    output.Add(buf);
            }
            finally
            {
                libmawk_uninitialize(m);
            }
            return output;
        }
    }

    public static IEnumerable<byte> ToUtf8LfNullTerminated(this string text)
    {
        var utf8 = Encoding.UTF8.GetEncoder();
        var cc = new char[1];
        var bb = new byte[4]; // UTF-8 최대 4바이트
        foreach (char c in text)
        {
            if (c is '\r') continue; // CRLF → LF
            cc[0] = c;
            for (int i = 0, cnt = utf8.GetBytes(cc, 0, 1, bb, 0, false); i < cnt; i++)
                yield return bb[i];
        }
        //todo: GetByteCount+1로 적당한 버퍼 생성후 씌우는게 더 효율적일듯
    }
}