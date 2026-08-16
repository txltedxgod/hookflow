FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/HookFlow/HookFlow.csproj", "src/HookFlow/"]
RUN dotnet restore "src/HookFlow/HookFlow.csproj"

COPY . .
WORKDIR "/src/src/HookFlow"
RUN dotnet publish "HookFlow.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "HookFlow.dll"]
