FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY ["ArticleService.csproj", "."]
RUN dotnet restore "ArticleService.csproj"

COPY . .
RUN dotnet build "ArticleService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ArticleService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Add curl for healthcheck
RUN apt-get update && apt-get install -y curl

COPY --from=publish /app/publish .

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "ArticleService.dll"]