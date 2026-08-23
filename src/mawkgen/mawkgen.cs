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
    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            //ctx.AddEmbeddedAttributeDefinition();//todo: sdk net10 분석기 4.14.0~ 부터 사용가능
            ctx.AddSource("mawkgen.g.cs", /*lang=C#*/"""
                namespace mawkgen;
                //[Microsoft.CodeAnalysis.Embedded]//todo: sdk net10 분석기 4.14.0~ 부터 사용가능
                class MawkAttribute(string awk) : System.Attribute;
                """);
        });
        var attr = context.SyntaxProvider.ForAttributeWithMetadataName("mawkgen.MawkAttribute", (_, _) => true, (c, _) => c);
        context.RegisterSourceOutput(attr, (spc, source) =>
        {
            foreach (var (v, i) in source.Attributes.Select((v, i) => (v, i)))
            {
                if (v.ConstructorArguments is [{ Value: string awk }])
                {
                    var ps = Exts.ParentStack(
                        source.TargetNode.Parent,
                        (source.TargetNode as TypeDeclarationSyntax)?
                            .WithAttributeLists([])
                            .WithModifiers([SyntaxFactory.Token(SyntaxKind.PartialKeyword)])
                            .WithMembers([SyntaxFactory.ParseMemberDeclaration("inner // <inner />")!])
                        !).NormalizeWhitespace().ToFullString().Split(["inner // <inner />"], default) is [var a, var b] ? (a, b) : throw new();
                    byte[] buffer = [
                        ..a.ToUtf8LfNullTerminated(),
                        .."\n"u8,
                        ..Exts.Run(awk, source.SemanticModel.SyntaxTree.ToString()),
                        ..b.ToUtf8LfNullTerminated(),
                    ];
                    spc.AddSource($"{source.TargetSymbol}{i}.g.cs", SourceText.From(buffer, buffer.Length, Encoding.UTF8));
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
            => ParentStack(n.Parent, n.WithMembers([m])),
        TypeDeclarationSyntax t
            => ParentStack(t.Parent, t.WithMembers([m])),
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