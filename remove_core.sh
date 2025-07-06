#!/usr/bin/env bash
# Removes the shared Core project and migrates its code into InputToControllerMapper.
# Run from the solution root.
set -e
solution="InputMappingSolution.sln"
core_project="Core/Core.csproj"
target_dir="InputToControllerMapper"

# Move code files if the Core directory exists
if [ -d "Core" ]; then
    mv Core/*.cs "$target_dir/" || true
    rm -rf Core
fi

# Remove project reference from the solution
if command -v dotnet >/dev/null 2>&1; then
    dotnet sln "$solution" remove "$core_project" 2>/dev/null || true
fi

# Purge project references from csproj files
find . -name "*.csproj" -exec sed -i '/Core\\\\Core.csproj/d' {} +

# Clean up using statements referencing Core
grep -rl "using Core;" "$target_dir" | xargs sed -i 's/^using Core;\s*//'

echo "Core project removed."
