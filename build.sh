
# Variables

now=$(date)
buildExtension=$(date +"%Y_%m_%d__%H_%M")

# Code Stuff

echo "Building for release."
echo "Please choose which files to build:"
echo "[1] Windows x64"
echo "[2] Windows x86"
echo "[3] Windows Selfcontained-x64"
echo "[4] MacOS"
echo "[5] Linux"
echo "[6] All"

read -p "Please enter your choice: " choice

if [ $choice == "1" ] || [ $choice == "6" ]
then
	echo "Building for Windows x64"
	dotnet publish \csp.csproj -r win-x64 -c Release -p:publishsinglefile=true --self-contained false -p:debugsymbols=false -p:debugtype=none -o Builds/colorsplitter-win-x64-$buildExtension/csp
	cd Builds/colorsplitter-win-x64-$buildExtension
	zip -r $OLDPWD/Builds/colorsplitter-win-x64-$buildExtension.zip .
	cd -
	rm -r Builds/colorsplitter-win-x64-$buildExtension
fi
if [ $choice == "2" ] || [ $choice == "6" ]
then
	echo "Building for Windows x86"
	dotnet publish \csp.csproj -r win-x86 -c Release -p:publishsinglefile=true --self-contained false -p:debugsymbols=false -p:debugtype=none -o Builds/colorsplitter-win-x86-$buildExtension/csp
	cd Builds/colorsplitter-win-x86-$buildExtension
	zip -r $OLDPWD/Builds/colorsplitter-win-x86-$buildExtension.zip .
	cd -
	rm -r Builds/colorsplitter-win-x86-$buildExtension
fi
if [ $choice == "3" ] || [ $choice == "6" ]
then
	echo "Building for Windows Selfcontained-x64"
	dotnet publish \csp.csproj -r win-x64 -c Release -p:publishsinglefile=true --self-contained true -p:debugsymbols=false -p:debugtype=none -o Builds/colorsplitter-win-$buildExtension/csp
	cd Builds/colorsplitter-win-$buildExtension
	zip -r $OLDPWD/Builds/colorsplitter-win-$buildExtension.zip .
	cd -
	rm -r Builds/colorsplitter-win-$buildExtension
fi
if [ $choice == "4" ] || [ $choice == "6" ]
then
	echo "Building for MacOS x64"
	dotnet publish \csp.csproj -r osx-x64 -c Release -p:publishsinglefile=true --self-contained true -p:debugsymbols=false -p:debugtype=none -o Builds/colorsplitter-macos-x64-$buildExtension/csp
	cd Builds/colorsplitter-macos-x64-$buildExtension
	zip -r $OLDPWD/Builds/colorsplitter-macos-x64-$buildExtension.zip .
	cd -
	rm -r Builds/colorsplitter-macos-x64-$buildExtension
fi
if [ $choice == "5" ] || [ $choice == "6" ]
then
	echo "Building for Linux x64"
	dotnet publish \csp.csproj -r linux-x64 -c Release -p:publishsinglefile=true --self-contained true -p:debugsymbols=false -p:debugtype=none -o Builds/colorsplitter-linux-x64-$buildExtension/csp
	cd Builds/colorsplitter-linux-x64-$buildExtension
	zip -r $OLDPWD/Builds/colorsplitter-linux-x64-$buildExtension.zip .
	cd -
	rm -r Builds/colorsplitter-linux-x64-$buildExtension
fi