FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Explicit rather than relying on the base image's own default: AWS App Runner is told which
# container port to route to (see README "Deploy en AWS App Runner"), so this must stay in sync
# with that setting. Binds all interfaces (0.0.0.0), required for the container's network
# namespace - "localhost" would be unreachable from outside the container.
ENV ASPNETCORE_URLS=http://+:8080
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
# mcr.microsoft.com/dotnet/aspnet images ship a built-in non-root "app" user; run as that
# instead of root inside the container.
USER app
ENTRYPOINT ["dotnet", "MedicalAppointments.Api.dll"]
