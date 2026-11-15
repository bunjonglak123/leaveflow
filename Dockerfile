# One image: Angular SPA built into the API's wwwroot, served by ASP.NET Core.
# Default config = public demo (SQLite, fake data, daily reset). For real use, override the
# Database__Provider / ConnectionStrings__Default / Demo__* / Jwt__Key environment variables.

FROM node:22-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npx ng build --output-path=/out/wwwroot

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY LeaveFlow.slnx ./
COPY src/LeaveFlow.Domain/LeaveFlow.Domain.csproj src/LeaveFlow.Domain/
COPY src/LeaveFlow.Api/LeaveFlow.Api.csproj src/LeaveFlow.Api/
RUN dotnet restore src/LeaveFlow.Api/LeaveFlow.Api.csproj
COPY src/ src/
RUN dotnet publish src/LeaveFlow.Api/LeaveFlow.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /out/wwwroot/browser ./wwwroot
RUN mkdir -p /data && chown app:app /data
USER app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    TZ=Asia/Bangkok \
    Database__Provider=Sqlite \
    ConnectionStrings__Default="Data Source=/data/leaveflow.db" \
    Seed__Enabled=true \
    Demo__Enabled=true \
    Demo__ResetDaily=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "LeaveFlow.Api.dll"]
