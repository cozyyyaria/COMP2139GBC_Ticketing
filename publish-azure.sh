#!/bin/bash

echo "=== Azure Publish Script ==="

# Clean previous publish
echo "Cleaning previous publish..."
rm -rf ./publish
rm -rf ./bin/Release/net9.0/publish

# Restore packages
echo "Restoring packages..."
dotnet restore WebApplication1.csproj

# Build in Release mode
echo "Building in Release mode..."
dotnet build WebApplication1.csproj -c Release

# Publish with static files
echo "Publishing..."
dotnet publish WebApplication1.csproj -c Release -o ./publish --no-build

# Verify wwwroot was copied
echo ""
echo "=== Verifying static files ==="
if [ -d "./publish/wwwroot" ]; then
    echo "✓ wwwroot directory exists"
    ls -la ./publish/wwwroot/
    
    if [ -d "./publish/wwwroot/css" ]; then
        echo "✓ CSS directory exists"
        ls -la ./publish/wwwroot/css/
    else
        echo "✗ CSS directory missing! Copying manually..."
        cp -r ./wwwroot/css ./publish/wwwroot/
    fi
    
    if [ -d "./publish/wwwroot/js" ]; then
        echo "✓ JS directory exists"
    else
        echo "✗ JS directory missing! Copying manually..."
        cp -r ./wwwroot/js ./publish/wwwroot/
    fi
    
    if [ -d "./publish/wwwroot/lib" ]; then
        echo "✓ lib directory exists"
    else
        echo "✗ lib directory missing! Copying manually..."
        cp -r ./wwwroot/lib ./publish/wwwroot/
    fi
    
    if [ -d "./publish/wwwroot/images" ]; then
        echo "✓ images directory exists"
    else
        echo "✗ images directory missing! Copying manually..."
        cp -r ./wwwroot/images ./publish/wwwroot/
    fi
else
    echo "✗ wwwroot directory missing! Copying all static files..."
    cp -r ./wwwroot ./publish/
fi

echo ""
echo "=== Publish complete ==="
echo "Files in ./publish:"
ls -la ./publish/

echo ""
echo "=== Next Steps ==="
echo "1. Create a ZIP of the publish folder"
echo "2. Deploy to Azure App Service using:"
echo "   - Azure Portal: Deployment Center > Manual Deploy"
echo "   - Or: az webapp deployment source config-zip"
echo ""
echo "To create ZIP:"
echo "  cd publish && zip -r ../deployment.zip . && cd .."

