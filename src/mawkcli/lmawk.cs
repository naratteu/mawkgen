#:property AllowUnsafeBlocks=true
#:include ../external_dependencies/libmawk-1.0.5/src/app.dll

using System.Text;
using static app.Globals;

unsafe { args.ToFixedApp(&Main); }
static unsafe void Main(int argc, byte** argv)
{
    var m = libmawk_initialize(argc, argv);
    libmawk_run_main(m);
    libmawk_uninitialize(m);
}

static class Exts
{
    public unsafe static void ToFixedApp(this string[] args, delegate*<int, byte**, void> done)
    {
        var len = args.Length;
        var fixedArgs = stackalloc byte*[1 + len + 1]; // [app .. null]
        ++fixedArgs;
        Collect();
        void Collect() // 재귀스택 부담을 줄이기 위해 인자나 지역변수는 없는게 좋을듯.
        {
            if (len-- > 0)
                fixed (byte* arg = Encoding.UTF8.GetBytes(args[len] + '\0'))
                {
                    fixedArgs[len] = arg;
                    Collect();
                }
            else
                fixed (byte* app = "app\0"u8)
                {
                    fixedArgs[len] = app;
                    done(1 + args.Length, --fixedArgs); // 사용할 모든 포인터가 fixed인 스택을 벗어나선 안됨.
                }
        }
    }
}