# ================================================================================
# NoitaCA Fantasy.Net 服务端多阶段构建
# ================================================================================
# Stage 1 (build)   : .NET 8 SDK restore + publish
# Stage 2 (runtime) : ASP.NET 8 runtime, only contains publish output
#
# 构建命令:
#   docker build -t noitaca-server -f Server/Dockerfile .
#
# 运行命令 (示例, commandLineArgs 需按实际场景注入):
#   docker run --rm -it noitaca-server --m Develop
# ================================================================================

# ---------- Stage 1 : build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制解决方案与 csproj,先 restore 以利用 Docker 层缓存
COPY Server/Server.sln ./Server/
COPY Server/Main/Main.csproj    ./Server/Main/
COPY Server/Entity/Entity.csproj ./Server/Entity/
COPY Server/Hotfix/Hotfix.csproj ./Server/Hotfix/

RUN dotnet restore Server/Server.sln

# 复制全部源码并 publish
COPY Server/ ./Server/
RUN dotnet publish Server/Main/Main.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- Stage 2 : runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# 仅复制 publish 输出,镜像不含源码与 .pdb
COPY --from=build /app/publish ./

# Fantasy 进程模式参数 (与 launchSettings.json 一致);生产环境覆盖即可
ENTRYPOINT ["dotnet", "Main.dll", "--m", "Develop"]
