FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore MedicalAppointments.slnx --locked-mode
RUN dotnet publish src/MedicalAppointments.Api/MedicalAppointments.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MedicalAppointments.Api.dll"]
