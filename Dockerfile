# =============================================================================
# STAGE 1 — BUILD
# Start from the .NET 8 SDK image. The SDK has the compilers and `dotnet`
# tooling needed to build and publish. It is large (~800MB) but it is thrown
# away after this stage, so it never ships to production.
# "AS build" names this stage so stage 2 can copy files out of it.
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# --- Dependency restore (done first, on its own, for layer caching) ---------
# Docker caches each step as a "layer". If the .csproj files have not changed,
# Docker reuses the cached restore instead of re-downloading every build.
# So we copy ONLY the project files first, then restore.
COPY ["PRM.API/PRM.API.csproj", "PRM.API/"]
COPY ["PRM.Application/PRM.Application.csproj", "PRM.Application/"]
COPY ["PRM.Domain/PRM.Domain.csproj", "PRM.Domain/"]
COPY ["PRM.Infrastructure/PRM.Infrastructure.csproj", "PRM.Infrastructure/"]
RUN dotnet restore "PRM.API/PRM.API.csproj"

# --- Copy the rest of the source and publish --------------------------------
# Now copy everything else (the actual .cs code).
COPY . .
# Publish the API in Release config into /app/publish. This is the same
# `dotnet publish` you ran locally — the deployable bundle.
RUN dotnet publish "PRM.API/PRM.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =============================================================================
# STAGE 2 — RUNTIME
# Start from the much smaller ASP.NET runtime image (~220MB). It has the
# .NET runtime to RUN the app, but no compilers/SDK. This is what ships.
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy ONLY the published output from the build stage. The source code,
# SDK, and build tools are left behind — keeping this image lean.
COPY --from=build /app/publish .

# Tell ASP.NET Core to listen on plain HTTP port 8080 inside the container.
# (No HTTPS inside the container — the cloud host handles TLS in front of it.)
ENV ASPNETCORE_URLS=http://+:8080

# Document that the container serves traffic on 8080. This is informational;
# it does not actually publish the port (that happens at `docker run`).
EXPOSE 8080

# The command that runs when a container starts from this image:
# launch the API via the .NET runtime using the published DLL.
ENTRYPOINT ["dotnet", "PRM.API.dll"]
