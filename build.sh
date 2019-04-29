#!/bin/bash

set -e 
dotnet --info
dotnet --list-sdks
dotnet restore

echo "🤖 Attempting to build..."
for path in src/**/*.csproj; do
    dotnet build -f netstandard2.0 -c Release ${path}
done

# Commented out as RavenDB.Embedded is requesting Microsoft.NetCore.App 2.1.3 runtime that's not installed on the Appveyor CI server...
# 
#echo "🤖 Running tests..."
#for path in test/*.Tests/*.csproj; do
#    dotnet test -f netcoreapp2.0  -c Release ${path}
#done
