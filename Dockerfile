# Use the official ASP.NET Core runtime image as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

# Use the SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["LiveChat.csproj", "./"]
RUN dotnet restore "./LiveChat.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "LiveChat.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LiveChat.csproj" -c Release -o /app/publish

# Copy the build result to the base image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "YourProjectName.dll"]