# varsub

`varsub` is tool for variable substitutions in JSON files. It was created as a replacement for the [FileTransform](https://docs.microsoft.com/en-us/azure/devops/pipelines/tasks/utility/file-transform?view=azure-devops) task for DevOps, because it could not process multiple ZIP files at once.

## Installation

`varsub` is implemented as a .NET Global Tool, so its installation is quite simple:

```properties
dotnet tool install -g Kros.VariableSubstitution
```

To update it to the latest version, if it was installed previously, use:

```properties
dotnet tool update -g Kros.VariableSubstitution
```

## How to use it

```properties
varsub -w d:\Projects\artifacts -j **/appsettings.json
```
### Parameters

**--workingDirectory, -w**: Working directory.

**--zipFilesOrDirectories, -f**: Glob pattern to search a list of files or directories.

**--jsonTargetFiles, -j**: Glob pattern to search the target Json files.

**--tempDirectory, -t**: Path to temp directory.

**--variables, -v**: Variables. (var1=value1 var2=value2)

This tool processes all ZIP files that match the `--zipFilesOrDirectories` pattern in working directory. Processes all files that match the `--jsonTargetFiles` pattern. By default, it takes variables from environment variables. It tries to find the corresponding property in the JSON object for these variables. If  finds, will replace it.

The `--variables` parameter can be used mainly for testing purposes, with which it is possible to define variables and their values (`--variables var1=value1 var2=value2 ...`).

> ⚠ It is not possible to add a new property to a JSON object or a new record to a JSON field using this tool. It only allows the replacement of existing properties.
