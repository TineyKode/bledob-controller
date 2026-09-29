# BLEDOB

Windows controller for an ELK-BLEDOB LED strip, the kind the LotusLamp X phone app drives.

The strip is not a Windows Dynamic Lighting device. This app sends the same Bluetooth commands as the phone: power, one RGB color for the whole strip, brightness, and the effects that run on the controller.

Close the phone app before connecting. The strip accepts one Bluetooth connection.

## Open it

After `install.ps1`, BLEDOB is on the Start menu and the desktop, and it starts in the tray when you sign in. The installed copy is `%LocalAppData%\Programs\Bledob\Bledob.exe`.

Click the amber tray icon, or open BLEDOB from the Start menu, when you want the window. Closing the window leaves it running and releases Bluetooth so your phone can use the strip. Quit is on the tray icon's menu.

To install or refresh that copy from this folder:

```
powershell -ExecutionPolicy Bypass -File install.ps1
```

The address box starts as `BE:37:33:00:0D:0B`. Scan, then Connect. Double-click a device in the list to connect to it. The last address is saved in `%AppData%\Bledob\settings.json`.

For a one-off run from source instead:

```
dotnet run --project src/Bledob.App
```

To see what the radio can hear, without connecting:

```
dotnet run --project src/Bledob.Scan
```

Devices whose name looks like this strip are marked `strip`.

To connect once from the command line, turn the strip on, and set warm white:

```
dotnet run --project src/Bledob.Scan -- connect
```

## Tests

```
dotnet test
```

The tests cover the command frames. They do not need the strip to be nearby.
