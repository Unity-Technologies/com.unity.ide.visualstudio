#!/bin/sh
olddir="$(pwd)"
cd "$(dirname $0)/../Packages/com.unity.ide.visualstudio/Editor/AppleEventIntegration~"
xcodebuild -configuration Release
cp -R build/Release/AppleEventIntegration.bundle/* ../Plugins/AppleEventIntegration.bundle/
cd "$olddir"