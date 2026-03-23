# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

# Zorg dat je in de /src map werkt
WORKDIR /src

# Kopieer EERST alle .csproj bestanden (inclusief je afhankelijkheden!)
# Voeg hier ook COPY regels toe voor GB.Application, GB.Domain, etc. als je die gebruikt.
COPY ["Green Blanket Project _ Backend/Green Blanket Project _ Backend.csproj", "Green Blanket Project _ Backend/"]
COPY ["GB.Application/GB.Application.csproj", "GB.Application/"]
COPY ["GB.Domain/GB.Domain.csproj", "GB.Domain/"]
COPY ["GB.Infrastructure/GB.Infrastructure.csproj", "GB.Infrastructure/"]

# Restore het hoofdproject (die trekt de rest mee)
RUN dotnet restore "Green Blanket Project _ Backend/Green Blanket Project _ Backend.csproj"

# Kopieer daarna pas alle overige bestanden
COPY . .

# Navigeer naar de map van je API en bouw het
WORKDIR "/src/Green Blanket Project _ Backend"
RUN dotnet build "Green Blanket Project _ Backend.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Green Blanket Project _ Backend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Green Blanket Project _ Backend.dll"]