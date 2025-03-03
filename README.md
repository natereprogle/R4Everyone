# R4Everyone

R4Everyone is an open-source, modern alternative to r4cce. Both applications were designed to edit
usrcheat.dat R4 files.

## Why an alternative? Doesn't r4cce work just fine?

r4cce was last updated nearly two decades ago (2009) and, from what I can tell, is no longer maintained. As of today,
2025-03-01, it appears the website that hosted r4cce may also down. DNS resolves, but the site times out. I cannot find
any copies of the software online, either (which wouldn't have been allowed anyway due to the licensing). They all go to
dead .co.jp domains.

As well, r4cce was closed-source, and **very** slow (At least, in my testing).Loading a 50MB (Yes, 50 ***MB***) file with 
r4cce on my Ryzen 9 5900X takes upwards of 5 minutes, and brings my computer to a halt while doing so. Normal user 
operations are pretty fast, but r4cce struggles with larger files.

R4Everyone is intended to fix these issues. It's designed to be extremely fast, thanks to my custom library, `R4Everyone.Binary4Everyone`.

## What is `R4Everyone.Binary4Everyone`?

R4Everyone.Binary4Everyone allows extremely fast reads and writes of R4 .dat files. I painstakingly reverse-engineered
the file format and wrote a library to read and write it. You can see my notes on the file format in the file named
`File Format.md`.

## Will R4Everyone ever be cross-compatibility?

That's the goal. I attempted to start with Avalonia, but the lacking documentation made it difficult to learn and fix issues. As
someone who's never done desktop development before, it was a huge hurdle, and I need to start small before building up. I
decided to start using WinUI 3 for now, and slowly move things as I can.

This project is as much a learning experience for me as it is a tool for the community. I'm learning as I go, and I'm hoping
the community gets some use out of it in the process!