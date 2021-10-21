set OLDDIR="%CD%"
cd "%~dp0\..\Packages\com.unity.ide.visualstudio\Editor\COMIntegration\Release"

cmake ../COMIntegration~ -B ./build
cmake --build ./build --config=release -- /p:OutDir=..

cd "%OLDDIR%"