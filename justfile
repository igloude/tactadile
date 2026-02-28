# Tactadile development commands
# Run `just` to see all available recipes

set windows-shell := ["powershell.exe", "-NoLogo", "-Command"]

platform := "x64"
config   := "Debug"

# List available recipes
default:
    @just --list

# Build and run a worktree with interactive selector + hot-reload
worktree:
    & "{{justfile_directory()}}\dev.ps1" -Platform {{platform}} -Configuration {{config}}

# Build the main repo
build:
    dotnet build "{{justfile_directory()}}" -p:Platform={{platform}} -c {{config}}

# Build and run the main repo (no hot-reload)
run:
    dotnet build "{{justfile_directory()}}" -p:Platform={{platform}} -c {{config}}; \
    & "{{justfile_directory()}}\bin\{{platform}}\{{config}}\net8.0-windows10.0.19041.0\Tactadile.exe"

# Clean build output
clean:
    dotnet clean "{{justfile_directory()}}" -p:Platform={{platform}} -c {{config}}

# Restore NuGet packages
restore:
    dotnet restore "{{justfile_directory()}}"
