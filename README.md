# MTCF - Moonlight to Cloudflare

**MTCF** (Moonlight to Cloudflare) is a lightweight console utility that automatically creates DNS records for Minecraft servers managed through a [Moonlight Panel](https://moonlightpanel.xyz/) instance. It allows each server to be accessed via a custom subdomain, eliminating the need to share raw IP addresses.

## ✨ Features

- ✅ Automatically detects when new Minecraft servers start
- 🌐 Creates or updates DNS A/AAAA records via the Cloudflare API
- 📦 Lightweight and easy to deploy


> [!NOTE]
> Please be aware that this project was developed for a very specific setup and it is very likely that you have to adjust it to fit to yours.

## Basic Idea

The application runs in two seperate instances, one in Cloudflare mode and one in Moonlight mode. They communicate via a minimal API.

This allows any user who starts a server in the Moonlight panel to have a direct connection to that server over something like "my-server.my-domain.de" instead of using something like "node.my-domain.de:2000".

## Modes

### 1. Cloudflare

Adds/Removes A/AAAA and SRV records for individual servers.

### 2. Moonlight 

Fetches the configuration of any active servers.
> [!NOTE]
> Unfortunately, the **V1** Version of Moonlight **does not** currently support an actual API. That means that currently, this script is getting the data from Docker.
>
> That means that for this to work, we rely on users to create a dns.json file in the server directory (see "Setup" below), which introduces the risk of users changing these without knowing what they're doing.
>
> Once **V2** of Moonlight releases and we are able to access it, this script will be updated to follow a different configuration workflow which does not rely on the user entering important information (such as IP address etc.)



 ## Setup

 For both instances, a `.env` File is needed. They need to have the following configurations:

**Cloudflare**
```
CLOUDFLAREAPIKEY=????
CLOUDFLAREZONEID=????
MOONLIGHTAPIKEY=????
MOONLIGHTAPIURL=????
```

**Moonlight**
```
MOONLIGHTAPIKEY=????
```

In the current version, you can choose `MOONLIGHTAPIKEY` freely, since it's only used for auth between the two instances.

Currently, in **Moonlight** mode, the API is always hosted on `localhost:5000`, that will be configurable in a future version.


## Example `dns.json` file:

```json
{
    "IpAddress": "***.***.***.***",
    "Port": 2000,
    "Domain": "kaenguruu.dev",
    "Subdomain": "test"
}
```

This configuration will cause the script to create SRV and A records that allow players to connect to the server via `test.kaenguruu.dev`, which will connect them to the server running in the Moonlight panel on port 2000.


## Goals

1. With Moonlight V2
> Use Moonlight API instead of Docker to get list of servers
>
> Only allow the users who create a server to specify the subdomain, the rest will be automatically read from the API

2. Allow more configuration for the script setup
