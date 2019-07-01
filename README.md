# Chelydra

[![Join the chat at https://discord.gg/uGkBkeW](https://img.shields.io/discord/580823343733932032.svg?color=%2320ce88&label=Discord)](https://discord.gg/uGkBkeW)

`Chelydra` is the genus of the common snapping turtle-- and this is a Enklu-enabled DSLR controller. What do they have in common? THEY TAKE SNAPS.

Controls a DSLR camera based on events from the Enklu cloud.

### Prerequisites 

* `dotnet`
* `captura-cli`
* `gphoto` (optional) - support for this was removed, but will be coming back!

### Build and Run

Build with `dotnet build`.

Run with `dotnet run -- -o [ORG ID] -t [TOKEN]`.

### Publish

Publish with:

`dotnet build`
`dotnet publish -c Release -r win10-x64`
