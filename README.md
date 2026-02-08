# R4Everyone

R4Everyone is an open-source, web-based modern alternative to R4 Cheat Code Editor, known as r4cce. Both applications
were designed to edit usrcheat.dat R4 files.

# Features & Limitations

## Features

1. WebAssembly, meaning near-native speeds. In the browser. Yeah.
2. Installable as a PWA, supporting offline use. The installed app is 304 Kb, compared to r4cce's 1.45 MiB (\*Size on
   disk).
3. Client-side only, no data leaves your device.

## Limitations

1. Not all r4cce features have been implemented (See below).
2. Not yet fully reactive, meaning it requires a display of at least 992x530 pixels to work.
3. It does not work on iPhone due to screen size, but it _does_ work on iPad. However, an error appears on the app that
   says an error occurred and to reload. However, the app will still function.
4. Does not work on larger files. WASM has memory limitations, and editing large files will cause WASM to OOM.

## Planned Features

1. Undo/redo support
2. Better cheat code editor (Copy/paste support)
3. Support for adding triggers
4. Sorting/moving items
5. Dark mode
6. Import/export XML
7. Mobile support (Fully reactive layout)
8. Confirmation before deleting games or folders that have content or cheats that have been modified from "default"

# Why R4Everyone when r4cce works just fine?

R4cce works just fine for Windows users only. But with the prevalence of mobile emulators (Delta), handheld gaming PCs
(Steam Deck), and other non-Windows devices, plus the fact that r4cce isn't available anymore through official channels,
an alternative was necessary.

R4cce was last updated nearly two decades ago (2009) and, from what I can tell, is no longer maintained. R4cce was
hosted on the author's website and, as of today, 2026-02-07, that site is down. DNS seems to resolve, but the site
times out. I cannot find any copies of the software online, either (which wouldn't have been allowed anyway due to the
licensing). They all go to dead .co.jp domains. Archive.org does have the website archived, and that's the only place I
can find to get it safely.

As well, r4cce was closed-source, and **very** slow (At least, in my testing). Loading a 50MB (Yes, 50 ***MB***) file
with r4cce on my Ryzen 9 5900X took anywhere from 1 to 5 minutes, and sometimes would bring my computer to a halt while
doing so. Normal user operations are pretty fast, and most users will not experience these limitations when editing
normal files (read: kilobytes in size), but r4cce struggles with larger files.

R4Everyone is intended to fix these issues. It's designed to be extremely fast, thanks to my library,
`R4Everyone.Binary4Everyone`.

## What is `R4Everyone.Binary4Everyone`?

`R4Everyone.Binary4Everyone` allows reading and writing of R4 .dat files via modern .NET.
Using ImHex and weeks of research (Because the file format is so obscure, even AI couldn't help me find resources on
it), I was able to reverse-engineer the file format and wrote this library to read and write it. My notes on the format
will be uploaded at a later date, but I already have
[a blog post about it](https://medium.com/@natereprogle/reverse-engineering-a-long-lost-file-format-usrcheat-dat-2c15fefe2f63)!

## A "fast" web app? Suuuuuure.

I get it, JS typically sucks when it comes to "being fast." HOWEVER, R4Everyone _does_ manage to be MUCH faster than JS,
due to it actually being a Blazor WebAssembly Standalone app! This means that a couple of things:

1. The app is 100% client-side. It does not communicate with a server, and therefore no data ever leaves your device.
2. The app is able to maintain its claim of being fast because it's written in .NET, not JS, and .NET compiled to WASM
   runs at near-native speeds.
3. The app has been configured as a PWA which means it's also installable on your device, offline.
4. The app works on Windows, macOS, and Linux. In fact, it works on any device that supports WASM!

## Are there any downsides?

Yes, the app does have some limitations that r4cce does not.

1. r4cce supports all four possible encoding methods that this app does not. This app defaults to UTF8, r4cce supports
   UTF8, GBK, SJIS, and BIG5. The library _supports_ the other encoding methods, but will use UTF8 regardless.
2. r4cce supports CycloEvo Cheats and R4/EDGE cheats. This app does not support CycloEvo.
3. r4cce supports importing/exporting XML, this app does not.
4. r4cce has an "encrypt file" feature, this app does not.
5. Sorting/moving items is not yet supported.
6. Adding "triggers" to cheats is not yet supported (You can't set a trigger to enable the cheat if L + R is held, as an
   example. r4cce allows this.)

If you need support for any of these features, this app may not support those yet. Side note: If you _have_ knowledge of
the file format for CycloEvo, please let me know! I investigated it at one point but was not able to figure anything out
about its format.