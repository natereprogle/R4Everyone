# R4Everyone

R4Everyone is an open-source, web-based modern alternative to R4 Cheat Code Editor, known as r4cce. Both applications
were designed to edit usrcheat.dat R4 files.

# Features & Limitations

## Features

1. Near-native speeds in the browser, thanks to WebAssembly. Yeah, that's right.
2. Installable as a PWA, supporting offline use. macOS, Windows, Linux, Android, and iOS are all supported. Please check
   your vendor's documentation for PWA support, as some browsers (like Safari) have limited support for PWAs.
3. Client-side only, no data leaves your device.
4. Activator support. Activator keys can now be added to Cheat Codes! These allow users to define what keys should be
   pressed or not pressed for a cheat to run
5. Works on phones, tablets, and desktops. The layout adapts to your screen size, and on phones the game editor opens
   full-screen with tabs for cheats, game details, and the selected item.
6. Undo and redo, with buttons and keyboard shortcuts (Ctrl+Z, and Ctrl+Y or Ctrl+Shift+Z).
7. Drag and drop to reorder games, folders, and cheats, or to move a cheat into a folder. Works with a mouse, touch,
   or the keyboard. Folders, cheats, and games can also be sorted A–Z.
8. Import and export codelist XML, the format used by melonDS-style tools. Exports also keep enabled states, One Hot,
   and master codes, so they round-trip without loss.
9. Confirmation before removing games, cheats, or folders with content, and before discarding unsaved changes.
10. Open and save CycloDS Evolution (`.evoCheats`), EDGE (`.dat`), and M3 DS Real (`.db`) cheat files, and convert
    between any supported formats with "Export As…".
11. Opening a CycloDS Evolution or EDGE file that R4Everyone didn't create (judged by its creator tag) shows a warning
    that it may not read properly, which can be turned off with "Don't warn me again".
12. Ability to **read** M3 DS Real files as well as write. While r4cce can write them, it cannot read them, making it
    possible to finally edit these files!

## Limitations

1. Not all r4cce features have been implemented ([see below](#are-there-any-downsides))

# Why R4Everyone when r4cce works just fine?

R4cce works just fine, but only for Desktop and only on Windows (disregarding tools like Wine and Proton).
But with the prevalence of mobile emulators (Delta), handheld gaming PCs (Steam Deck), and other non-Windows devices,
plus the fact that r4cce isn't available anymore through official channels, an alternative was necessary.

R4cce was last updated nearly two decades ago (2009) and, from what I can tell, is no longer maintained. R4cce was
hosted on the author's website and, as of today, 2026-02-07, that site is down. DNS seems to resolve, but the site
times out. I cannot find any copies of the software online, either (which wouldn't have been allowed anyway due to the
licensing). They all go to dead `.co.jp` domains. Archive.org does have the website archived, and that's the only place
I can find to get it safely.

As well, r4cce was closed-source, and **very** slow (At least, in my testing). Loading a 50MB (Yes, 50 ***MB***) file
(the DeadSkullzJr R4 DB) with r4cce on my Ryzen 9 5900X took anywhere from 1 to 5 minutes, and sometimes would bring my
computer to a halt while doing so. Normal user operations are pretty fast, and most users will not experience these
limitations when editing normal files (read: kilobytes in size), but r4cce struggles with larger files.

R4Everyone is intended to fix these issues. It's designed to be extremely fast, thanks to my library,
`R4Everyone.Binary4Everyone`.

## What is `R4Everyone.Binary4Everyone`?

`R4Everyone.Binary4Everyone` allows reading and writing of R4, CycloEvo, EDGE, and M3Real .dat and .db files via modern
.NET. Using ImHex and weeks of research (Because the file format is so obscure, even AI couldn't help me find resources
on it), I was able to reverse-engineer the file format and wrote this library to read and write it. My notes on the
format will be uploaded at a later date, but I already
have [a blog post about it](https://medium.com/@natereprogle/reverse-engineering-a-long-lost-file-format-usrcheat-dat-2c15fefe2f63)!

## A "fast" web app? Suuuuuure.

I get it, JS typically sucks when it comes to "being fast." HOWEVER, R4Everyone _does_ manage to be MUCH faster than JS,
due to it actually being WebAssembly (specifically, Blazor WebAssembly Standalone)! This means that a couple of things:

1. The app is 100% client-side. It does not communicate with a server, and therefore no data ever leaves your device.
2. The app is able to maintain its claim of being fast because it's written in .NET, not JS, and .NET compiled to WASM
   runs at near-native speeds.
3. The app has been configured as a PWA which means it's also installable on your device, offline.
4. The app works on Windows, macOS, and Linux. In fact, it works on any device that has a browser which supports WASM!

For reference, the DeadSkullzJr file that takes my Ryzen 9 5900X _minutes_ to open in r4cce only takes about 1-2 seconds
using R4Everyone. And that's in the browser compared to a native desktop app. The speed gain is astronomical.

> [!NOTE]
> The DeadSkullzJr database is a worst-case scenario. There is no world in which anyone would realistically be editing
this
> file. It doesn't follow the standard format, and its size requires quite a bit of memory and processing power to
> open/edit.
> Memory-starved devices such as phones and low-end PCs will struggle with files of this size. For reference, my iPhone
> 17
> Pro Max completely locked up when opening it. Just because it can doesn't mean it should 😉

## Are there any downsides?

As of version 3.0.0, the app supports almost all of the features r4cce does, with two notable exceptions:

1. r4cce supports all four possible encoding methods that this app does not. This app defaults to UTF8, r4cce supports
   UTF8, GBK, SJIS, and BIG5. The library _supports_ the other encoding methods, but will use UTF8 regardless. Only
   M3Real files can be exported with a different encoding, and only if the user chooses to do so. However, since UTF8
   supports every Japanese character found in systems like Shift-JIS or EUC-JP, this is not generally a problem.
2. This app does not support the "encrypt file" feature that exists in r4cce.

If you need support for either of these features, this app may not support those yet.

# Screenshots

![Main UI](images/R4Everyone%20Main%20Screen.png)
![Editor](images/R4Everyone%20Game%20Editor.png)
![Light Mode](images/R4Everyone%20Light%20Mode.png)
<img src="images/R4Everyone%20Mobile.png" width="400" />

# Credits

Credit where credit is due!

## GameTDB

GameTDB is a fantastic resource for game information, and I used it to load the cover art in R4Everyone. R4Everyone
as it looks today would not be possible without the wonderful people at GameTDB!

https://www.gametdb.com/

## Jusrcheat

Jusrcheat was the only documentation I could find regarding the R4 file format online. This project really jump-started
my research into the R4 file format!

- https://github.com/xperia64/Jusrcheat

## Action Replay info

These sites were very helpful when understanding the Activator logic.

- https://osdl.sourceforge.net/main/documentation/misc/nintendo-DS/homebrew-guide/HomebrewForDS.html
- http://double.nz/nintendo_ds/nds_develop3.html
- https://doc.kodewerx.org/hacking_nds.html