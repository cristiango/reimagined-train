FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

ARG GitHubToken
ENV GitHubToken=${GitHubToken}

COPY . ./repo/
RUN git config --system --add safe.directory /repo

WORKDIR /repo