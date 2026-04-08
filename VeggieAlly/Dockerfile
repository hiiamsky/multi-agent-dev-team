# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Build.props"]
COPY ["src/VeggieAlly.WebAPI/VeggieAlly.WebAPI.csproj", "src/VeggieAlly.WebAPI/"]
COPY ["src/VeggieAlly.Application/VeggieAlly.Application.csproj", "src/VeggieAlly.Application/"]
COPY ["src/VeggieAlly.Domain/VeggieAlly.Domain.csproj", "src/VeggieAlly.Domain/"]
COPY ["src/VeggieAlly.Infrastructure/VeggieAlly.Infrastructure.csproj", "src/VeggieAlly.Infrastructure/"]
RUN dotnet restore "src/VeggieAlly.WebAPI/VeggieAlly.WebAPI.csproj"
COPY . .
WORKDIR "/src/src/VeggieAlly.WebAPI"
RUN dotnet build "VeggieAlly.WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "VeggieAlly.WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VeggieAlly.WebAPI.dll"]