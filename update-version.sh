#!/bin/bash
set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Parse arguments
VERSION=""
INCREMENT="patch"

while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--version)
            VERSION="$2"
            shift 2
            ;;
        -i|--increment)
            INCREMENT="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Updates the version number in all Jezda.Common NuGet packages."
            echo ""
            echo "Options:"
            echo "  -v, --version VERSION    Set specific version (e.g., '1.0.35')"
            echo "  -i, --increment TYPE     Increment type: major, minor, patch (default: patch)"
            echo "  -h, --help              Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0                      # Auto-increment patch version"
            echo "  $0 -v 2.0.0            # Set specific version"
            echo "  $0 -i minor            # Increment minor version"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

# Find all Jezda.Common.*.csproj files (excluding Tests and obj folders)
mapfile -t PROJECT_FILES < <(find . -name "Jezda.Common.*.csproj" -type f | grep -v "/obj/" | grep -v "Tests")

if [ ${#PROJECT_FILES[@]} -eq 0 ]; then
    echo -e "${RED}No Jezda.Common.*.csproj files found!${NC}"
    exit 1
fi

echo -e "${CYAN}Found ${#PROJECT_FILES[@]} project files:${NC}"
for proj in "${PROJECT_FILES[@]}"; do
    echo -e "  - $(basename "$proj")"
done
echo ""

# Determine new version
if [ -z "$VERSION" ]; then
    # Extract current versions
    CURRENT_VERSIONS=()
    for proj in "${PROJECT_FILES[@]}"; do
        if grep -q "<Version>" "$proj"; then
            ver=$(grep -oP '<Version>\K[\d.]+' "$proj")
            CURRENT_VERSIONS+=("$ver")
        fi
    done

    if [ ${#CURRENT_VERSIONS[@]} -eq 0 ]; then
        echo -e "${RED}No versions found in project files!${NC}"
        exit 1
    fi

    # Find highest version
    HIGHEST_VERSION=$(printf '%s\n' "${CURRENT_VERSIONS[@]}" | sort -V | tail -1)
    echo -e "${YELLOW}Current highest version: $HIGHEST_VERSION${NC}"

    # Parse version components
    IFS='.' read -r MAJOR MINOR PATCH <<< "$HIGHEST_VERSION"

    # Increment version
    case $INCREMENT in
        major)
            VERSION="$((MAJOR + 1)).0.0"
            ;;
        minor)
            VERSION="$MAJOR.$((MINOR + 1)).0"
            ;;
        patch)
            VERSION="$MAJOR.$MINOR.$((PATCH + 1))"
            ;;
        *)
            echo -e "${RED}Invalid increment type: $INCREMENT${NC}"
            exit 1
            ;;
    esac
fi

echo -e "${GREEN}New version: $VERSION${NC}"
echo ""

# Confirm with user
read -p "Update all packages to version $VERSION? (y/N): " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}Cancelled.${NC}"
    exit 0
fi

# Update all project files
UPDATED_COUNT=0
for proj in "${PROJECT_FILES[@]}"; do
    if grep -q "<Version>" "$proj"; then
        sed -i "s|<Version>[0-9.]*</Version>|<Version>$VERSION</Version>|g" "$proj"
        echo -e "${GREEN}[UPDATED]${NC} $(basename "$proj")"
        ((UPDATED_COUNT++))
    else
        echo -e "${YELLOW}[SKIPPED]${NC} $(basename "$proj") - No <Version> tag found"
    fi
done

echo ""
echo -e "${CYAN}Updated $UPDATED_COUNT project(s) to version $VERSION${NC}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo -e "${GRAY}  1. Build all projects: dotnet build -c Release${NC}"
echo -e "${GRAY}  2. Review changes: git diff${NC}"
echo -e "${GRAY}  3. Commit changes: git add . && git commit -m 'Bump version to $VERSION'${NC}"
echo -e "${GRAY}  4. Create tag: git tag v$VERSION${NC}"
echo -e "${GRAY}  5. Push: git push && git push --tags${NC}"
