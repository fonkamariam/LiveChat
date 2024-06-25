# Use the .NET 8.0 SDK as the build environment
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /src

# Copy the csproj and restore any dependencies
COPY ["LiveChat.csproj", "./"]
RUN dotnet restore "./LiveChat.csproj"

# Copy the remaining project files and build the project
COPY . .
RUN dotnet publish "./LiveChat.csproj" -c Release -o /app/publish

# Use the .NET 8.0 runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/publish .

# Set the entry point for the container
ENTRYPOINT ["dotnet", "LiveChat.dll"]
