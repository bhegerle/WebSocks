# WebStunnel

Encapsulates TCP within a lightly secured WebSocket. Useful for
managing cloud devices via a bastion host, especially if intermediate
firewalls block non-HTTP traffic.

Authorization is through a shared-secret via HMAC.

Here is a diagram:

```
         +--------------+                   +-------------+
  TCP -> | TCP listener | --- WebSocket --> | WS Listener | --- TCP ---> #.#.#.#:##
         +--------------+                   +-------------+ 
```

## Requirements

Requires ASP.NET core 6.

## Configuration

Configuration is through a JSON file.

A TCP listener file looks like this:
```
{
  "ListenOn": "tcp://localhost:22/",
  "TunnelTo": "ws://199.9.9.9/",
  "Key": "secret"
}
```

A WS listener file looks like this:
```
{
  "ListenOn": "ws://0.0.0.0:8008/",
  "TunnelTo": "tcp://127.0.0.1:22/",
  "Key": "secret"
}
```

Other properties are:
```
{
  "LogPath": "/some/file",
  "Proxy": {
    "UseSystemProxy": false,
    "HttpProxy": "http://proxy.local:8080/"
  }
}
```

## Running

`wstunnel config.json` 
