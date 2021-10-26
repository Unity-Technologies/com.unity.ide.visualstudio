#!/bin/sh
olddir="$(pwd)"
cd "$(dirname $0)/../Packages/com.unity.ide.visualstudio/Editor/AppleEventIntegration~"
xcodebuild -configuration Release
mv build/Release/AppleEventIntegration.bundle ../Plugins/
cd "$olddir"