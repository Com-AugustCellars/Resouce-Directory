cd ResourceDirectory
rd /S /Q obj
rd /S /Q bin

msbuild /t:restore RD.deploy.csproj
msbuild /t:clean /t:pack /p:Configuration=Deploy /p:Platform="Any CPU" RD.deploy.csproj
copy "bin\dev\Deploy\com*.nupkg" \projects\nuget-packages
cd ..


