# Win Tools
Advanced set of useful plugins for controlling Windows from the Stream Deck

## New in v1.3
- :new: ***VIRTUAL DESKTOP SUPPORT***: Create, Delete and Switch to a Windows Virtual Desktop all from the Stream Deck
- Use along with Windows Mover & Resizer plugin to manage Virtual Desktops and then move the desired apps to there.
- Updates to `Drive Info` action:
    - Added support for rotating between all drives on your PC
    - Added option to show Drive Label/Name in addition to drive letter
    - Clicking the key will open Windows Explorer to that drive
- `Explorer Scratch Pad` now supports folders with non-ASCII characters such as `ä`,`æ`,`א` using the `Encoding Code Page` setting
- `Multi-Clip` action now also supports fetching the current Clipboard contents (instead of the current highlighted text)
- Fixed bug in `Multi-Clip` not fetching the right selected text

## Features:
- `App Audio Mixer` - Control the volume of all your Windows apps straight from the Stream Deck!!!
    - Mixer mode shows you all relevant apps and you can quickly +/- the volume for each or click the app icon to mute it. Full Screen support for both Classic and XL
- `App Volume Adjuster` and `App Volume Set` allow quick volume changes to one specific app.
- `Drive Info` shows you stats on your Hard Drives and warns you when space is getting low
- `Latest File Copy` - Monitors a directory for changes and copies the last modified file to a customizable location. Great to ensure you always have the latest Instant Replay available at a specific location.
- `Lock` Action... well... Locks your Computer
- `Multi Clip` action - Turns your Stream Deck into multiple clipboards!
    1. Select some text and then **Long Press** the Stream Deck key to store it
    2. Short Press to paste it
    3. Rinse & Repeat. Each key can store a DIFFERENT entry
- `Network Card Info` - Shows if your current Network Card is up or down (Great to check if your VPN is connected)
- `Ping` - Ping servers and see the latency on the Stream Deck
- `Power Plan` Action - Allows you to change between the various Windows power plans 
- `Primary Monitor` allows you to set your Primary Monitor from the Stream Deck
- `Services` - Start/Stop/Restart Windows Services from the Stream Deck (Requires Stream Deck to run as Admin)
- `Uptime` Action shows you your PC uptime (since last reboot)
- `Virtual Desktop` - Create, Delete and Switch to a Windows Virtual Desktop all from the Stream Deck
- `Windows Explorer Scratch Pad` :sunglasses: - Quickly cycle between common directories on your PC. Long press the key while in Windows Explorer to store the current folder. Then Short Press every time you want to go to that folder. Rinse and Repeat.

### Download

* [Download plugin](https://github.com/BarRaider/streamdeck-wintools/releases/)

## I found a bug, who do I contact?
For support please contact the developer. Contact information is available at https://barraider.com

## I have a feature request, who do I contact?
Please contact the developer. Contact information is available at https://barraider.com

## Dependencies
* Uses StreamDeck-Tools by BarRaider: [![NuGet](https://img.shields.io/nuget/v/streamdeck-tools.svg?style=flat)](https://www.nuget.org/packages/streamdeck-tools)
* Uses [Easy-PI](https://github.com/BarRaider/streamdeck-easypi) by BarRaider - Provides seamless integration with the Stream Deck PI (Property Inspector) 


## Change Log

## New in v1.2
- ***Stream Deck Mobile Support!***
- :new: **Volume Fade** - `App Volume Set` action now supports Fade In/Fade Out :PogChamp: 
- **New Action** `Drive Info` shows you stats on your Hard Drives and warns you when space is getting low
- **New Action** `Primary Monitor` allows you to set your Primary Monitor from the Stream Deck
- `App Volume Mixer` and `App Volume Adjust` support finer grain volume adjustments
- `Explorer Scratch Pad` now has a `Lock` setting to prevent actual modification of the directory
- `Explorer Scratch Pad` now has supports Short Clicking to copy the directory location to clipboard INSTEAD of opening an Explorer window
- `Multi-Clip` now supports reusing the same clipboard across multiple Stream Deck profiles (using the `Shared Id` setting)
- Multi-Action support for `App Volume Set` / `App Volume Adjust` / `Primary Monitor`

## New in v1.1
- **New Action** `App Audio Mixer` - Control the volume of all your Windows apps straight from the Stream Deck!!!
    - Mixer mode shows you all relevant apps and you can quickly +/- the volume for each or click the app icon to mute it. Full Screen support for both Classic and XL
- `App Volume Adjuster` and `App Volume Set` allow quick volume changes to one specific app.
- **New Action** `Multi Clip` action - Turns your Stream Deck into multiple clipboards!
    1. Select some text and then **Long Press** the Stream Deck key to store it
    2. Short Press to paste it
    3. Rinse & Repeat. Each key can store a DIFFERENT entry
    - Customizable Long-Press length
    - Customizable Audio sound when long pressing (to indicate it copied)
- **New Action** `Uptime` Action shows you your PC uptime (since last reboot)
- **New Action** `Lock` Action... well... Locks your Computer
- `Ping` Action now supports customizable images to give you visual indication if the ping is above/below latency or timing out.
- `Explorer Scratch Pad` got a customizable Long Press length too
- `Power Plan` Action will now turn the icon Green if the selected plan is also the active one.

