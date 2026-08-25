#:sdk Cake.Sdk@6.2.0

//todo: cake.cs의 전과정이 AI생성으로 복잡성이 집중되있는 편임. 정리필요

using System.Net.Http;

var configuration = Argument("configuration", "Release");
var packageVersion = Argument("tag", "");
var project = "./src/mawkgen/mawkgen.csproj";
var outputDirectory = "./artifacts";
var nugetSource = "https://api.nuget.org/v3/index.json";
var externalDirectory = "./src/external_dependencies";
var irilDirectory = $"{externalDirectory}/Iril";
var libmawkDirectory = $"{externalDirectory}/libmawk-1.0.5";
var appDll = $"{libmawkDirectory}/src/app.dll";
var libmawkArchive = $"{externalDirectory}/libmawk-1.0.5.tar.gz";

void Run(string fileName, string workingDirectory, Action<ProcessArgumentBuilder> arguments)
{
    var processArguments = new ProcessArgumentBuilder();
    arguments(processArguments);
    var exitCode = StartProcess(fileName, new ProcessSettings
    {
        WorkingDirectory = workingDirectory,
        Arguments = processArguments,
    });

    if (exitCode != 0)
    {
        throw new Exception($"{fileName} failed with exit code {exitCode}.");
    }
}

void Download(string url, string path)
{
    Information($"Downloading {url}");
    using var client = new HttpClient();
    using var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
    response.EnsureSuccessStatusCode();
    using var input = response.Content.ReadAsStream();
    using var output = System.IO.File.Create(path);
    input.CopyTo(output);
}

void RunUnixCommand(string command, string workingDirectory, Action<ProcessArgumentBuilder> arguments)
{
    if (IsRunningOnUnix())
    {
        Run(command, workingDirectory, arguments);
        return;
    }

    var commandLine = new ProcessArgumentBuilder();
    commandLine.Append(command);
    arguments(commandLine);
    Run("bash", workingDirectory, args => args.Append("-c").AppendQuoted(commandLine.Render()));
}

Task("DownloadDependencies")
    .Does(() =>
{
    EnsureDirectoryExists(externalDirectory);

    if (!System.IO.Directory.Exists(irilDirectory))
    {
        Run("git", "./", args => args
            .Append("clone")
            .Append("--depth").Append("1")
            .Append("--branch").Append("for_libmawk")
            .Append("https://github.com/naratteu/Iril")
            .Append(irilDirectory));
    }

    if (!System.IO.Directory.Exists(libmawkDirectory))
    {
        if (!System.IO.File.Exists(libmawkArchive))
        {
            //Download("https://repo.hu/projects/releases/libmawk-1.0.5.tar.gz", libmawkArchive);
            Download("https://github.com/naratteu/mawkgen/releases/download/v0.0.3/libmawk-1.0.5.tar.gz", libmawkArchive);
        }

        EnsureDirectoryExists(libmawkDirectory);
        Run("tar", "./", args => args
            .Append("-xzf").AppendQuoted(libmawkArchive)
            .Append("--strip-components=1")
            .Append("-C").AppendQuoted(libmawkDirectory));
    }
});

Task("BuildLibMawk")
    .IsDependentOn("DownloadDependencies")
    .Does(() =>
{
    if (System.IO.File.Exists(appDll))
    {
        Information($"app.dll already exists at {appDll}");
        return;
    }

    RunUnixCommand("sh", libmawkDirectory, args => args.Append("./configure"));
    RunUnixCommand("make", libmawkDirectory, _ => { });
});

Task("GenerateAppDll")
    .IsDependentOn("BuildLibMawk")
    .Does(() =>
{
    if (System.IO.File.Exists(appDll))
    {
        Information($"app.dll already exists at {appDll}");
        return;
    }

    var sourceFiles = new[]
    {
        "memory.c", "hash.c", "code.c", "vars.c", "da_bin.c", "da_common.c", "da_bin_helper.c",
        "error.c", "bi_vars.c", "bi_funct_common.c", "array.c", "array_orig.c", "array_generic.c",
        "field_common.c", "re_cmpl.c", "zmalloc.c", "fin_common.c", "files.c", "matherr.c", "fcall.c",
        "version.c", "missing.c", "math_wrap.c", "cast.c", "cell.c", "scancode.c", "str.c",
        "array_environ.c", "files_children.c", "vio_orig.c", "num_double.c", "parse.c", "scan.c",
        "da_text.c", "code_dump.c", "kw.c", "jmp.c", "execute.c", "bi_funct.c", "print.c", "debug.c",
        "field_exec.c", "split.c", "rexp/rexp.c", "rexp/rexp0.c", "rexp/rexp1.c", "rexp/rexp2.c",
        "rexp/rexp3.c", "zfifo.c", "vio_fifo.c", "init.c", "libmawk.c", "fin_exec.c",
    };

    Run("dotnet", $"{libmawkDirectory}/src", args =>
    {
        args.Append("run")
            .Append("--project").AppendQuoted("../../Iril/Cli/Cli.csproj")
            .Append("--")
            .Append("-Ilibmawk")
            .Append("example_apps/30_out_pipes/app.c");

        foreach (var sourceFile in sourceFiles)
        {
            args.Append($"libmawk/{sourceFile}");
        }
    });

    if (!System.IO.File.Exists(appDll))
    {
        throw new Exception($"app.dll was not generated at {appDll}.");
    }

    Information($"Generated app.dll at {appDll}");
});

Task("Build")
    .IsDependentOn("GenerateAppDll")
    .Does(() =>
{
    DotNetBuild(project, new DotNetBuildSettings
    {
        Configuration = configuration,
        NoRestore = false,
    });
});

Task("Package")
    .IsDependentOn("Build")
    .Does(() =>
{
    EnsureDirectoryExists(outputDirectory);

    Run("dotnet", "./", args =>
    {
        args.Append("pack")
            .Append(project)
            .Append("--configuration").Append(configuration)
            .Append("--output").AppendQuoted(outputDirectory)
            .Append("--no-restore");

        if (!string.IsNullOrWhiteSpace(packageVersion))
        {
            args.Append($"-p:Version={packageVersion}");
        }
    });
});

Task("Push")
    .IsDependentOn("Package")
    .Does(() =>
{
    var apiKey = EnvironmentVariable("NUGET_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        throw new Exception("NUGET_API_KEY must be set when publishing a package.");
    }

    foreach (var package in GetFiles($"{outputDirectory}/*.nupkg"))
    {
        Run("dotnet", "./", args => args
            .Append("nuget")
            .Append("push")
            .AppendQuoted(package.FullPath)
            .Append("--api-key").AppendQuoted(apiKey)
            .Append("--source").AppendQuoted(nugetSource)
            .Append("--skip-duplicate"));
    }
});

RunTarget(Argument("target", "Push"));
