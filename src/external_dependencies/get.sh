ls Iril || git clone https://github.com/naratteu/Iril --branch for_libmawk
ls libmawk-1.0.5 || curl http://repo.hu/projects/releases/libmawk-1.0.5.tar.gz | tar xz
cd libmawk-1.0.5
./configure
make install
cd src
dotnet run --project ../../Iril/Cli/Cli.csproj -- -Ilibmawk example_apps/30_out_pipes/app.c \
    libmawk/memory.c libmawk/hash.c libmawk/code.c libmawk/vars.c libmawk/da_bin.c libmawk/da_common.c libmawk/da_bin_helper.c libmawk/error.c libmawk/bi_vars.c libmawk/bi_funct_common.c libmawk/array.c libmawk/array_orig.c libmawk/array_generic.c libmawk/field_common.c libmawk/re_cmpl.c libmawk/zmalloc.c libmawk/fin_common.c libmawk/files.c libmawk/matherr.c libmawk/fcall.c libmawk/version.c libmawk/missing.c libmawk/math_wrap.c libmawk/cast.c libmawk/cell.c libmawk/scancode.c libmawk/str.c libmawk/array_environ.c libmawk/files_children.c libmawk/vio_orig.c libmawk/num_double.c libmawk/parse.c libmawk/scan.c libmawk/da_text.c libmawk/code_dump.c libmawk/kw.c libmawk/jmp.c libmawk/execute.c libmawk/bi_funct.c libmawk/print.c libmawk/debug.c libmawk/field_exec.c libmawk/split.c libmawk/rexp/rexp.c libmawk/rexp/rexp0.c libmawk/rexp/rexp1.c libmawk/rexp/rexp2.c libmawk/rexp/rexp3.c libmawk/zfifo.c libmawk/vio_fifo.c libmawk/init.c libmawk/libmawk.c libmawk/fin_exec.c
