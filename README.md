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

**--fast**: Substitute directly in the ZIP package. The tool does not extract the package.

This tool processes all ZIP files that match the `--zipFilesOrDirectories` pattern in working directory. Multiple glob patterns are allowed (`-f *pattern.zip -f **/anotherPattern.zip ...`). Processes all files that match the `--jsonTargetFiles` pattern. By default, it takes variables from environment variables. It tries to find the corresponding property in the JSON object for these variables. If a match is found, it will replace it.

The `--variables` parameter can be used mainly for testing purposes, with which it is possible to define variables and their values (`--variables var1=value1 --variables var2=value2 ...`).

> ⚠ It is not possible to add a new property to a JSON object or a new record to a JSON field using this tool. It only allows the replacement of existing properties.

### --fast

By default, the tool extracts the ZIP package to the temp directory. It substitutes the JSON files. Then it builds a new archive from the extracted files. To change one JSON file, the tool decompresses and compresses every file in the package.

With `--fast`, the tool rewrites only the JSON entries in the existing archive. All other entries keep their original compressed bytes. The tool extracts no files. If no variable matches, the tool does not change the package.

The two methods give the same result: the same entries with the same content. The tests verify this.

The table shows the median time of the substitution call on real deployment packages:

| package | entries | default | `--fast` |
|---|---|---|---|
| 7.7 MB | 90 | 399 ms | 7 ms |
| 35.9 MB | 289 | 1 721 ms | 28 ms |
| 79.7 MB | 457 | 3 708 ms | 55 ms |

`--fast` uses more memory. The default writes to disk. It uses approximately 30 MB for all package sizes. `--fast` holds the archive in memory to rewrite it. Peak memory is approximately the size of the package. For the 79.7 MB package in the table, peak memory was 112 MB.

Memory does not accumulate. The tool releases each archive before it opens the next one. One command that processed 32 packages, 1 211 MB in total, used a maximum of 256 MB.

Use the default if memory is limited, or if the packages are very large.
