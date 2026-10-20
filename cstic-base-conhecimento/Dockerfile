FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json ./
COPY src/Cstic.KnowledgeBase/Cstic.KnowledgeBase.csproj src/Cstic.KnowledgeBase/
RUN dotnet restore src/Cstic.KnowledgeBase/Cstic.KnowledgeBase.csproj
COPY src/Cstic.KnowledgeBase/ src/Cstic.KnowledgeBase/
RUN dotnet publish src/Cstic.KnowledgeBase/Cstic.KnowledgeBase.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
# Database and session keys; mount a volume here so users and POPs survive container updates.
RUN mkdir -p /app/App_Data && chown app:app /app/App_Data
VOLUME /app/App_Data
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Cstic.KnowledgeBase.dll"]
