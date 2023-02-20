FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

ARG GitHubToken
ENV GitHubToken=${GitHubToken}

#RUN dotnet nuget add source --name evision-github --username github \
#    --password $GitHubTokenPackagesReadOnly --store-password-in-clear-text \
#    https://nuget.pkg.github.com/eVisionSoftware/index.json
#
#RUN echo "${GitHubToken}" | docker login ghcr.io -u ci --password-stdin

COPY . ./repo/

RUN git config --system --add safe.directory /repo

WORKDIR /repo