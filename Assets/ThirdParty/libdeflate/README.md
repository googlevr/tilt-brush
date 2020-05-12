# Overview

libdeflate is a library for fast, whole-buffer DEFLATE-based compression and
decompression.

The supported formats are:

- DEFLATE (raw)
- zlib (a.k.a. DEFLATE with a zlib wrapper)
- gzip (a.k.a. DEFLATE with a gzip wrapper)

libdeflate is heavily optimized.  It is significantly faster than the zlib
library, both for compression and decompression, and especially on x86
processors.  In addition, libdeflate provides optional high compression modes
that provide a better compression ratio than the zlib's "level 9".

libdeflate itself is a library, but the following command-line programs which
use this library are also provided:

* gzip (or gunzip), a program which mostly behaves like the standard equivalent,
  except that it does not yet have good streaming support and therefore does not
  yet support very large files
* benchmark, a program for benchmarking in-memory compression and decompression

# Building

## For UNIX

Just run `make`.  You need GNU Make and either GCC or Clang.  GCC is recommended
because it builds slightly faster binaries.  There is no `make install` yet;
just copy the file(s) to where you want.

By default, all targets are built, including the library and programs, with the
exception of the `benchmark` program.  `make help` shows the available targets.
There are also several options which can be set on the `make` command line.  See
the Makefile for details.

## For Windows

Prebuilt Windows binaries can be downloaded from
https://github.com/ebiggers/libdeflate/releases.  But if you need to build the
binaries yourself, MinGW (gcc) is the recommended compiler to use.  If you're
performing the build *on* Windows (as opposed to cross-compiling for Windows on
Linux, for example), you'll need to follow the directions in **one** of the two
sections below to set up a minimal UNIX-compatible environment using either
Cygwin or MSYS2, then do the build.  (Other MinGW distributions may not work, as
they often omit basic UNIX tools such as `sh`.)

Alternatively, libdeflate may be built using the Visual Studio toolchain by
running `nmake /f Makefile.msc`.  However, while this is supported in the sense
that it will produce working binaries, it is not recommended because the
binaries built with MinGW will be significantly faster.

Also note that 64-bit binaries are faster than 32-bit binaries and should be
preferred whenever possible.

### Using Cygwin

Run the Cygwin installer, available from https://cygwin.com/setup-x86_64.exe.
When you get to the package selection screen, choose the following additional
packages from category "Devel":

- git
- make
- mingw64-i686-binutils
- mingw64-i686-gcc-g++
- mingw64-x86_64-binutils
- mingw64-x86_64-gcc-g++

(You may skip the mingw64-i686 packages if you don't need to build 32-bit
binaries.)

After the installation finishes, open a Cygwin terminal.  Then download
libdeflate's source code (if you haven't already) and `cd` into its directory:

    git clone https://github.com/ebiggers/libdeflate
    cd libdeflate

(Note that it's not required to use `git`; an alternative is to extract a .zip
or .tar.gz archive of the source code downloaded from the releases page.
Also, in case you need to find it in the file browser, note that your home
directory in Cygwin is usually located at `C:\cygwin64\home\<your username>`.)

Then, to build 64-bit binaries:

    make CC=x86_64-w64-mingw32-gcc

or to build 32-bit binaries:

    make CC=i686-w64-mingw32-gcc

### Using MSYS2

Run the MSYS2 installer, available from http://www.msys2.org/.  After
installing, open an MSYS2 shell and run:

    pacman -Syu

Say `y`, then when it's finished, close the shell window and open a new one.
Then run the same command again:

    pacman -Syu

Then, install the packages needed to build libdeflate:

    pacman -S git \
              make \
              mingw-w64-i686-binutils \
              mingw-w64-i686-gcc \
              mingw-w64-x86_64-binutils \
              mingw-w64-x86_64-gcc

(You may skip the mingw-w64-i686 packages if you don't need to build 32-bit
binaries.)

Then download libdeflate's source code (if you haven't already):

    git clone https://github.com/ebiggers/libdeflate

(Note that it's not required to use `git`; an alternative is to extract a .zip
or .tar.gz archive of the source code downloaded from the releases page.
Also, in case you need to find it in the file browser, note that your home
directory in MSYS2 is usually located at `C:\msys64\home\<your username>`.)

Then, to build 64-bit binaries, open "MSYS2 MinGW 64-bit" from the Start menu
and run the following commands:

    cd libdeflate
    make clean
    make

Or to build 32-bit binaries, do the same but use "MSYS2 MinGW 32-bit" instead.

# API

libdeflate has a simple API that is not zlib-compatible.  You can create
compressors and decompressors and use them to compress or decompress buffers.
See libdeflate.h for details.

There is currently no support for streaming.  This has been considered, but it
always significantly increases complexity and slows down fast paths.
Unfortunately, at this point it remains a future TODO.  So: if your application
compresses data in "chunks", say, less than 1 MB in size, then libdeflate is a
great choice for you; that's what it's designed to do.  This is perfect for
certain use cases such as transparent filesystem compression.  But if your
application compresses large files as a single compressed stream, similarly to
the `gzip` program, then libdeflate isn't for you.

Note that with chunk-based compression, you generally should have the
uncompressed size of each chunk stored outside of the compressed data itself.
This enables you to allocate an output buffer of the correct size without
guessing.  However, libdeflate's decompression routines do optionally provide
the actual number of output bytes in case you need it.

# DEFLATE vs. zlib vs. gzip

The DEFLATE format ([rfc1951](https://www.ietf.org/rfc/rfc1951.txt)), the zlib
format ([rfc1950](https://www.ietf.org/rfc/rfc1950.txt)), and the gzip format
([rfc1952](https://www.ietf.org/rfc/rfc1952.txt)) are commonly confused with
each other as well as with the [zlib software library](http://zlib.net), which
actually supports all three formats.  libdeflate (this library) also supports
all three formats.

Briefly, DEFLATE is a raw compressed stream, whereas zlib and gzip are different
wrappers for this stream.  Both zlib and gzip include checksums, but gzip can
include extra information such as the original filename.  Generally, you should
choose a format as follows:

- If you are compressing whole files with no subdivisions, similar to the `gzip`
  program, you probably should use the gzip format.
- Otherwise, if you don't need the features of the gzip header and footer but do
  still want a checksum for corruption detection, you probably should use the
  zlib format.
- Otherwise, you probably should use raw DEFLATE.  This is ideal if you don't
  need checksums, e.g. because they're simply not needed for your use case or
  because you already compute your own checksums that are stored separately from
  the compressed stream.

Note that gzip and zlib streams can be distinguished from each other based on
their starting bytes, but this is not necessarily true of raw DEFLATE streams.

# Compression levels

An often-underappreciated fact of compression formats such as DEFLATE is that
there are an enormous number of different ways that a given input could be
compressed.  Different algorithms and different amounts of computation time will
result in different compression ratios, while remaining equally compatible with
the decompressor.

For this reason, the commonly used zlib library provides nine compression
levels.  Level 1 is the fastest but provides the worst compression; level 9
provides the best compression but is the slowest.  It defaults to level 6.
libdeflate uses this same design but is designed to improve on both zlib's
performance *and* compression ratio at every compression level.  In addition,
libdeflate's levels go [up to 12](https://xkcd.com/670/) to make room for a
minimum-cost-path based algorithm (sometimes called "optimal parsing") that can
significantly improve on zlib's compression ratio.

If you are using DEFLATE (or zlib, or gzip) in your application, you should test
different levels to see which works best for your application.

# Motivation

Despite DEFLATE's widespread use mainly through the zlib library, in the
compression community this format from the early 1990s is often considered
obsolete.  And in a few significant ways, it is.

So why implement DEFLATE at all, instead of focusing entirely on
bzip2/LZMA/xz/LZ4/LZX/ZSTD/Brotli/LZHAM/LZFSE/[insert cool new format here]?

To do something better, you need to understand what came before.  And it turns
out that most ideas from DEFLATE are still relevant.  Many of the newer formats
share a similar structure as DEFLATE, with different tweaks.  The effects of
trivial but very useful tweaks, such as increasing the sliding window size, are
often confused with the effects of nontrivial but less useful tweaks.  And
actually, many of these formats are similar enough that common algorithms and
optimizations (e.g. those dealing with LZ77 matchfinding) can be reused.

In addition, comparing compressors fairly is difficult because the performance
of a compressor depends heavily on optimizations which are not intrinsic to the
compression format itself.  In this respect, the zlib library sometimes compares
poorly to certain newer code because zlib is not well optimized for modern
processors.  libdeflate addresses this by providing an optimized DEFLATE
implementation which can be used for benchmarking purposes.  And, of course,
real applications can use it as well.

That being said, I have also started [a separate
project](https://github.com/ebiggers/xpack) for an experimental, more modern
compression format.

# License

libdeflate is [MIT-licensed](COPYING).

Additional notes (informational only):

- I am not aware of any patents covering libdeflate.

- Old versions of libdeflate were public domain; I only started copyrighting
  changes in newer versions.  Portions of the source code that have not been
  changed since being released in a public domain version can theoretically
  still be used as public domain if you want to.  But for practical purposes, it
  probably would be easier to just take the MIT license option, which is nearly
  the same anyway.
