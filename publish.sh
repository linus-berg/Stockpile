#!/bin/sh

dotnet publish -p:PublishSingleFile=true -c Release --self-contained true -r win-x64 -o ./Dist/Windows/
dotnet publish -p:PublishSingleFile=true -c Release --self-contained true -r linux-x64 -o ./Dist/Linux/
